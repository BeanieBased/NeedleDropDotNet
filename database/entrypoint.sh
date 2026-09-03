#!/bin/bash
# The official mssql/server image doesn't auto-run init scripts the way
# postgres/mysql images do, so this does it by hand: start the server in
# the background, poll until it's accepting connections, run every .sql
# file in initialization-scripts/ once, then bring the server to the
# foreground so the container keeps running normally.
set -e

/opt/mssql/bin/sqlservr &
SQLPID=$!

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
if [ ! -x "$SQLCMD" ]; then
  SQLCMD=/opt/mssql-tools/bin/sqlcmd
fi

echo "Waiting for SQL Server to accept connections..."
for i in $(seq 1 60); do
  if "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1; then
    echo "SQL Server is up."
    break
  fi
  sleep 2
done

MARKER_FILE=/var/opt/mssql/data/.needledrop-initialized
if [ ! -f "$MARKER_FILE" ]; then
  for script in /usr/src/app/initialization-scripts/*.sql; do
    echo "Running init script: $script"
    "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -i "$script"
  done
  touch "$MARKER_FILE"
else
  echo "Already initialized, skipping init scripts."
fi

wait "$SQLPID"
