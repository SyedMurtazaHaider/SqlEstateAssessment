#!/usr/bin/env python3
"""Load phpMyAdmin ct_*.json exports into SqlEstatePortal MSSQL tables."""
from __future__ import annotations

import json
import sys
from datetime import datetime
from decimal import Decimal, InvalidOperation
from pathlib import Path

import pyodbc

CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=MURTAZA\\SQL2022;"
    "DATABASE=SqlEstatePortal;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)

ROOT = Path(__file__).resolve().parents[1]  # SqlEstatePortal
SCHEMA_SQL = ROOT / "Data" / "CtInventorySchema.sql"
JSON_DIR = ROOT / "Scripts" / "inventory-json"

# Tables present in schema but without a JSON export in this drop
EXPECTED_MISSING_JSON = {
    "ct_application_database_history",
    "ct_application_server",
    "ct_azure_sync_log",
}

INT_COLS = {
    "id",
    "tx_id",
    "application_id",
    "database_id",
    "user_id",
    "value",
    "max_size_gb",
    "max_size_mb",
    "current_size_mb",
    "azure_sku_capacity",
    "free_space_mb",
}
BIT_COLS = {
    "is_active",
    "zone_redundant",
}
DECIMAL_COLS = {
    "azure_hosting_cost",
}
DATETIME_COLS = {
    "created_on",
    "updated_on",
    "synced_at",
    "azure_cost_synced_at",
    "azure_synced_at",
    "creation_date",
    "created_at",
}

# phpMyAdmin export table name -> destination MSSQL table (when different)
TABLE_NAME_ALIASES = {
    "sql_inventory_servers": "ct_servers",
    "sql_inventory_databases": "ct_database",
}


def extract_table_rows(path: Path) -> tuple[str, list[dict]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, list):
        raise ValueError(f"{path.name}: expected phpMyAdmin JSON array")
    tables = [x for x in payload if isinstance(x, dict) and x.get("type") == "table"]
    if len(tables) != 1:
        raise ValueError(f"{path.name}: expected exactly 1 table block, found {len(tables)}")
    block = tables[0]
    name = block.get("name")
    rows = block.get("data") or []
    if not name:
        raise ValueError(f"{path.name}: table block missing name")
    if not isinstance(rows, list):
        raise ValueError(f"{path.name}: data is not a list")
    name = TABLE_NAME_ALIASES.get(name, name)
    return name, rows


def apply_schema(cur: pyodbc.Cursor) -> None:
    sql = SCHEMA_SQL.read_text(encoding="utf-8")
    # Split on GO-less batches: each IF OBJECT_ID ... END; block is a statement ending with END;
    # Execute whole file as batches split by blank-line-separated IF blocks is fragile;
    # instead split on ";\n\nIF " boundaries carefully.
    batches: list[str] = []
    buf: list[str] = []
    for line in sql.splitlines():
        if line.startswith("--"):
            continue
        buf.append(line)
        if line.strip() == "END;":
            batches.append("\n".join(buf))
            buf = []
    if any(x.strip() for x in buf):
        batches.append("\n".join(buf))
    for batch in batches:
        if batch.strip():
            cur.execute(batch)


def get_table_columns(cur: pyodbc.Cursor, table: str) -> list[tuple[str, str, bool]]:
    """Return [(name, data_type, is_nullable)]."""
    cur.execute(
        """
        SELECT c.name, t.name AS data_type, c.is_nullable
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(?)
        ORDER BY c.column_id
        """,
        table,
    )
    rows = cur.fetchall()
    if not rows:
        raise RuntimeError(f"Table {table} not found")
    return [(r[0], r[1], bool(r[2])) for r in rows]


def convert_value(col: str, raw, data_type: str):
    if raw is None:
        return None
    if isinstance(raw, str) and raw.strip() == "":
        # keep empty string for text-ish columns; treat as NULL for numeric/date
        if col in INT_COLS or col in DECIMAL_COLS or col in DATETIME_COLS:
            return None
        if data_type in ("int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "datetime2", "datetime", "date", "time"):
            return None

    if col in INT_COLS or data_type in ("int", "bigint", "smallint", "tinyint"):
        try:
            return int(raw)
        except (TypeError, ValueError):
            return None

    if col in BIT_COLS or data_type == "bit":
        if isinstance(raw, bool):
            return raw
        s = str(raw).strip().lower()
        if s in ("1", "true", "yes", "y"):
            return True
        if s in ("0", "false", "no", "n"):
            return False
        try:
            return bool(int(raw))
        except (TypeError, ValueError):
            return None

    if col in DECIMAL_COLS or data_type in ("decimal", "numeric", "float", "real"):
        try:
            return Decimal(str(raw))
        except (InvalidOperation, ValueError):
            return None

    if col in DATETIME_COLS or data_type in ("datetime2", "datetime", "date", "time"):
        if isinstance(raw, datetime):
            return raw
        s = str(raw).strip()
        for fmt in (
            "%Y-%m-%d %H:%M:%S",
            "%Y-%m-%d %H:%M:%S.%f",
            "%Y-%m-%dT%H:%M:%S",
            "%Y-%m-%dT%H:%M:%S.%f",
            "%Y-%m-%d",
        ):
            try:
                return datetime.strptime(s, fmt)
            except ValueError:
                continue
        return None

    return str(raw)


def load_table(cur: pyodbc.Cursor, table: str, rows: list[dict]) -> dict:
    cols_meta = get_table_columns(cur, table)
    col_names = [c[0] for c in cols_meta]
    col_types = {c[0]: c[1] for c in cols_meta}

    if not rows:
        cur.execute(f"DELETE FROM dbo.[{table}];")
        return {
            "table": table,
            "json_rows": 0,
            "inserted": 0,
            "extra_json_keys": [],
            "missing_json_keys": col_names,
        }

    json_keys = set(rows[0].keys())
    for r in rows[1:]:
        json_keys |= set(r.keys())

    extra = sorted(json_keys - set(col_names))
    missing = sorted(set(col_names) - json_keys)
    # Insert only intersection, preferring table column order
    insert_cols = [c for c in col_names if c in json_keys]
    identity_col = next((c for c in ("id", "tx_id") if c in insert_cols), None)
    if identity_col is None:
        raise RuntimeError(f"{table}: JSON has no id/tx_id column; refusing load")

    placeholders = ", ".join("?" for _ in insert_cols)
    col_sql = ", ".join(f"[{c}]" for c in insert_cols)
    insert_sql = f"INSERT INTO dbo.[{table}] ({col_sql}) VALUES ({placeholders})"

    cur.execute(f"DELETE FROM dbo.[{table}];")
    cur.execute(f"SET IDENTITY_INSERT dbo.[{table}] ON;")

    batch: list[tuple] = []
    inserted = 0
    errors: list[str] = []
    for i, row in enumerate(rows, start=1):
        values = []
        for c in insert_cols:
            values.append(convert_value(c, row.get(c), col_types[c]))
        # NOT NULL without default: ensure required fields present
        batch.append(tuple(values))
        if len(batch) >= 200:
            try:
                cur.executemany(insert_sql, batch)
            except Exception as ex:  # noqa: BLE001
                # fall back row-by-row for diagnostics
                for j, vals in enumerate(batch):
                    try:
                        cur.execute(insert_sql, vals)
                    except Exception as row_ex:  # noqa: BLE001
                        errors.append(f"row batch@{inserted + j + 1}: {row_ex}")
                        if len(errors) > 5:
                            break
                if errors:
                    raise RuntimeError(f"{table} insert failed: " + "; ".join(errors[:5])) from ex
            inserted += len(batch)
            batch = []

    if batch:
        try:
            cur.executemany(insert_sql, batch)
        except Exception as ex:  # noqa: BLE001
            for j, vals in enumerate(batch):
                try:
                    cur.execute(insert_sql, vals)
                except Exception as row_ex:  # noqa: BLE001
                    errors.append(f"row {inserted + j + 1}: {row_ex}")
                    if len(errors) > 5:
                        break
            if errors:
                raise RuntimeError(f"{table} insert failed: " + "; ".join(errors[:5])) from ex
        inserted += len(batch)

    cur.execute(f"SET IDENTITY_INSERT dbo.[{table}] OFF;")
    cur.execute(f"SELECT COUNT(*) FROM dbo.[{table}];")
    db_count = cur.fetchone()[0]

    # Reseed identity to max(pk)
    cur.execute(f"SELECT ISNULL(MAX([{identity_col}]), 0) FROM dbo.[{table}];")
    max_id = cur.fetchone()[0]
    cur.execute(f"DBCC CHECKIDENT ('dbo.{table}', RESEED, {int(max_id)});")

    return {
        "table": table,
        "json_rows": len(rows),
        "inserted": inserted,
        "db_count": db_count,
        "extra_json_keys": extra,
        "missing_json_keys": missing,
        "max_id": max_id,
    }


def main() -> int:
    if not JSON_DIR.is_dir():
        print(f"JSON dir missing: {JSON_DIR}", file=sys.stderr)
        return 1
    if not SCHEMA_SQL.is_file():
        print(f"Schema missing: {SCHEMA_SQL}", file=sys.stderr)
        return 1

    json_files = sorted(JSON_DIR.glob("ct_*.json"))
    if not json_files:
        print("No ct_*.json files found", file=sys.stderr)
        return 1

    conn = pyodbc.connect(CONN_STR, autocommit=False)
    cur = conn.cursor()
    cur.fast_executemany = True

    print("Applying schema...")
    apply_schema(cur)
    conn.commit()

    # Discover all ct_ tables in DB
    cur.execute(
        "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name LIKE 'ct[_]%'"
    )
    db_tables = {r[0] for r in cur.fetchall()}

    results = []
    loaded_tables = set()
    try:
        for path in json_files:
            table, rows = extract_table_rows(path)
            expected_from_file = path.stem
            if table != expected_from_file:
                raise RuntimeError(f"{path.name}: resolved table '{table}' != file stem '{expected_from_file}'")
            if table not in db_tables:
                raise RuntimeError(f"{path.name}: target table '{table}' does not exist in DB")
            only = {a for a in sys.argv[1:] if not a.startswith("-")}
            if only and table not in only:
                continue
            print(f"Loading {table} ({len(rows)} rows from {path.name})...")
            info = load_table(cur, table, rows)
            results.append(info)
            loaded_tables.add(table)
            if info["db_count"] != info["json_rows"]:
                raise RuntimeError(
                    f"{table}: count mismatch json={info['json_rows']} db={info['db_count']}"
                )
            conn.commit()
            print(
                f"  OK inserted={info['inserted']} db={info['db_count']} max_id={info['max_id']}"
                + (f" extra_keys={info['extra_json_keys']}" if info["extra_json_keys"] else "")
                + (f" missing_keys={info['missing_json_keys']}" if info["missing_json_keys"] else "")
            )
    except Exception:
        conn.rollback()
        raise

    unloaded = sorted(db_tables - loaded_tables)
    only = {a for a in sys.argv[1:] if not a.startswith("-")}
    if only:
        expected_unloaded = sorted(db_tables - only)
        unexpected_unloaded = []
    else:
        unexpected_unloaded = [t for t in unloaded if t not in EXPECTED_MISSING_JSON]
        expected_unloaded = [t for t in unloaded if t in EXPECTED_MISSING_JSON]

    print("\n=== SUMMARY ===")
    total_json = sum(r["json_rows"] for r in results)
    total_db = sum(r["db_count"] for r in results)
    print(f"JSON files loaded: {len(results)}")
    print(f"Total rows: json={total_json} db={total_db}")
    for r in results:
        status = "OK" if r["db_count"] == r["json_rows"] else "MISMATCH"
        print(f"  [{status}] {r['table']}: {r['db_count']}")

    print(f"\nTables with no JSON (expected empty): {expected_unloaded or 'none'}")
    if unexpected_unloaded:
        print(f"ERROR: tables missing JSON unexpectedly: {unexpected_unloaded}")
        return 2

    # Final verification query
    print("\nDB counts:")
    for t in sorted(db_tables):
        cur.execute(f"SELECT COUNT(*) FROM dbo.[{t}]")
        print(f"  {t}: {cur.fetchone()[0]}")

    conn.close()
    print("\nDone.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
