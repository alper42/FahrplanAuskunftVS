using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using FahrplanAuskunft.Models;

namespace FahrplanAuskunft.Services
{
    public class DbApiService
    {
        private static readonly HttpClient _http = new HttpClient();

        public async Task<List<string>> SucheStationenAsync(string query)
        {
            var url = $"https://v6.db.transport.rest/locations?query={Uri.EscapeDataString(query)}&results=8&stops=true&addresses=false&poi=false";
            var resp = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(resp);
            var namen = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
                if (item.TryGetProperty("name", out var n))
                    namen.Add(n.GetString() ?? "");
            return namen.Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        public async Task<ObservableCollection<Verbindung>> LadeVerbindungenAsync(
            string von, string nach, DateTime datum, TimeSpan zeit)
        {
            var vonId = await SucheStationIdAsync(von);
            var nachId = await SucheStationIdAsync(nach);
            if (vonId == null || nachId == null) throw new Exception("Station nicht gefunden");

            var dt = datum.Date.Add(zeit);
            var iso = dt.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var url = $"https://v6.db.transport.rest/journeys?from={vonId}&to={nachId}&departure={Uri.EscapeDataString(iso)}&results=6&language=de";

            var resp = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(resp);
            var journeys = doc.RootElement.GetProperty("journeys");
            var result = new ObservableCollection<Verbindung>();

            foreach (var journey in journeys.EnumerateArray())
            {
                var legs = journey.GetProperty("legs");
                var firstLeg = legs[0];
                var lastLeg = legs[legs.GetArrayLength() - 1];

                var dep = DateTime.Parse(firstLeg.GetProperty("departure").GetString() ?? "");
                var arr = DateTime.Parse(lastLeg.GetProperty("arrival").GetString() ?? "");
                var dauer = arr - dep;
                var umstiege = legs.GetArrayLength() - 1;

                string zugNr = "Zug";
                string zugFarbe = "#1F6FEB";
                if (firstLeg.TryGetProperty("line", out var lineEl))
                {
                    zugNr = lineEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var product = lineEl.TryGetProperty("product", out var p) ? p.GetString() ?? "" : "";
                    zugFarbe = product switch
                    {
                        "nationalExpress" => "#B31312",
                        "national"        => "#C62300",
                        "regionalExpress" => "#0044BB",
                        "regional"        => "#005A8E",
                        "suburban"        => "#008D4F",
                        _                 => "#1F6FEB"
                    };
                }

                double preis = 0;
                if (journey.TryGetProperty("price", out var priceEl) &&
                    priceEl.TryGetProperty("amount", out var amountEl))
                    preis = amountEl.GetDouble();
                if (preis == 0) preis = 19.90 + umstiege * 5;

                var halte = new List<Haltestelle>();
                foreach (var leg in legs.EnumerateArray())
                {
                    var orgName = leg.TryGetProperty("origin", out var org)
                        ? (org.TryGetProperty("name", out var on) ? on.GetString() ?? "" : "") : "";
                    var legDep = leg.TryGetProperty("departure", out var ld) ? ld.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(orgName))
                        halte.Add(new Haltestelle
                        {
                            Name = orgName,
                            Uhrzeit = string.IsNullOrEmpty(legDep) ? "" : DateTime.Parse(legDep).ToString("HH:mm"),
                            PunktFarbe = Color.FromRgb(48, 54, 61),
                            TextFarbe = new SolidColorBrush(Color.FromRgb(139, 148, 158))
                        });
                }

                if (halte.Any())
                {
                    halte[0].PunktFarbe = Color.FromRgb(31, 111, 235);
                    halte[0].TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243));
                }

                var destName = lastLeg.TryGetProperty("destination", out var dest)
                    ? (dest.TryGetProperty("name", out var dn) ? dn.GetString() ?? nach : nach) : nach;
                halte.Add(new Haltestelle
                {
                    Name = destName,
                    Uhrzeit = arr.ToString("HH:mm"),
                    PunktFarbe = Color.FromRgb(46, 160, 67),
                    TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243))
                });

                result.Add(new Verbindung
                {
                    Von = von, Nach = nach,
                    Abfahrtszeit = dep.ToString("HH:mm"),
                    Ankunftszeit = arr.ToString("HH:mm"),
                    Zugnummer = zugNr, ZugFarbeStr = zugFarbe,
                    Umstiege = umstiege,
                    Dauer = $"{(int)dauer.TotalHours}h {dauer.Minutes}min",
                    Preis = preis,
                    PuenktlichkeitText = "Echtzeit",
                    PuenktlichkeitFarbe = Color.FromRgb(31, 111, 235),
                    Haltestellen = halte
                });
            }
            return result;
        }

        private async Task<string?> SucheStationIdAsync(string name)
        {
            var url = $"https://v6.db.transport.rest/locations?query={Uri.EscapeDataString(name)}&results=1";
            var resp = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(resp);
            if (doc.RootElement.GetArrayLength() > 0)
                return doc.RootElement[0].GetProperty("id").GetString();
            return null;
        }

        public static ObservableCollection<Verbindung> GeneriereDemoVerbindungen(
            string von, string nach, DateTime datum, TimeSpan startZeit)
        {
            var rnd = new Random(von.GetHashCode() ^ nach.GetHashCode() ^ datum.DayOfYear);
            var result = new ObservableCollection<Verbindung>();
            var zugTypen = new[]
            {
                ("ICE", "#B31312", 0, 89.90), ("ICE", "#B31312", 1, 49.90),
                ("IC",  "#C62300", 1, 39.90), ("IC",  "#C62300", 2, 29.90),
                ("RE",  "#0044BB", 2, 19.90), ("RB",  "#005A8E", 3, 14.90),
            };

            var aktuelleZeit = startZeit;
            foreach (var (typ, farbe, umstiege, basisPreis) in zugTypen)
            {
                var nummer = rnd.Next(100, 999);
                var dauerMin = umstiege == 0 ? rnd.Next(240, 360)
                             : umstiege == 1 ? rnd.Next(300, 420) : rnd.Next(360, 540);
                var puenktlichkeit = rnd.Next(0, 10);
                var (pText, pFarbe) = puenktlichkeit switch
                {
                    < 3 => ("Pünktlich", Color.FromRgb(46, 160, 67)),
                    < 7 => ($"+{puenktlichkeit} Min", Color.FromRgb(227, 179, 65)),
                    _   => ($"+{puenktlichkeit} Min", Color.FromRgb(248, 81, 73))
                };

                var abfahrt = aktuelleZeit;
                var ankunft = aktuelleZeit.Add(TimeSpan.FromMinutes(dauerMin));

                result.Add(new Verbindung
                {
                    Von = von, Nach = nach,
                    Abfahrtszeit = abfahrt.ToString(@"hh\:mm"),
                    Ankunftszeit = ankunft.ToString(@"hh\:mm"),
                    Zugnummer = $"{typ} {nummer}", ZugFarbeStr = farbe,
                    Umstiege = umstiege,
                    Dauer = $"{dauerMin / 60}h {dauerMin % 60}min",
                    Preis = basisPreis + rnd.NextDouble() * 10,
                    PuenktlichkeitText = pText,
                    PuenktlichkeitFarbe = pFarbe,
                    Haltestellen = GeneriereHaltestellen(von, nach, abfahrt, ankunft, umstiege, rnd)
                });

                aktuelleZeit = aktuelleZeit.Add(TimeSpan.FromMinutes(rnd.Next(20, 60)));
            }
            return result;
        }

        private static List<Haltestelle> GeneriereHaltestellen(
            string von, string nach, TimeSpan abfahrt, TimeSpan ankunft, int umstiege, Random rnd)
        {
            var list = new List<Haltestelle>();
            var zwischen = new[] { "Augsburg Hbf", "Ingolstadt", "Nürnberg Hbf", "Erfurt Hbf",
                "Halle(Saale)", "Leipzig Hbf", "Bitterfeld", "Dessau Hbf" };
            var dauer = ankunft - abfahrt;
            var gewählt = zwischen.OrderBy(_ => rnd.Next()).Take(umstiege + 2).ToArray();

            list.Add(new Haltestelle
            {
                Name = von, Uhrzeit = abfahrt.ToString(@"hh\:mm"),
                PunktFarbe = Color.FromRgb(31, 111, 235),
                TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243))
            });

            for (int i = 0; i < gewählt.Length; i++)
            {
                var z = abfahrt.Add(TimeSpan.FromMinutes(dauer.TotalMinutes * (i + 1.0) / (gewählt.Length + 1)));
                list.Add(new Haltestelle
                {
                    Name = gewählt[i], Uhrzeit = z.ToString(@"hh\:mm"),
                    PunktFarbe = Color.FromRgb(48, 54, 61),
                    TextFarbe = new SolidColorBrush(Color.FromRgb(139, 148, 158))
                });
            }

            list.Add(new Haltestelle
            {
                Name = nach, Uhrzeit = ankunft.ToString(@"hh\:mm"),
                PunktFarbe = Color.FromRgb(46, 160, 67),
                TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243))
            });

            return list;
        }
    }
}
