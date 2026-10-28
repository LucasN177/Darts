# Darts

Blazor-WebAssembly-App (MudBlazor) zum Zählen und Trainieren von Darts. Spiele, Spieler und Statistiken
werden mit Anmeldung in Supabase gespeichert, ohne Anmeldung lokal im Browser.

## Spielmodi

| Modus | Beschreibung |
|---|---|
| **X01** | 101/301/501/701/1001 oder eigener Startwert, Single/Double/Master In und Out, Legs und Sets, Checkout-Vorschläge |
| **Cricket** | 15–20 und Bull, Standard oder Cut Throat, optionales Rundenlimit |
| **Around the Clock** | 1–20 (+ Bull), alle Felder / nur Doppel / nur Triple, optional überspringen per Multiplikator |
| **Shanghai** | Runde = Zahl, Shanghai (S+D+T) gewinnt sofort |
| **Bob's 27** | Doppeltraining D1–D20 + Bull, optional mit Ausscheiden |
| **121 Checkout** | Checkout-Training ab 121, einstellbare Darts pro Versuch und Fehlregel |
| **High Score** | Feste Rundenzahl, höchste Punktzahl gewinnt |

Weitere Funktionen: Dart-für-Dart-Eingabe mit Undo über Aufnahmen und Legs hinweg, laufendes Spiel übersteht
Neuladen, Verlauf mit Aufnahme-Details, Statistik pro Spieler und Modus (Average, First 9, Checkout-Quote,
180er, MPR, Trefferquoten, Verlaufsdiagramm), Spielerverwaltung, Hell-/Dunkelmodus, PWA.

## Projektstruktur

```
src/DartsApp.Core/        Spiellogik (Engines je Modus, Checkout-Rechner, Statistik) – ohne UI-Abhängigkeiten
src/DartsApp/             Blazor-WASM-App (MudBlazor), Supabase-Anbindung über REST (Auth + PostgREST)
tests/DartsApp.Core.Tests Unit-Tests der Spiellogik
supabase/schema.sql       Datenbankschema inkl. Row Level Security
publish.ps1               Build für GitHub Pages ins Repository-Root
```

Alle Spielmodi erben von `GameEngine`. Ein Spiel ist vollständig durch Einstellungen, Spieler und die Liste der
geworfenen Darts beschrieben – Undo und das Wiederherstellen gespeicherter Spiele spielen diese Liste einfach neu ab.

## Einrichtung Supabase

1. Im Supabase-Dashboard unter **SQL Editor** den Inhalt von [`supabase/schema.sql`](supabase/schema.sql) ausführen.
   Alle Tabellen haben das Präfix `darts_` und sind per Row Level Security auf den jeweiligen Benutzer beschränkt.
2. Unter **Authentication → URL Configuration** die Site-URL (z.B. `https://<user>.github.io/Darts/`) und
   `http://localhost:5211/` als Redirect-URL eintragen, damit Bestätigungs- und Passwort-Reset-Links funktionieren.
3. URL und Publishable Key stehen in [`src/DartsApp/wwwroot/appsettings.json`](src/DartsApp/wwwroot/appsettings.json).
   Der Publishable Key ist für den Browser gedacht; der Datenschutz erfolgt über die RLS-Policies.

## Entwicklung

```bash
dotnet run --project src/DartsApp
```

```bash
dotnet test
```

## Veröffentlichen (GitHub Pages)

GitHub Pages liefert das Repository-Root unter `/Darts/` aus. `publish.ps1` baut die App, setzt den Base-Pfad
und ersetzt den Build im Root:

```bash
pwsh ./publish.ps1
```

Danach committen und pushen.
