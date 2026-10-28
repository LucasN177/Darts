# Datenbank (Supabase / PostgreSQL)

Alle Tabellen der App, mit Indizes, Row Level Security und Rechten. Jedes Skript ist idempotent und kann
gefahrlos erneut ausgeführt werden. Alle Objekte haben das Präfix `darts_`, damit andere Tabellen im selben
Supabase-Projekt nicht berührt werden.

| Reihenfolge | Skript | Inhalt |
|---|---|---|
| 1 | [`00_functions.sql`](00_functions.sql) | Hilfsfunktion `darts_touch_updated_at()` |
| 2 | [`01_darts_profiles.sql`](01_darts_profiles.sql) | Profil je Benutzer (1:1 zu `auth.users`) |
| 3 | [`02_darts_players.sql`](02_darts_players.sql) | Spielerliste je Benutzer |
| 4 | [`03_darts_games.sql`](03_darts_games.sql) | Spiele inkl. Einstellungen und aller Würfe |
| 5 | [`04_darts_game_players.sql`](04_darts_game_players.sql) | Ergebnis je Spieler und Spiel (Statistik) |
| – | [`install_all.psql`](install_all.psql) | Führt 00–04 nacheinander aus (nur psql) |
| – | [`drop_all.sql`](drop_all.sql) | **Löscht** alle Darts-Tabellen samt Daten |

## Struktur anlegen oder wiederherstellen

**Supabase-Dashboard:** *SQL Editor → New query*, dann die Skripte `00` bis `04` nacheinander einfügen und
ausführen.

**psql** (Connection String unter *Project Settings → Database*):

```bash
psql "postgresql://postgres:<passwort>@db.<projekt>.supabase.co:5432/postgres" -v ON_ERROR_STOP=1 -f database/install_all.psql
```

Komplett neu aufsetzen: zuerst `drop_all.sql`, danach wie oben installieren. Dabei gehen alle gespeicherten
Spiele, Spieler und Profile verloren; Benutzerkonten bleiben bestehen.

## Hinweise für Änderungen

- Neue Spalten mit `alter table ... add column if not exists ...` im passenden Tabellen-Skript ergänzen, damit
  die Skripte idempotent bleiben.
- Spaltennamen sind snake_case und müssen zu den Modellen in
  [`src/DartsApp/Services/Repositories.cs`](../src/DartsApp/Services/Repositories.cs) passen.
- Die App benötigt die Supabase-Rolle `authenticated` und die Funktion `auth.uid()`; beides gibt es in jedem
  Supabase-Projekt.
