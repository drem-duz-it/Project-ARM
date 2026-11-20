-- ARM: sincronizzazione cifrata tra i dispositivi.
-- Il server conserva solo dati cifrati sul dispositivo (AES-GCM): non può leggerli.
-- Lo script si può eseguire più volte senza danni.

-- 1. Elementi sincronizzati (note, task, eventi, ordini, playlist, brani, collegamenti, eliminazioni)
create table if not exists public.records (
    user_id    uuid        not null default auth.uid() references auth.users (id) on delete cascade,
    key        text        not null,                  -- identificativo dell'elemento (scelto dall'app)
    version    bigint      not null,                  -- assegnata dal server, cresce a ogni scrittura
    deleted    boolean     not null default false,
    payload    text,                                  -- dati cifrati (base64)
    device_id  text,                                  -- dispositivo che ha scritto l'ultima versione
    updated_at timestamptz not null default now(),
    primary key (user_id, key)
);

create sequence if not exists public.records_version_seq;
create index if not exists records_user_version on public.records (user_id, version);

alter table public.records enable row level security;

-- Lettura: solo le proprie righe. Scrittura: solo tramite push_records (controlla i conflitti).
drop policy if exists "records: lettura proprie" on public.records;
create policy "records: lettura proprie" on public.records
    for select to authenticated
    using (user_id = auth.uid());

revoke insert, update, delete on public.records from anon, authenticated;

-- 2. Scrittura a blocchi con controllo dei conflitti.
-- items: [{ "key": "...", "base": 12 | null, "payload": "...", "deleted": false, "device": "..." }]
-- "base" è la versione da cui il dispositivo è partito (null = elemento nuovo per il dispositivo).
-- Se sul server c'è una versione diversa, la scrittura è rifiutata (ok = false) e l'app chiede quale tenere.
create or replace function public.push_records(items jsonb)
returns table (key text, ok boolean, version bigint)
language plpgsql
security definer
set search_path = public
as $$
#variable_conflict use_column
declare
    uid  uuid := auth.uid();
    item jsonb;
    k    text;
    base bigint;
    cur  bigint;
    nv   bigint;
begin
    if uid is null then
        raise exception 'non autenticato';
    end if;

    for item in select * from jsonb_array_elements(items)
    loop
        k    := item ->> 'key';
        base := (item ->> 'base')::bigint;

        select r.version into cur
        from public.records r
        where r.user_id = uid and r.key = k
        for update;

        if cur is null then
            -- Elemento nuovo sul server
            nv := nextval('public.records_version_seq');
            insert into public.records (user_id, key, version, deleted, payload, device_id, updated_at)
            values (uid, k, nv, coalesce((item ->> 'deleted')::boolean, false), item ->> 'payload', item ->> 'device', now())
            on conflict on constraint records_pkey do nothing;

            if found then
                key := k; ok := true; version := nv;
            else
                -- Un altro dispositivo l'ha appena creato: conflitto
                select r.version into cur from public.records r where r.user_id = uid and r.key = k;
                key := k; ok := false; version := cur;
            end if;
        elsif base is not null and base = cur then
            nv := nextval('public.records_version_seq');
            update public.records r
            set version = nv,
                deleted = coalesce((item ->> 'deleted')::boolean, false),
                payload = item ->> 'payload',
                device_id = item ->> 'device',
                updated_at = now()
            where r.user_id = uid and r.key = k;
            key := k; ok := true; version := nv;
        else
            key := k; ok := false; version := cur;
        end if;

        return next;
    end loop;
end;
$$;

revoke all on function public.push_records(jsonb) from public, anon;
grant execute on function public.push_records(jsonb) to authenticated;

-- 3. Abbinamento di un nuovo dispositivo: la chiave di cifratura, cifrata a sua volta con il codice
--    mostrato su un dispositivo già associato. Vale pochi minuti e viene cancellata dopo l'uso.
create table if not exists public.pairing (
    user_id     uuid        primary key default auth.uid() references auth.users (id) on delete cascade,
    wrapped_key text        not null,
    salt        text        not null,
    expires_at  timestamptz not null
);

alter table public.pairing enable row level security;

drop policy if exists "pairing: proprie" on public.pairing;
create policy "pairing: proprie" on public.pairing
    for all to authenticated
    using (user_id = auth.uid())
    with check (user_id = auth.uid());

-- 4. Copertine delle playlist (cifrate), in una cartella per utente
insert into storage.buckets (id, name, public)
values ('covers', 'covers', false)
on conflict (id) do nothing;

drop policy if exists "covers: proprie" on storage.objects;
create policy "covers: proprie" on storage.objects
    for all to authenticated
    using (bucket_id = 'covers' and (storage.foldername(name))[1] = auth.uid()::text)
    with check (bucket_id = 'covers' and (storage.foldername(name))[1] = auth.uid()::text);

-- 5. Avviso in tempo reale quando cambia un elemento (rispetta le regole di lettura sopra)
do $$
begin
    if not exists (
        select 1 from pg_publication_tables
        where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = 'records'
    ) then
        alter publication supabase_realtime add table public.records;
    end if;
end;
$$;
