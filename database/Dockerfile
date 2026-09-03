# Real SQL Server, running as a Linux container (not LocalDB — LocalDB is
# Windows-only and can't run in a container). This is the same SQL syntax
# the backend already uses, so nothing had to be rewritten for a different
# database engine.

FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y

USER root
COPY database/initialization-scripts/ /usr/src/app/initialization-scripts/
COPY database/entrypoint.sh /usr/src/app/entrypoint.sh
RUN chmod +x /usr/src/app/entrypoint.sh
USER mssql

ENTRYPOINT ["/usr/src/app/entrypoint.sh"]
