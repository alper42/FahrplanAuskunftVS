using System.Windows.Media;

namespace FahrplanAuskunft.Models
{
    public class Haltestelle
    {
        public string Name { get; set; } = "";
        public string Uhrzeit { get; set; } = "";
        public Color PunktFarbe { get; set; }
        public SolidColorBrush TextFarbe { get; set; } = new(Colors.White);
    }
}
