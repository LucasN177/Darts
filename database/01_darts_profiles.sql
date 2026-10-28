-- 01 · darts_profiles
-- Profil je Benutzer (1:1 zu auth.users). Wird von der App beim ersten Login angelegt.
-- Benötigt: 00_functions.sql. Idempotent.

create table if not exists public.darts_profiles (
    id           uuid primary key references auth.users (id) on delete cascade,
    display_name text        not null default '',
    first_name   text,
    last_name    text,
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);

drop trigger if exists darts_profiles_touch on public.darts_profiles;
create trigger darts_profiles_touch
    before update on public.darts_profiles
    for each row execute function public.darts_touch_updated_at();

-- Row Level Security: jeder sieht und ändert nur sein eigenes Profil
alter table public.darts_profiles enable row level security;

drop policy if exists "darts_profiles_own" on public.darts_profiles;
create policy "darts_profiles_own" on public.darts_profiles
    for all to authenticated
    using (id = (select auth.uid()))
    with check (id = (select auth.uid()));

grant select, insert, update, delete on public.darts_profiles to authenticated;
