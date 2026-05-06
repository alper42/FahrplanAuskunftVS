# Fahrplanauskunft

Eine Windows-Desktopanwendung zur Abfrage von Zugverbindungen, entwickelt mit **C# und WPF**.

## Features
- Echtzeit-Verbindungssuche über die offizielle DB-API
- Autocomplete für Bahnhofsnamen
- Detailansicht mit Reiseverlauf und Haltestellen
- Preisübersicht (Flexpreis, Sparpreis, Super Sparpreis)
- Verbindungsfilter nach Zugtyp und Umstiegen
- Automatischer Fallback auf Demo-Daten bei fehlender Verbindung
- Dark Mode UI

## Technologien
| Technologie | Verwendung |
|---|---|
| C# / WPF | Desktop-Anwendung (.NET 6) |
| XAML | UI-Design mit Custom Styles |
| DB REST API | Echtzeit-Fahrplandaten |
| HttpClient | API-Kommunikation |
| MVVM-Pattern | Datenbindung via INotifyPropertyChanged |

