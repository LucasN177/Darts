-- 04 · darts_game_players
-- Ergebnis je Spieler und Spiel – Grundlage der Statistiken.
-- Benötigt: 02_darts_players.sql, 03_darts_games.sql. Idempotent.

create table if not exists public.darts_game_players (
    id                uuid primary key default gen_random_uuid(),
    game_id           uuid          not null references public.darts_games (id) on delete cascade,
    owner_id          uuid          not null default auth.uid() references auth.users (id) on delete cascade,
    player_id         uuid          references public.darts_players (id) on delete set null, -- null = Gast
    player_name       text          not null,
    position          int           not null, -- Sitzplatz (0-basiert)
    rank              int           not null default 0,
    is_winner         boolean       not null default false,
    darts_thrown      int           not null default 0,
    score             int           not null default 0, -- modusabhängiger Hauptwert
    average           numeric(7, 2) not null default 0, -- 3-Dart-Average bzw. MPR (Cricket)
    first9_average    numeric(7, 2),
    highest_turn      int           not null default 0,
    highest_checkout  int           not null default 0,
    checkout_attempts int           not null default 0,
    checkouts_hit     int           not null default 0,
    count_180         int           not null default 0,
    count_140         int           not null default 0,
    count_100         int           not null default 0,
    legs_won          int           not null default 0,
    sets_won          int           not null default 0,
    extra             jsonb         not null default '{}'::jsonb -- z.B. mpr, hit_rate, highest_target
);

create index if not exists darts_game_players_game_idx on public.darts_game_players (game_id);
create index if not exists darts_game_players_owner_player_idx on public.darts_game_players (owner_id, player_id);

-- Row Level Security: nur eigene Ergebnisse, und nur zu eigenen Spielen
alter table public.darts_game_players enable row level security;

drop policy if exists "darts_game_players_own" on public.darts_game_players;
create policy "darts_game_players_own" on public.darts_game_players
    for all to authenticated
    using (owner_id = (select auth.uid()))
    with check (
        owner_id = (select auth.uid())
        and exists (select 1 from public.darts_games g where g.id = game_id and g.owner_id = (select auth.uid()))
    );

grant select, insert, update, delete on public.darts_game_players to authenticated;

-- PostgREST Schema-Cache neu laden, damit die Tabellen sofort per API verfügbar sind
notify pgrst, 'reload schema';
