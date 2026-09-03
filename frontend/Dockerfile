# WPF is a Windows desktop GUI framework. No container — Linux or Windows —
# can display its window, because a container has no attached display/GUI
# session to render into (that's true even for Windows containers; you'd
# need to add remote-desktop tooling on top, which is out of scope here).
#
# What this Dockerfile CAN do, and does: compile and publish the app.
# Since .NET 6+, the WPF/WinForms reference assemblies ship as NuGet
# packages, so `dotnet publish` for a net8.0-windows project succeeds even
# on a Linux build agent — it just can't be *run* here afterward. That's
# enough to containerize the build step and produce NeedleDrop.exe as a
# reproducible artifact; running it still happens on an actual Windows
# machine (or via the GitHub Actions release, same as before).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY frontend/NeedleDrop.csproj frontend/
RUN dotnet restore frontend/NeedleDrop.csproj

COPY frontend/ frontend/
RUN dotnet publish frontend/NeedleDrop.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o /app/publish --no-restore

# Final stage just holds the built .exe so it can be copied out with
# `docker cp` — see README for the exact command. There's nothing to
# "run" here in the normal sense.
FROM busybox AS artifact
WORKDIR /out
COPY --from=build /app/publish/NeedleDrop.exe .
CMD ["sh", "-c", "echo 'NeedleDrop.exe built at /out/NeedleDrop.exe — copy it out with: docker cp <container_name>:/out/NeedleDrop.exe .' && sleep infinity"]
