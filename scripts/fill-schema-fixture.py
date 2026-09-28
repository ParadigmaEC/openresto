#!/usr/bin/env python3
"""
fill-schema-fixture.py — put one representative row in every table of a PostgreSQL database.

Used by the migration-safety CI job. Building a database from the previous release's
migrations and immediately applying the new ones proves the two paths agree on *schema* but
never runs a migration against *data*. Those are different tests: PostgreSQL accepts

    ALTER TABLE t ADD COLUMN c text NOT NULL

on an empty table and rejects it the moment the table has a row ("column contains null
values"). A migration that does that passes an empty-database check and then fails on every
real install. Seeding a row before the upgrade is what makes the check represent an upgrade.

The rows are deliberately generic rather than realistic — the point is that each table is
non-empty, not that the data means anything. Emails get a mixed-case, space-padded value so
a normalising migration has something to normalise.

psql is run as $PSQL (default "psql"), so CI can point it into the service container and
match the server's client version:

    PSQL="docker exec -i <container> psql -U postgres" python3 scripts/fill-schema-fixture.py upgrade
"""

import os
import shlex
import subprocess
import sys

# EF Core reads this table to decide what to apply, so a fixture row would corrupt the run.
SKIP_TABLES = {"__EFMigrationsHistory"}

FIXTURE_TIMESTAMP = "2026-01-01 00:00:00+00"

VALUE_BY_TYPE = {
    "int2": "1",
    "int4": "1",
    "int8": "1",
    "numeric": "1.0",
    "float4": "1.0",
    "float8": "1.0",
    "bool": "true",
    "timestamptz": FIXTURE_TIMESTAMP,
    "timestamp": FIXTURE_TIMESTAMP,
    "date": "2026-01-01",
    "time": "00:00:00",
    "interval": "1 hour",
    "uuid": "00000000-0000-0000-0000-000000000001",
    "bytea": "\\x00",
    "json": "{}",
    "jsonb": "{}",
}

COLUMNS_QUERY = """
SELECT table_name, column_name, udt_name, is_nullable, coalesce(column_default, ''),
       is_identity, is_generated, coalesce(character_maximum_length::text, '')
FROM information_schema.columns
WHERE table_schema = 'public'
ORDER BY table_name, ordinal_position;
"""


def psql(database, sql):
    command = shlex.split(os.environ.get("PSQL", "psql")) + [
        "-X", "-q", "-At", "-F", "\t", "-v", "ON_ERROR_STOP=1", "-d", database,
    ]
    return subprocess.run(command, input=sql, text=True, capture_output=True)


def fixture_literal(column_name, udt_name, max_length):
    """A cast literal PostgreSQL will accept for this column."""
    if udt_name.startswith("_"):
        value = "{}"
    elif udt_name in VALUE_BY_TYPE:
        value = VALUE_BY_TYPE[udt_name]
    elif "email" in column_name.lower():
        # Padded and mixed-case on purpose: a migration that trims/lower-cases emails should
        # have something to actually change.
        value = "  Fixture@Example.COM  "
    else:
        value = "fixture"

    if max_length:
        value = value[: int(max_length)]
    return f"'{value}'::\"{udt_name}\""


def load_tables(database):
    result = psql(database, COLUMNS_QUERY)
    if result.returncode != 0:
        raise SystemExit(result.stderr)

    tables = {}
    for line in result.stdout.splitlines():
        table, column, udt, nullable, default, identity, generated, max_length = line.split("\t")
        if table in SKIP_TABLES:
            continue
        columns = tables.setdefault(table, {"all": [], "required": []})
        # Identity/serial keys and generated columns are the server's to fill.
        if identity == "YES" or generated == "ALWAYS" or default.startswith("nextval("):
            continue
        entry = (column, udt, max_length)
        columns["all"].append(entry)
        if nullable == "NO" and not default:
            columns["required"].append(entry)
    return tables


def insert_sql(table, columns):
    # Replica mode skips FK triggers, so tables can be filled in any order. Needs superuser,
    # which the CI service's user is.
    prefix = "SET session_replication_role = replica;\n"
    if not columns:
        return f'{prefix}INSERT INTO "{table}" DEFAULT VALUES;'
    names = ",".join(f'"{name}"' for name, _, _ in columns)
    values = ",".join(fixture_literal(name, udt, length) for name, udt, length in columns)
    return f'{prefix}INSERT INTO "{table}"({names}) VALUES({values});'


def main():
    if len(sys.argv) != 2:
        raise SystemExit("usage: fill-schema-fixture.py <database>")
    database = sys.argv[1]

    tables = load_tables(database)
    filled, skipped = [], []
    for table, columns in sorted(tables.items()):
        # Every column first, so the row exercises as much of the migration as possible; the
        # required-only retry is for a CHECK constraint the generic value doesn't satisfy.
        last_error = ""
        for candidate in (columns["all"], columns["required"]):
            result = psql(database, insert_sql(table, candidate))
            if result.returncode == 0:
                filled.append(table)
                break
            last_error = result.stderr.strip()
        else:
            skipped.append(f"{table} ({last_error})")

    print(f"[fixture] filled {len(filled)}/{len(tables)} tables: {', '.join(filled)}")
    if skipped:
        # Not fatal: a table nobody could populate still leaves the rest of the check useful,
        # but it is a hole in the coverage and should be visible in the log.
        print(f"[fixture] WARNING could not populate: {'; '.join(skipped)}")


if __name__ == "__main__":
    main()
