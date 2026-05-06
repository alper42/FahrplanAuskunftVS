using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace FahrplanAuskunft.Models
{
    public class Verbindung : INotifyPropertyChanged
    {
        public string Von { get; set; } = "";
        public string Nach { get; set; } = "";
        public string Abfahrtszeit { get; set; } = "";
        public string Ankunftszeit { get; set; } = "";
        public string Zugnummer { get; set; } = "";
        public string ZugFarbeStr { get; set; } = "#1F6FEB";
        public int Umstiege { get; set; }
        public string Dauer { get; set; } = "";
        public double Preis { get; set; }
        public string PuenktlichkeitText { get; set; } = "";
        public Color PuenktlichkeitFarbe { get; set; }
        public List<Haltestelle> Haltestellen { get; set; } = new();

        public string UmstiegeText => Umstiege == 0
            ? "Direktverbindung"
            : Umstiege == 1 ? "1 Umstieg" : $"{Umstiege} Umstiege";

        public Color ZugFarbe =>
            (Color)ColorConverter.ConvertFromString(ZugFarbeStr);

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
