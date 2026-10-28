-- 03 · darts_games
-- Ein abgeschlossenes Spiel. "darts" enthält alle Würfe ("T20", "D16", ...) in Wurfreihenfolge,
-- damit die App das Spiel Aufnahme für Aufnahme nachspielen kann.
-- Idempotent.

create table if not exists public.darts_games (
    id          uuid primary key default gen_random_uuid(),
    owner_id    uuid        not null default auth.uid() references auth.users (id) on delete cascade,
    mode        text        not null, -- X01, Cricket, AroundTheClock, Shanghai, Bobs27, Checkout121, HighScore
    settings    jsonb       not null default '{}'::jsonb,
    players     jsonb       not null default '[]'::jsonb, -- Sitzreihenfolge [{id, name}]
    darts       jsonb       not null default '[]'::jsonb,
    winner_name text,
    started_at  timestamptz not null default now(),
    finished_at timestamptz,
    created_at  timestamptz not null default now()
);

create index if not exists darts_games_owner_finished_idx on public.darts_games (owner_id, finished_at desc);

-- Row Level Security: jeder sieht und ändert nur seine eigenen Spiele
alter table public.darts_games enable row level security;

drop policy if exists "darts_games_own" on public.darts_games;
create policy "darts_games_own" on public.darts_games
    for all to authenticated
    using (owner_id = (select auth.uid()))
    with check (owner_id = (select auth.uid()));

grant select, insert, update, delete on public.darts_games to authenticated;
