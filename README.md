# 🚂 Fahrplanauskunft – WPF Anwendung

Eine moderne C# WPF Desktop-App zur Fahrplanauskunft im GitHub-Dark-Design.

## Voraussetzungen

- **Windows** (WPF ist Windows-only)
- **.NET 8.0 SDK** → https://dotnet.microsoft.com/download/dotnet/8.0
- **Visual Studio 2022** oder **VS Code** mit C#-Erweiterung

## Starten

### Option 1 – Kommandozeile
```bash
cd FahrplanAuskunft
dotnet run
```

### Option 2 – Visual Studio
1. `FahrplanAuskunft.csproj` öffnen
2. F5 drücken

## Features

- **Verbindungssuche** mit Start, Ziel, Datum und Uhrzeit
- **Stations-Tausch** (⇄ Button)
- **6 Demo-Verbindungen** pro Suche (ICE, IC, RE, RB)
- **Detailansicht** mit Reiseverlauf und allen Zwischenhalten
- **Filter** nach Zugtyp oder Direktverbindung
- **Preisanzeige** (Super Sparpreis, Sparpreis, Flexpreis)
- **Live-Uhr** oben rechts
- **Pünktlichkeitsanzeige** (grün/gelb/rot)

## Hinweis

Die Verbindungen werden als Demo-Daten generiert.
Für echte Daten kann die Deutsche Bahn API (DB Rest)
integriert werden: https://v6.db.transport.rest/
