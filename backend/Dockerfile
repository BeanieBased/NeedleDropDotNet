# Build context is the repo root (see compose.yaml), so paths here are
# relative to the mono-repo base, not this backend/ folder.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/NeedleDrop.Api.csproj backend/
RUN dotnet restore backend/NeedleDrop.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/NeedleDrop.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage: just the ASP.NET Core runtime, not the full SDK, so the
# final image is small.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "NeedleDropApi.dll"]
