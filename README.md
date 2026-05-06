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

## Architektur
Das Projekt folgt dem **MVVM-Pattern** (Model-View-ViewModel) mit einer klaren Trennung der Schichten:

- **Models** – Datenmodelle (`Verbindung`, `Haltestelle`)
- **Services** – API-Kommunikation und Business Logik (`DbApiService`)
- **ViewModels** – Zustandsverwaltung und Präsentationslogik (`MainViewModel`)
- **Views** – UI-Schicht (`MainWindow.xaml`)
