#!/usr/bin/env python3
"""Parse ct_applications.servers (CSV / free text) into ct_application_server M:N links."""
from __future__ import annotations

import re
import sys
from collections import Counter
from pathlib import Path

import pyodbc

CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=MURTAZA\\SQL2022;"
    "DATABASE=SqlEstatePortal;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)

SCHEMA_SQL = Path(__file__).resolve().parents[1] / "Data" / "CtInventorySchema.sql"

SKIP_TOKEN = re.compile(
    r"^(n/?a|na|none|null|-|multiple servers|remote azure hosted|tbc|tba|unknown|"
    r"tolerate|invest|eliminate|migrate|yes|no)$",
    re.I,
)
SPLIT = re.compile(r"[,;\n\r|]+|_x000[Bb]_|[\u000b\xa0]+")
HOSTISH = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{2,}")


def ensure_table(cur: pyodbc.Cursor) -> None:
    cur.execute(
        """
IF OBJECT_ID(N'dbo.ct_application_server', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ct_application_server] (
        [id] int IDENTITY(1,1) NOT NULL,
        [application_id] int NOT NULL,
        [server_id] int NULL,
        [server_name] nvarchar(200) NOT NULL,
        [source_text] nvarchar(500) NULL,
        [created_on] datetime2 NULL CONSTRAINT [DF_ct_application_server_created_on] DEFAULT (SYSUTCDATETIME()),
        [created_by] nvarchar(100) NULL,
        CONSTRAINT [PK_ct_application_server] PRIMARY KEY CLUSTERED ([id]),
        CONSTRAINT [UQ_ct_application_server_app_name] UNIQUE ([application_id], [server_name])
    );
    CREATE NONCLUSTERED INDEX [idx_ct_application_server_application_id]
        ON dbo.[ct_application_server] ([application_id]);
    CREATE NONCLUSTERED INDEX [idx_ct_application_server_server_id]
        ON dbo.[ct_application_server] ([server_id]);
    CREATE NONCLUSTERED INDEX [idx_ct_application_server_server_name]
        ON dbo.[ct_application_server] ([server_name]);
END;
"""
    )


def clean_token(raw: str, known: dict[str, int]) -> str:
    t = raw.strip()
    if not t:
        return ""
    t = re.sub(r"\([^)]*\)", " ", t)
    t = re.sub(r"\+.*$", "", t)
    t = re.sub(r"\s+", " ", t).strip(" ._-")
    m = re.search(r"(?:server|host)\s*[:=]\s*([A-Za-z0-9._-]+)\s*$", t, re.I)
    if m:
        t = m.group(1)
    if "://" in t or (" " in t and not re.fullmatch(r"[A-Za-z0-9._-]+", t.replace(" ", ""))):
        parts = HOSTISH.findall(t)
        for cand in parts:
            if cand.lower() in known:
                return cand
        host = [c for c in parts if c.count(".") <= 2 and not c.isdigit()]
        return host[0] if host else ""
    return t


def resolve_server_id(name: str, known: dict[str, int]) -> int | None:
    key = name.lower()
    if key in known:
        return known[key]
    for sn, sid in known.items():
        if sn == key or sn.startswith(key) or key.startswith(sn):
            return sid
    return None


def parse_servers_field(raw: str | None, known: dict[str, int]) -> list[tuple[str, str]]:
    """Return list of (normalized_server_name, source_fragment)."""
    if raw is None:
        return []
    text = str(raw).strip()
    if not text:
        return []

    out: list[tuple[str, str]] = []
    seen: set[str] = set()
    for part in SPLIT.split(text):
        source = part.strip()
        if not source:
            continue
        tok = clean_token(source, known)
        if not tok or SKIP_TOKEN.match(tok):
            continue
        if tok.isdigit():
            continue
        if len(tok) < 3:
            continue
        # Reject free-text prose (keep hostname-like names only unless inventory match)
        if " " in tok and tok.lower() not in known:
            continue
        if len(tok) > 120:
            continue
        key = tok.lower()
        if key in seen:
            continue
        seen.add(key)
        # Prefer canonical casing from inventory when matched
        sid = resolve_server_id(tok, known)
        if sid is not None:
            # find original casing from known keys
            for sn, sid2 in known.items():
                if sid2 == sid:
                    # restore display casing from DB load — known keys are lowercased
                    tok = sn  # still lower; fixed later when inserting via lookup map
                    break
        out.append((tok, source[:500]))
    return out


def main() -> int:
    conn = pyodbc.connect(CONN_STR, autocommit=False)
    cur = conn.cursor()
    cur.fast_executemany = True

    print("Ensuring ct_application_server exists...")
    ensure_table(cur)
    conn.commit()

    cur.execute("SELECT tx_id, server_name FROM dbo.ct_servers WHERE server_name IS NOT NULL")
    known_lower: dict[str, int] = {}
    canonical: dict[str, str] = {}
    for tx_id, name in cur.fetchall():
        n = str(name).strip()
        if not n:
            continue
        known_lower[n.lower()] = int(tx_id)
        canonical[n.lower()] = n

    cur.execute(
        """
        SELECT id, servers
        FROM dbo.ct_applications
        WHERE servers IS NOT NULL AND LTRIM(RTRIM(servers)) <> N''
        """
    )
    apps = cur.fetchall()
    print(f"Scanning {len(apps)} applications with non-empty servers...")

    rows: list[tuple[int, int | None, str, str]] = []
    unmatched = Counter()
    matched = 0
    for app_id, servers_text in apps:
        for tok, source in parse_servers_field(servers_text, known_lower):
            key = tok.lower()
            sid = known_lower.get(key)
            if sid is None:
                sid = resolve_server_id(tok, known_lower)
            name = canonical.get(key, tok)
            if sid is not None:
                # use canonical name from inventory
                for sn_l, sid2 in known_lower.items():
                    if sid2 == sid:
                        name = canonical.get(sn_l, tok)
                        break
                matched += 1
            else:
                unmatched[name] += 1
            rows.append((int(app_id), sid, name[:200], source[:500]))

    # de-dupe by (application_id, server_name) case-insensitive
    dedup: dict[tuple[int, str], tuple[int, int | None, str, str]] = {}
    for app_id, sid, name, source in rows:
        dedup[(app_id, name.lower())] = (app_id, sid, name, source)
    final = list(dedup.values())

    print(f"Parsed links: {len(final)} (matched to ct_servers: {matched}, unmatched unique names: {len(unmatched)})")
    if unmatched:
        print("Top unmatched server names:")
        for name, cnt in unmatched.most_common(15):
            print(f"  {cnt:3d}  {name}")

    cur.execute("DELETE FROM dbo.ct_application_server;")
    insert_sql = """
        INSERT INTO dbo.ct_application_server
            (application_id, server_id, server_name, source_text, created_by)
        VALUES (?, ?, ?, ?, ?)
    """
    batch = [(a, s, n, src, "servers-column-scan") for a, s, n, src in final]
    if batch:
        cur.executemany(insert_sql, batch)
    conn.commit()

    cur.execute("SELECT COUNT(*) FROM dbo.ct_application_server")
    total = cur.fetchone()[0]
    cur.execute("SELECT COUNT(*) FROM dbo.ct_application_server WHERE server_id IS NOT NULL")
    linked = cur.fetchone()[0]
    cur.execute("SELECT COUNT(DISTINCT application_id) FROM dbo.ct_application_server")
    apps_linked = cur.fetchone()[0]
    print(f"\nDone. ct_application_server rows={total}, with server_id={linked}, applications={apps_linked}")
    conn.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
