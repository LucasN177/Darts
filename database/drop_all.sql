-- ACHTUNG: Löscht alle Darts-Tabellen inklusive aller Spiele, Spieler und Profile unwiderruflich!
-- Nur verwenden, um die Struktur komplett neu aufzusetzen; danach 00 bis 04 erneut ausführen.
-- Benutzerkonten (auth.users) und andere Tabellen im Projekt bleiben unberührt.

drop table if exists public.darts_game_players;
drop table if exists public.darts_games;
drop table if exists public.darts_players;
drop table if exists public.darts_profiles;
drop function if exists public.darts_touch_updated_at();

notify pgrst, 'reload schema';
