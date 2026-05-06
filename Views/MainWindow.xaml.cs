using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FahrplanAuskunft.Models;
using FahrplanAuskunft.ViewModels;

namespace FahrplanAuskunft.Views  
{
    public partial class MainWindow : Window  
    {
        private ObservableCollection<Verbindung> _alleVerbindungen = new();
        private DispatcherTimer _clockTimer = null!;
        private static readonly HttpClient _http = new HttpClient();
        private DispatcherTimer _vonTimer = null!;
        private DispatcherTimer _nachTimer = null!;

        public MainWindow()
        {
            InitializeComponent();
            DatumPicker.SelectedDate = DateTime.Today;
            TxtZeit.Text = DateTime.Now.ToString("HH:mm");
            StartClock();
            SetupAutocomplete(TxtVon, PopupVon, ListVon, ref _vonTimer);
            SetupAutocomplete(TxtNach, PopupNach, ListNach, ref _nachTimer);
        }

        private void StartClock()
        {
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
        }

        private void UpdateClock()
        {
            CurrentTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            CurrentDateText.Text = DateTime.Now.ToString("dddd, dd. MMMM yyyy",
                new System.Globalization.CultureInfo("de-DE"));
        }

        private void SetupAutocomplete(TextBox txt, System.Windows.Controls.Primitives.Popup popup,
            ListBox list, ref DispatcherTimer timer)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer = t;

            txt.TextChanged += (s, e) =>
            {
                t.Stop();
                var q = txt.Text.Trim();
                if (q.Length < 2) { popup.IsOpen = false; return; }
                t.Start();
            };

            t.Tick += async (s, e) =>
            {
                t.Stop();
                var q = txt.Text.Trim();
                if (q.Length < 2) return;
                try
                {
                    var url = $"https://v6.db.transport.rest/locations?query={Uri.EscapeDataString(q)}&results=8&stops=true&addresses=false&poi=false";
                    var resp = await _http.GetStringAsync(url);
                    var doc = JsonDocument.Parse(resp);
                    var namen = new List<string>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var n))
                            namen.Add(n.GetString() ?? "");
                    }
                    list.ItemsSource = namen.Where(x => !string.IsNullOrEmpty(x)).ToList();
                    popup.IsOpen = namen.Any();
                }
                catch { popup.IsOpen = false; }
            };

            txt.LostFocus += (s, e) =>
            {
                System.Threading.Tasks.Task.Delay(200)
                    .ContinueWith(_ => Dispatcher.Invoke(() => popup.IsOpen = false));
            };

            list.SelectionChanged += (s, e) =>
            {
                if (list.SelectedItem is string selected)
                {
                    txt.Text = selected;
                    txt.CaretIndex = selected.Length;
                    popup.IsOpen = false;
                    list.SelectedItem = null;
                }
            };
        }

        private void SwapStations_Click(object sender, RoutedEventArgs e)
        {
            (TxtVon.Text, TxtNach.Text) = (TxtNach.Text, TxtVon.Text);
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtVon.Text) || string.IsNullOrWhiteSpace(TxtNach.Text))
            {
                MessageBox.Show("Bitte geben Sie Start- und Zielbahnhof ein.", "Fehlende Eingabe",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SuchenBtn.IsEnabled = false;
            SuchenBtn.Content = "Suche läuft...";
            EmptyState.Visibility = Visibility.Visible;
            ResultsScroll.Visibility = Visibility.Collapsed;

            var datum = DatumPicker.SelectedDate ?? DateTime.Today;
            if (!TimeSpan.TryParse(TxtZeit.Text.Trim(), out var zeit))
                zeit = TimeSpan.FromHours(8);

            var von = TxtVon.Text.Trim();
            var nach = TxtNach.Text.Trim();

            try
            {
                _alleVerbindungen = await LadeEchteVerbindungenAsync(von, nach, datum, zeit);
            }
            catch
            {
                _alleVerbindungen = GeneriereDemoVerbindungen(von, nach, datum, zeit);
            }

            ZeigeVerbindungen(_alleVerbindungen);
            ResultHeaderText.Text = $"{_alleVerbindungen.Count} Verbindungen  ·  {von} → {nach}";
            EmptyState.Visibility = Visibility.Collapsed;
            ResultsScroll.Visibility = Visibility.Visible;

            SuchenBtn.IsEnabled = true;
            SuchenBtn.Content = "Verbindungen suchen";
        }

        private async System.Threading.Tasks.Task<ObservableCollection<Verbindung>> LadeEchteVerbindungenAsync(
            string von, string nach, DateTime datum, TimeSpan zeit)
        {
            var result = new ObservableCollection<Verbindung>();
            var vonId = await SucheStationIdAsync(von);
            var nachId = await SucheStationIdAsync(nach);
            if (vonId == null || nachId == null) throw new Exception("Station nicht gefunden");

            var dt = datum.Date.Add(zeit);
            var iso = dt.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var url = $"https://v6.db.transport.rest/journeys?from={vonId}&to={nachId}&departure={Uri.EscapeDataString(iso)}&results=6&language=de";

            var resp = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(resp);
            var journeys = doc.RootElement.GetProperty("journeys");

            foreach (var journey in journeys.EnumerateArray())
            {
                var legs = journey.GetProperty("legs");
                var firstLeg = legs[0];
                var lastLeg = legs[legs.GetArrayLength() - 1];

                var depTime = firstLeg.GetProperty("departure").GetString() ?? "";
                var arrTime = lastLeg.GetProperty("arrival").GetString() ?? "";
                DateTime dep = DateTime.Parse(depTime);
                DateTime arr = DateTime.Parse(arrTime);
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
                        "national" => "#C62300",
                        "regionalExpress" => "#0044BB",
                        "regional" => "#005A8E",
                        "suburban" => "#008D4F",
                        _ => "#1F6FEB"
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
                if (halte.Any()) { halte[0].PunktFarbe = Color.FromRgb(31, 111, 235); halte[0].TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243)); }

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
                    Von = von,
                    Nach = nach,
                    Abfahrtszeit = dep.ToString("HH:mm"),
                    Ankunftszeit = arr.ToString("HH:mm"),
                    Zugnummer = zugNr,
                    ZugFarbeStr = zugFarbe,
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

        private async System.Threading.Tasks.Task<string?> SucheStationIdAsync(string name)
        {
            var url = $"https://v6.db.transport.rest/locations?query={Uri.EscapeDataString(name)}&results=1";
            var resp = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(resp);
            if (doc.RootElement.GetArrayLength() > 0)
                return doc.RootElement[0].GetProperty("id").GetString();
            return null;
        }

        private void ZeigeVerbindungen(IEnumerable<Verbindung> verbindungen)
        {
            ResultsList.ItemsSource = verbindungen.ToList();
            EmptyDetail.Visibility = Visibility.Visible;
            DetailScroll.Visibility = Visibility.Collapsed;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_alleVerbindungen == null || !_alleVerbindungen.Any()) return;
            var filter = (FilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Alle";
            var gefiltert = filter switch
            {
                "Nur ICE/IC" => _alleVerbindungen.Where(v => v.Zugnummer.StartsWith("ICE") || v.Zugnummer.StartsWith("IC")),
                "Nur RE/RB" => _alleVerbindungen.Where(v => v.Zugnummer.StartsWith("RE") || v.Zugnummer.StartsWith("RB")),
                "Umstiegsfrei" => _alleVerbindungen.Where(v => v.Umstiege == 0),
                _ => _alleVerbindungen.AsEnumerable()
            };
            ZeigeVerbindungen(gefiltert);
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultsList.SelectedItem is not Verbindung v) return;
            ZeigeDetail(v);
        }

        private void ZeigeDetail(Verbindung v)
        {
            DetailZug.Text = $"{v.Zugnummer}  ·  {v.Umstiege} Umstiege";
            DetailDauer.Text = v.Dauer;
            DetailVon.Text = $"{v.Von}  {v.Abfahrtszeit}";
            DetailNach.Text = $"{v.Nach}  {v.Ankunftszeit}";
            HaltestellenList.ItemsSource = v.Haltestellen;
            DetailPreis1.Text = $"{v.Preis * 2.1:F2} €";
            DetailPreis2.Text = $"{v.Preis * 1.2:F2} €";
            DetailPreis3.Text = $"{v.Preis:F2} €";
            EmptyDetail.Visibility = Visibility.Collapsed;
            DetailScroll.Visibility = Visibility.Visible;
        }

        private static ObservableCollection<Verbindung> GeneriereDemoVerbindungen(
            string von, string nach, DateTime datum, TimeSpan startZeit)
        {
            var rnd = new Random(von.GetHashCode() ^ nach.GetHashCode() ^ datum.DayOfYear);
            var result = new ObservableCollection<Verbindung>();
            var zugTypen = new[] {
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
                    _ => ($"+{puenktlichkeit} Min", Color.FromRgb(248, 81, 73))
                };
                var abfahrt = aktuelleZeit;
                var ankunft = aktuelleZeit.Add(TimeSpan.FromMinutes(dauerMin));
                result.Add(new Verbindung
                {
                    Von = von,
                    Nach = nach,
                    Abfahrtszeit = abfahrt.ToString(@"hh\:mm"),
                    Ankunftszeit = ankunft.ToString(@"hh\:mm"),
                    Zugnummer = $"{typ} {nummer}",
                    ZugFarbeStr = farbe,
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
                Name = von,
                Uhrzeit = abfahrt.ToString(@"hh\:mm"),
                PunktFarbe = Color.FromRgb(31, 111, 235),
                TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243))
            });
            for (int i = 0; i < gewählt.Length; i++)
            {
                var z = abfahrt.Add(TimeSpan.FromMinutes(dauer.TotalMinutes * (i + 1.0) / (gewählt.Length + 1)));
                list.Add(new Haltestelle
                {
                    Name = gewählt[i],
                    Uhrzeit = z.ToString(@"hh\:mm"),
                    PunktFarbe = Color.FromRgb(48, 54, 61),
                    TextFarbe = new SolidColorBrush(Color.FromRgb(139, 148, 158))
                });
            }
            list.Add(new Haltestelle
            {
                Name = nach,
                Uhrzeit = ankunft.ToString(@"hh\:mm"),
                PunktFarbe = Color.FromRgb(46, 160, 67),
                TextFarbe = new SolidColorBrush(Color.FromRgb(230, 237, 243))
            });
            return list;
        }
    }
}