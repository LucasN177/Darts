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
| **Killer** | Ab 2 Spielern: eigene Zahl, als Killer die Doppel der Gegner treffen, einstellbare Leben |
| **Halve-It** | 9 feste Ziele (15, 16, D, 17, 18, T, 19, 20, Bull), kein Treffer halbiert die Punkte |
| **Baseball** | 9 Innings, Inning n = Zahl n, Single/Double/Triple = 1/2/3 Runs |
| **Gotcha** | Von 0 genau aufs Ziel (301), gleicher Stand schickt den Gegner auf 0 zurück |

Alle Modi lassen sich allein oder mit bis zu 8 Spielern an einem Gerät spielen – auch gemischt mit
**Computergegnern** in sechs Stufen (Anfänger Ø ~25 bis Weltklasse Ø ~100). Der Computer wirft auf einer
simulierten Scheibe (echte Feldgeometrie, normalverteilte Streuung je Stufe) und zielt je Modus mit passender
Taktik: Checkout-Wege in X01/121, Schließen und Punkten in Cricket, Doppel der Gegner bei Killer usw.
Die Stufen sind per Test auf ihren Average kalibriert (`tests/DartsApp.Core.Tests/BotTests.cs`).

Weitere Funktionen: Dart-für-Dart-Eingabe mit Undo über Aufnahmen und Legs hinweg, laufendes Spiel übersteht
Neuladen, Verlauf mit Aufnahme-Details, Statistik pro Spieler und Modus (Average, First 9, Checkout-Quote,
180er, MPR, Trefferquoten, Verlaufsdiagramm), Spielerverwaltung, Hell-/Dunkelmodus, PWA.

## Projektstruktur

```
src/DartsApp.Core/        Spiellogik (Engines je Modus, Checkout-Rechner, Statistik) – ohne UI-Abhängigkeiten
src/DartsApp/             Blazor-WASM-App (MudBlazor), Supabase-Anbindung über REST (Auth + PostgREST)
tests/DartsApp.Core.Tests Unit-Tests der Spiellogik
database/                 SQL-Skripte aller Tabellen inkl. Row Level Security (je Tabelle ein Skript)
publish.ps1               Test + Build für GitHub Pages (nach ./site)
.github/workflows/        Build & Deploy per GitHub Actions
```

Alle Spielmodi erben von `GameEngine`. Ein Spiel ist vollständig durch Einstellungen, Spieler und die Liste der
geworfenen Darts beschrieben – Undo und das Wiederherstellen gespeicherter Spiele spielen diese Liste einfach neu ab.

## Entwicklung

```bash
dotnet run --project src/DartsApp
```

```bash
dotnet test
```

## Deployment (GitHub Actions → GitHub Pages)

Der Workflow [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) läuft bei jedem Push und Pull Request:
Er führt die Tests aus und baut die App mit `publish.ps1` für den Pfad `/<Repository-Name>/`. Bei Pushes auf `main`
wird das Ergebnis auf GitHub Pages deployt. Der Build liegt nicht mehr im Repository.

Einmalig nötig: **Settings → Pages → Build and deployment → Source: „GitHub Actions"**.

Lokal denselben Build erzeugen (Ausgabe in `./site`, nicht eingecheckt):

```bash
powershell -ExecutionPolicy Bypass -File ./publish.ps1 -BasePath /Darts/
```
