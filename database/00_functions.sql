-- 00 · Hilfsfunktionen
-- Wird von den Tabellen-Skripten verwendet und muss zuerst ausgeführt werden.
-- Idempotent: kann beliebig oft ausgeführt werden.

-- Setzt updated_at bei jedem UPDATE auf den aktuellen Zeitpunkt.
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
