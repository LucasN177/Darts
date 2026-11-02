# Darts

Blazor-WebAssembly-App (MudBlazor) zum Zählen und Trainieren von Darts. Spiele, Spieler und Statistiken
werden mit Anmeldung in Supabase gespeichert, ohne Anmeldung lokal im Browser.

## Spielmodi

### Spiele

| Modus | Beschreibung |
|---|---|
| **X01** | 101/301/501/701/1001 oder eigener Startwert, Single/Double/Master In und Out, Legs und Sets, Checkout-Vorschläge |
| **Cricket** | Standard (15–20), Tactics (10–20), Random (6 zufällige Zahlen) oder Mickey Mouse (12–20, Doppel, Triple), jeweils mit Bull; Standard oder Cut Throat, optionales Rundenlimit |
| **Shanghai** | Runde = Zahl, Shanghai (S+D+T) gewinnt sofort |
| **Killer** | Ab 2 Spielern: eigene Zahl, als Killer die Doppel der Gegner treffen, einstellbare Leben |
| **Halve-It** | Klassisch (15, 16, D, 17, 18, T, 19, 20, Bull) oder Bermuda Triangle (13 Runden); kein Treffer halbiert die Punkte |
| **Baseball** | 9 Innings, Inning n = Zahl n, Single/Double/Triple = 1/2/3 Runs |
| **Gotcha** | Von 0 genau aufs Ziel (301), gleicher Stand schickt den Gegner auf 0 zurück |
| **Golf** | 9 oder 18 Löcher, Triple/Double/Single = 1/2/3 Schläge, sonst 5; der letzte Treffer zählt, wenigste Schläge gewinnen |
| **Scram** | Genau 2 Spieler: Stopper schließt Felder, Scorer punktet auf offenen Feldern, danach Rollentausch |
| **Tic-Tac-Toe** | Genau 2 Spieler: 3×3-Raster aus Zahlen und Bull, 3 Marks belegen ein Feld, drei in einer Reihe gewinnen |
| **Football** | Bull = Ballbesitz, mit Ball zählt jedes Doppel als Tor, erste Mannschaft mit 10 Toren gewinnt |
| **Hare and Hounds** | Ab 2 Spielern: der Hase läuft ab der 20 einmal ums Board, die Hunde starten zwei Felder dahinter und jagen ihn |
| **Nine Lives** | 1–20 der Reihe nach, jede Aufnahme ohne Treffer kostet eines von neun Leben |
| **All Fives** | Nur durch 5 teilbare Aufnahmen zählen (Summe ÷ 5), genau 51 Punkte gewinnen |
| **Prisoner** | 1–20 der Reihe nach, Fehlwürfe werden Gefangene; wer sie befreit, rückt vor, der Besitzer fällt zurück |

### Training

| Modus | Beschreibung |
|---|---|
| **Around the Clock** | 1–20 (+ Bull), alle Felder / nur Doppel / nur Triple, optional überspringen per Multiplikator |
| **Bob's 27** | Doppeltraining D1–D20 + Bull, optional mit Ausscheiden |
| **121 Checkout** | Checkout-Training ab 121, einstellbare Darts pro Versuch und Fehlregel |
| **Checkout Roulette** | Zufällige, auscheckbare Ziele in einem einstellbaren Bereich (für alle Spieler dieselben) |
| **Catch 40** | Checkouts 61–100 mit je 6 Darts, 2/3/4–6 Darts = 3/2/1 Punkte |
| **JDC Challenge** | Shanghai 10–15, alle Doppel und Bull, Shanghai 15–20 |
| **High Score** | Feste Rundenzahl, höchste Punktzahl gewinnt |
| **Scoring-Drill** | Viele Darts auf ein Feld (z.B. T20), Trefferquote im Feld und im Segment |
| **Bull-Training** | Darts auf das Bull, 25 = 1 Punkt, Bullseye = 2 Punkte |
| **Doubles Ladder** | D1 bis D20 und Bull mit je drei Darts, Trefferquote pro Doppel |
| **Big Finish** | Zufällige Checkouts von 100 bis 170 |
| **Sudden Death** | X01 mit Aufnahmelimit pro Leg (Standard 15); danach gewinnt der kleinste Rest |

**Teams:** In jedem Modus lassen sich Teams aus 2–4 Spielern bilden (auch gemischt mit Computergegnern); ein Team
spielt auf einen gemeinsamen Spielstand, die Mitglieder werfen abwechselnd.
**Handicap:** In X01 und Sudden Death kann jeder Spieler einen eigenen Startwert bekommen (z.B. 501 gegen 301).
**Schnelleingabe (Fast Mode):** In X01 (Single In), Sudden Death, High Score, All Fives und den Checkout-Trainings
lässt sich statt jedes Darts nur die Summe der Aufnahme eingeben; beim Checkout fragt die App nach der Dartanzahl.
Intern wird die Summe in eine passende Dart-Kombination umgerechnet (`FastInput`), die Spiellogik bleibt dart-basiert.

Alle Modi lassen sich allein oder mit bis zu 8 Spielern an einem Gerät spielen (Scram und Tic-Tac-Toe mit genau zwei) – auch gemischt mit
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
