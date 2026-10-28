-- 02 · darts_players
-- Spielerliste des Benutzers, inklusive ihm selbst (is_self = true, höchstens einmal je Benutzer).
-- Idempotent.

create table if not exists public.darts_players (
    id         uuid primary key default gen_random_uuid(),
    owner_id   uuid        not null default auth.uid() references auth.users (id) on delete cascade,
    name       text        not null check (char_length(name) between 1 and 40),
    is_self    boolean     not null default false,
    created_at timestamptz not null default now()
);

create index if not exists darts_players_owner_idx on public.darts_players (owner_id);
create unique index if not exists darts_players_one_self_idx on public.darts_players (owner_id) where is_self;

-- Row Level Security: jeder sieht und ändert nur seine eigenen Spieler
alter table public.darts_players enable row level security;

drop policy if exists "darts_players_own" on public.darts_players;
create policy "darts_players_own" on public.darts_players
    for all to authenticated
    using (owner_id = (select auth.uid()))
    with check (owner_id = (select auth.uid()));

grant select, insert, update, delete on public.darts_players to authenticated;
