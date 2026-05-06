using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FahrplanAuskunft.Models;
using FahrplanAuskunft.Services;

namespace FahrplanAuskunft.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DbApiService _apiService = new();

        private ObservableCollection<Verbindung> _alleVerbindungen = new();
        private ObservableCollection<Verbindung> _angezeigteVerbindungen = new();
        private Verbindung? _ausgewählteVerbindung;
        private string _vonText = "München Hbf";
        private string _nachText = "Berlin Hbf";
        private DateTime _datum = DateTime.Today;
        private string _zeitText = DateTime.Now.ToString("HH:mm");
        private string _filterTyp = "Alle";
        private bool _isLoading;
        private string _resultHeader = "Verbindungen";
        private bool _showResults;

        public string VonText
        {
            get => _vonText;
            set { _vonText = value; OnPropertyChanged(); }
        }

        public string NachText
        {
            get => _nachText;
            set { _nachText = value; OnPropertyChanged(); }
        }

        public DateTime Datum
        {
            get => _datum;
            set { _datum = value; OnPropertyChanged(); }
        }

        public string ZeitText
        {
            get => _zeitText;
            set { _zeitText = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string ResultHeader
        {
            get => _resultHeader;
            set { _resultHeader = value; OnPropertyChanged(); }
        }

        public bool ShowResults
        {
            get => _showResults;
            set { _showResults = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Verbindung> AngezeigteVerbindungen
        {
            get => _angezeigteVerbindungen;
            set { _angezeigteVerbindungen = value; OnPropertyChanged(); }
        }

        public Verbindung? AusgewählteVerbindung
        {
            get => _ausgewählteVerbindung;
            set { _ausgewählteVerbindung = value; OnPropertyChanged(); }
        }

        public void SwapStations()
        {
            (VonText, NachText) = (NachText, VonText);
        }

        public async Task<List<string>> SucheStationenAsync(string query)
        {
            if (query.Length < 2) return new List<string>();
            try { return await _apiService.SucheStationenAsync(query); }
            catch { return new List<string>(); }
        }

        public async Task SucheVerbindungenAsync()
        {
            if (string.IsNullOrWhiteSpace(VonText) || string.IsNullOrWhiteSpace(NachText))
                return;

            IsLoading = true;
            ShowResults = false;

            if (!TimeSpan.TryParse(ZeitText.Trim(), out var zeit))
                zeit = TimeSpan.FromHours(8);

            try
            {
                _alleVerbindungen = await _apiService.LadeVerbindungenAsync(VonText, NachText, Datum, zeit);
            }
            catch
            {
                _alleVerbindungen = DbApiService.GeneriereDemoVerbindungen(VonText, NachText, Datum, zeit);
            }

            ApplyFilter(_filterTyp);
            ResultHeader = $"{_alleVerbindungen.Count} Verbindungen  ·  {VonText} → {NachText}";
            ShowResults = true;
            IsLoading = false;
        }

        public void ApplyFilter(string filter)
        {
            _filterTyp = filter;
            var gefiltert = filter switch
            {
                "Nur ICE/IC"  => _alleVerbindungen.Where(v => v.Zugnummer.StartsWith("ICE") || v.Zugnummer.StartsWith("IC")),
                "Nur RE/RB"   => _alleVerbindungen.Where(v => v.Zugnummer.StartsWith("RE") || v.Zugnummer.StartsWith("RB")),
                "Umstiegsfrei"=> _alleVerbindungen.Where(v => v.Umstiege == 0),
                _             => _alleVerbindungen.AsEnumerable()
            };
            AngezeigteVerbindungen = new ObservableCollection<Verbindung>(gefiltert);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
