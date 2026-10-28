-- Darts App – Supabase Schema
-- Einmalig im Supabase Dashboard unter "SQL Editor" ausführen.
-- Alle Tabellen haben das Präfix "darts_", damit sie sich nicht mit anderen Tabellen im Projekt beißen.
-- Das Skript ist idempotent und kann gefahrlos mehrfach ausgeführt werden.

-- ---------------------------------------------------------------------------
-- Profile (1:1 zu auth.users)
-- ---------------------------------------------------------------------------
create table if not exists public.darts_profiles (
    id           uuid primary key references auth.users (id) on delete cascade,
    display_name text        not null default '',
    first_name   text,
    last_name    text,
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Spielerliste des Benutzers (inkl. ihm selbst, is_self = true)
-- ---------------------------------------------------------------------------
create table if not exists public.darts_players (
    id         uuid primary key default gen_random_uuid(),
    owner_id   uuid        not null default auth.uid() references auth.users (id) on delete cascade,
    name       text        not null check (char_length(name) between 1 and 40),
    is_self    boolean     not null default false,
    created_at timestamptz not null default now()
);

create index if not exists darts_players_owner_idx on public.darts_players (owner_id);
create unique index if not exists darts_players_one_self_idx on public.darts_players (owner_id) where is_self;

-- ---------------------------------------------------------------------------
-- Spiele
-- ---------------------------------------------------------------------------
create table if not exists public.darts_games (
    id          uuid primary key default gen_random_uuid(),
    owner_id    uuid        not null default auth.uid() references auth.users (id) on delete cascade,
    mode        text        not null,
    settings    jsonb       not null default '{}'::jsonb,
    players     jsonb       not null default '[]'::jsonb, -- Sitzreihenfolge [{id, name}]
    darts       jsonb       not null default '[]'::jsonb, -- alle Würfe ("T20", "D16", ...) zum Nachspielen
    winner_name text,
    started_at  timestamptz not null default now(),
    finished_at timestamptz,
    created_at  timestamptz not null default now()
);

create index if not exists darts_games_owner_finished_idx on public.darts_games (owner_id, finished_at desc);

-- ---------------------------------------------------------------------------
-- Ergebnis pro Spieler und Spiel (für Statistiken)
-- ---------------------------------------------------------------------------
create table if not exists public.darts_game_players (
    id                uuid primary key default gen_random_uuid(),
    game_id           uuid         not null references public.darts_games (id) on delete cascade,
    owner_id          uuid         not null default auth.uid() references auth.users (id) on delete cascade,
    player_id         uuid         references public.darts_players (id) on delete set null,
    player_name       text         not null,
    position          int          not null,
    rank              int          not null default 0,
    is_winner         boolean      not null default false,
    darts_thrown      int          not null default 0,
    score             int          not null default 0,
    average           numeric(7, 2) not null default 0,
    first9_average    numeric(7, 2),
    highest_turn      int          not null default 0,
    highest_checkout  int          not null default 0,
    checkout_attempts int          not null default 0,
    checkouts_hit     int          not null default 0,
    count_180         int          not null default 0,
    count_140         int          not null default 0,
    count_100         int          not null default 0,
    legs_won          int          not null default 0,
    sets_won          int          not null default 0,
    extra             jsonb        not null default '{}'::jsonb
);

create index if not exists darts_game_players_game_idx on public.darts_game_players (game_id);
create index if not exists darts_game_players_owner_player_idx on public.darts_game_players (owner_id, player_id);

-- ---------------------------------------------------------------------------
-- Row Level Security: jeder sieht und ändert nur seine eigenen Daten
-- ---------------------------------------------------------------------------
alter table public.darts_profiles     enable row level security;
alter table public.darts_players      enable row level security;
alter table public.darts_games        enable row level security;
alter table public.darts_game_players enable row level security;

drop policy if exists "darts_profiles_own" on public.darts_profiles;
create policy "darts_profiles_own" on public.darts_profiles
    for all to authenticated
    using (id = (select auth.uid()))
    with check (id = (select auth.uid()));

drop policy if exists "darts_players_own" on public.darts_players;
create policy "darts_players_own" on public.darts_players
    for all to authenticated
    using (owner_id = (select auth.uid()))
    with check (owner_id = (select auth.uid()));

drop policy if exists "darts_games_own" on public.darts_games;
create policy "darts_games_own" on public.darts_games
    for all to authenticated
    using (owner_id = (select auth.uid()))
    with check (owner_id = (select auth.uid()));

drop policy if exists "darts_game_players_own" on public.darts_game_players;
create policy "darts_game_players_own" on public.darts_game_players
    for all to authenticated
    using (owner_id = (select auth.uid()))
    with check (
        owner_id = (select auth.uid())
        and exists (select 1 from public.darts_games g where g.id = game_id and g.owner_id = (select auth.uid()))
    );

grant select, insert, update, delete on
    public.darts_profiles, public.darts_players, public.darts_games, public.darts_game_players
    to authenticated;

-- updated_at für Profile pflegen
create or replace function public.darts_touch_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists darts_profiles_touch on public.darts_profiles;
create trigger darts_profiles_touch
    before update on public.darts_profiles
    for each row execute function public.darts_touch_updated_at();

-- PostgREST Schema-Cache neu laden, damit die Tabellen sofort per API verfügbar sind
notify pgrst, 'reload schema';
