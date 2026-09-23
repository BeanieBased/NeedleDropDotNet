# Needle Drop — .NET Front End

A WPF (.NET 8) front end for Needle Drop, covering **both** modes from the
brief. This is no longer just the GUI shell — it's wired to a real Spotify
account (real login, real track data, real audio playback via the Spotify
Web Playback SDK) and a real SQL Server database for both leaderboards. See
"Real Spotify integration" and "Leaderboards (real SQL, not mock data)"
further down for exactly how each of those works and their honest limits.

## What's here

- `frontend/` — the WPF project (`MainWindow.xaml` + `MainWindow.xaml.cs`).
  A mode-select screen opens the app, then branches into:
  - **Guess the Song**
    1. **Setup** — the 4-step "create a Spotify app" wizard
    2. **Source** — pick Liked Songs / a Playlist / Daily Drop, guessing mode, Random Start toggle
    3. **Game** — the spinning turntable, multiple choice or type-in guessing, reveal panel
    4. **Final** — score recap, a "missed tracks" list, initials entry, and the real leaderboard
  - **Streams Showdown**
    1. **Source** — pull matchups from a playlist, or skip Spotify and pick a Billboard chart year
    2. **Game** — two tracks face off; tap the one you think has more streams, keep the streak alive, one wrong pick ends the run
    3. **Final** — final streak, initials entry, and the real leaderboard

  Per the brief, both modes have their own separate leaderboard where you
  type 3 initials and it lists as e.g. `JWB — 83`. That list is a real SQL
  table now — see "Leaderboards (real SQL, not mock data)" below for how
  `frontend/LeaderboardApiClient.cs` talks to `backend/Data/Db.cs` over
  HTTP, and why the backend project has to be running too.
- `.github/workflows/build.yml` — a GitHub Actions workflow that builds a
  self-contained `NeedleDrop.exe` on a Windows runner and attaches it to a
  GitHub Release every time you push to `main`.

## Why it builds in CI instead of locally in this environment

WPF needs the Windows desktop SDK, and NuGet restore needs network access to
`nuget.org` — neither is available in the sandbox this was written in. The
project itself is a normal, valid WPF solution though, so:

- **On your own Windows machine** (with Visual Studio 2022 or the .NET 8 SDK):
  ```
  dotnet build NeedleDrop.sln
  dotnet run --project frontend
  ```
- **Via GitHub Actions (recommended for the assignment):** just push this repo
  to GitHub. The workflow runs automatically, builds `NeedleDrop.exe`, and
  creates a Release named `Needle Drop build N`. Open that release on GitHub —
  the `.exe` asset's URL is what you submit.

## Submitting

1. Push this repo to GitHub (needs a `main` branch).
2. Check the **Actions** tab — the "Build and Release Needle Drop" workflow
   should run and go green.
3. Open the **Releases** page on your repo, find the newest release, and
   right-click the `NeedleDrop.exe` asset to copy its direct link.
4. Submit that URL.

## Back-end API (`NeedleDrop.Api`)

A minimal ASP.NET Core Web API, published the same way as the front end —
self-contained `NeedleDropApi.exe`. Every response is mock/hard-coded data,
per this week's assignment; there's no real Spotify, Billboard, or SQL
connection wired up yet, and the request does **not** have to come from the
WPF front end — curl, Postman, or a browser all work.

Run it (from the published exe, or via `dotnet run --project backend`)
and it listens on **http://localhost:5080**. Swagger UI — the easiest way to
test and screenshot — is at **http://localhost:5080/swagger**.

**Endpoints:**

| Method | Route | What it returns |
|---|---|---|
| GET | `/api/health` | `{ status: "ok" }` — sanity check |
| GET | `/api/songmode/track` | A mock round: a track id + 4 multiple-choice options |
| POST | `/api/songmode/guess` | `{ trackId, guess }` → `{ correct, correctTitle, correctArtist }` |
| GET | `/api/songmode/leaderboard` | Mock leaderboard, e.g. `JWB — 8` |
| POST | `/api/songmode/leaderboard` | `{ initials, value }` → adds an entry, returns updated list |
| GET | `/api/streamsmode/matchup` | Two mock tracks (stream counts withheld until guessed) |
| POST | `/api/streamsmode/guess` | `{ matchupId, pick }` → `{ correct, streamsA, streamsB, winnerId }` |
| GET | `/api/streamsmode/leaderboard` | Mock streak leaderboard, e.g. `JWB — 83` |
| POST | `/api/streamsmode/leaderboard` | `{ initials, value }` → adds an entry, returns updated list |

## For the "Back-end API executable" assignment

1. Push this update (see below) so the workflow rebuilds — the Release will
   now have **two** assets: `NeedleDrop.exe` (front end) and
   `NeedleDropApi.exe` (back end). Submit the `NeedleDropApi.exe` link for
   this assignment.
2. To get your request/response screen capture: run `NeedleDropApi.exe`
   (double-click it, or `.\NeedleDropApi.exe` from a terminal — a console
   window will confirm it's listening on port 5080), then either:
   - Open **http://localhost:5080/swagger** in a browser, expand an endpoint
     like `GET /api/songmode/track`, click **Try it out** → **Execute**, and
     screenshot the request + the JSON response Swagger shows underneath, or
   - Use curl/Postman, e.g.:
     ```
     curl http://localhost:5080/api/songmode/track
     curl -X POST http://localhost:5080/api/songmode/guess -H "Content-Type: application/json" -d "{\"trackId\":\"t1\",\"guess\":\"Artist One\"}"
     ```
     and screenshot the terminal showing both the command and the JSON that
     comes back.

## Database (`backend/Data/Db.cs`)

Uses **SQL Server Express LocalDB** — the database engine that comes bundled
with Visual Studio. This is already the "no separate server" option: LocalDB
doesn't run as a background Windows service you have to install or manage —
it starts on demand under your own user account the moment something
connects to it, and shuts down when nothing's using it. You browse it
without leaving Visual Studio through **SQL Server Object Explorer** (View
menu → SQL Server Object Explorer, if it's not already docked somewhere). If
it's missing from your machine: Visual Studio Installer → Modify →
Individual Components → search "SQL Server Express LocalDB" and check it.

The first time `NeedleDropApi.exe` runs, it connects to
`(localdb)\MSSQLLocalDB`, creates a `NeedleDropDb` database if it doesn't
exist yet, creates three tables if they don't exist, and seeds sample rows
if the tables are empty:

- **`Tracks`** — the main table (`Id`, `SpotifyTrackId`, `Title`, `Artist`, `Streams`), seeded with 6 sample tracks
- **`SongLeaderboard`** — just `Id`, `Initials`, `Score`
- **`StreamsLeaderboard`** — just `Id`, `Initials`, `Score`

Kept deliberately minimal — a leaderboard only needs who (initials) and how
well they did (score), so that's all each of those tables stores.

None of this is final schema — columns will change once real Spotify/Billboard
data is flowing in. It's just enough to prove the database exists, has real
tables, and can answer real queries.

**New endpoints that hit the real database** (as opposed to last week's
`/api/songmode/*` and `/api/streamsmode/*`, which are still there and still
mocked/in-memory):

| Method | Route | What it does |
|---|---|---|
| GET | `/api/db/tracks` | `SELECT * FROM Tracks` — **this is the one to screen-capture** |
| GET | `/api/db/songleaderboard` | `SELECT * FROM SongLeaderboard` |
| POST | `/api/db/songleaderboard` | `{ initials, value }` → real `INSERT`, then returns the updated table |
| GET | `/api/db/streamsleaderboard` | `SELECT * FROM StreamsLeaderboard` |
| POST | `/api/db/streamsleaderboard` | `{ initials, value }` → real `INSERT`, then returns the updated table |

## For the "Application DB setup" assignment

1. Make sure LocalDB is installed (see above) — most Visual Studio installs
   already have it.
2. Run `NeedleDropApi.exe` (or F5 the `backend` project in Visual
   Studio). The console should print:
   ```
   Database ready: (localdb)\MSSQLLocalDB -> NeedleDropDb
   ```
   If instead you see a `WARNING: could not reach LocalDB...` line, LocalDB
   isn't installed/running — go back to step 1.
3. **Confirm the data landed**, two ways (pick one or do both for the capture):
   - **Through Visual Studio directly:** View → SQL Server Object Explorer →
     expand `(localdb)\MSSQLLocalDB` → Databases → `NeedleDropDb` → Tables →
     right-click `dbo.Tracks` → **View Data**. That runs a `SELECT * FROM
     Tracks` and shows the 6 seeded rows in a grid.
   - **Through the API:** open `http://localhost:5080/swagger`, expand
     `GET /api/db/tracks`, click **Try it out** → **Execute**, and see the
     same rows come back as JSON.
4. Screen-capture (screenshot or short recording) whichever of those shows
   the table's contents — that's your `SELECT *` submission.
5. Push the updated code so the GitHub Actions workflow rebuilds
   `NeedleDropApi.exe` with the DB code included, then submit your screen
   capture per the assignment's file upload.

## Docker (`docker-compose.yml`, `frontend/Dockerfile`, `backend/Dockerfile`, `db/Dockerfile`)

The repo follows this mono-repo layout (required exact paths in bold):

```
NeedleDropDotNet/
├── NeedleDrop.sln
├── **docker-compose.yml**
├── **.dockerignore**
├── frontend/
│   ├── NeedleDrop.csproj       (was NeedleDrop/ — the WPF app)
│   ├── Dockerfile
│   └── NeedleDrop.Tests/       xUnit tests for the front end
├── backend/
│   ├── NeedleDrop.Api.csproj   (was NeedleDrop.Api/ — the Web API)
│   ├── Dockerfile
│   └── NeedleDrop.Api.Tests/   xUnit tests for the back end
├── **db/**
│   └── **init.sql**
└── **.github/workflows/ci.yml**
```

**backend** and **db** are fully containerized and actually run and talk to
each other through Docker — that part works exactly like a real deployment.
**db** runs a real SQL Server Linux container (`mcr.microsoft.com/mssql/server`),
since LocalDB is Windows-only and can't run inside a container; the SQL
syntax didn't need to change, just the connection method (SQL login instead
of Windows-integrated auth). `backend/Data/Db.cs` picks this up
automatically: if a `DB_HOST` environment variable is set (as compose sets
it), it connects to that container; if not (plain local dev in Visual
Studio), it falls back to LocalDB exactly as before. Nothing about running
it in Visual Studio changes.

**frontend is the one honest limitation here.** WPF renders a native Windows
window, and no container — Linux or Windows — has a display session to
render that window into. `frontend/Dockerfile` still does something real: it
compiles and publishes `NeedleDrop.exe` inside the container (the WPF
reference assemblies ship as NuGet packages since .NET 6, so this actually
works even on a Linux build image), but the resulting container can't be
"run" as a live app the way the other two can — it just holds the built
`.exe` as an artifact. See the comments at the top of that Dockerfile for
the full explanation.

### Running it

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) if you don't have it.
2. From the repo root:
   ```
   docker compose up --build
   ```
3. Give the database container a minute on first run — the healthcheck in
   `docker-compose.yml` makes the backend wait for it automatically, so you
   shouldn't need to do anything, just watch the logs.
4. Once it's up:
   - Backend Swagger UI (mock endpoints from Week 3): `http://localhost:8080/swagger`
   - Backend DB-backed endpoints (Week 4): `http://localhost:8080/api/db/tracks`, etc.
   - Database is also reachable directly on `localhost:1433` if you want to
     connect with SSMS/Azure Data Studio using login `sa` / password
     `YourStrong!Passw0rd` (see `docker-compose.yml` — change this before this
     ever becomes a real deployment).
5. To pull the compiled front-end exe out of its container:
   ```
   docker cp needledrop-frontend:/out/NeedleDrop.exe .
   ```
6. `docker compose down` to stop everything (add `-v` if you also want to
   wipe the database volume and start fresh next time).

## Testing (`frontend/NeedleDrop.Tests`, `backend/NeedleDrop.Api.Tests`)

Both apps have an xUnit test project, living alongside the app it tests:

```
frontend/NeedleDrop.Tests/   — targets net8.0-windows (has to match the
                                WPF project's TFM to reference it)
backend/NeedleDrop.Api.Tests/ — targets net8.0
```

Since most of the front end's logic lives directly in `MainWindow.xaml.cs`
event handlers (which need a live WPF window on an STA thread to test) and
most of the backend's logic lives in minimal-API route lambdas, a small
amount of that logic was pulled out into plain static classes specifically
so it's testable without any of that:

- **`frontend/GameLogic.cs`** — stream-count formatting, matchup win/lose,
  initials normalization. Used by `MainWindow.xaml.cs`, tested directly by
  `frontend/NeedleDrop.Tests/GameLogicTests.cs` (12 pass/fail cases).
- **`backend/GuessEvaluator.cs`** — the same three ideas, backend side. Used
  by `backend/Program.cs`'s route handlers, tested directly by
  `backend/NeedleDrop.Api.Tests/GuessEvaluatorTests.cs` (14 pass/fail cases).

The backend also gets a handful of real integration tests in
`backend/NeedleDrop.Api.Tests/ApiEndpointTests.cs`, using
`WebApplicationFactory` to boot the actual API in-memory and send it real
HTTP requests — no mocking, these hit the same route handlers Swagger/curl
would. `Db.Initialize()` is already wrapped in a try/catch in `Program.cs`,
so these pass even on a machine with no SQL Server reachable; nothing in
these specific tests touches `/api/db/*`.

### Running the tests

**In Visual Studio:** Test menu → Test Explorer → Run All Tests.

**From the command line** (also what CI runs):
```
dotnet test NeedleDrop.sln
```

## CI pipeline (`.github/workflows/ci.yml`)

Three jobs, each gated on the previous one succeeding:

1. **build** — triggered on push to `main` (i.e. on merge). Restores and
   builds the whole solution. If this fails, nothing else runs.
2. **test** — `needs: build`, so it only starts once build succeeds. Runs
   `dotnet test` across both xUnit projects and uploads the `.trx` results
   as a workflow artifact either way (`if: always()`), so a failing run
   still leaves something to download and inspect.
3. **publish** — `needs: test`, so it only runs once tests pass. Builds the
   two self-contained executables and attaches them to a GitHub Release,
   same as previous weeks — now gated behind a green test suite instead of
   just a successful build.

You can see this dependency chain directly in the Actions tab: each job
shows as its own row, and `test`/`publish` will visibly wait on (and skip
if) the job before it fails.

## For the "Initial Repo structure & Docker integration" assignment

Submit `.github/workflows/ci.yml` — that's the file this assignment asks
for (it doesn't have to be named exactly `ci.yml`, but that's what this repo
uses). Push the whole repo either way so the grader can see the required
structure and run `docker compose up --build` if they want to.

## For the "Unit Testing" assignment

1. Pull this update, then in Visual Studio: Test menu → Test Explorer →
   **Run All Tests**.
2. Screen-capture the results — the list of ~30 tests all green, plus the
   summary line at the top ("X Passed").
3. To also show a test **failing** on a real condition (optional but easy
   proof the suite isn't just rubber-stamping): temporarily change an
   `InlineData` expected value in either test file to something wrong, rerun,
   screenshot the red X, then change it back before you commit.
4. Push the update so CI picks up the relocated test projects too.

## Real Spotify integration (`frontend/Spotify/`)

The front end now talks to Spotify's real Web API instead of using sample
data — real login, real track data, real playback control. Nothing here was
verified against Spotify's live servers from the environment this was built
in (no network access to spotify.com domains, no .NET SDK to actually run
it), so treat this as a careful, spec-correct implementation rather than a
tested one. Build and run it in Visual Studio to confirm it end-to-end
against your own Spotify app.

**Files:**

- `Spotify/PkceHelper.cs` — generates the PKCE code_verifier/code_challenge
  pair (SHA-256 + base64url) used to prove the token exchange came from this
  same app instance, without ever needing a client secret.
- `Spotify/SpotifyAuthService.cs` — the OAuth 2.0 Authorization Code flow
  with PKCE. Opens the system browser to Spotify's `/authorize` page, runs a
  tiny local `HttpListener` on `http://127.0.0.1:8080/callback` to catch the
  redirect, validates the `state` parameter (CSRF protection), then exchanges
  the code for an access token + refresh token at Spotify's `/api/token`.
- `Spotify/SpotifyApiClient.cs` — the real API calls: current user (`/me`),
  Liked Songs (`/me/tracks`), playlist tracks (`/playlists/{id}/items`),
  available devices (`/me/player/devices`), and playback
  start/pause (`/me/player/play`, `/me/player/pause`).
- `Spotify/SpotifyWebPlaybackController.cs` — hosts the Spotify **Web
  Playback SDK** inside a hidden WebView2 control so this app gets its own,
  already-active Spotify Connect device instead of remote-controlling
  whatever device the user happened to have open. See the playback section
  below for why this exists.
- `Spotify/LocalSettings.cs` — saves the Client ID and refresh token to
  `%AppData%\NeedleDrop\settings.json` as plain JSON, so a returning player
  skips straight past the setup wizard. **This is not a secure credential
  store** — no encryption, no OS credential vault — which is an acceptable
  tradeoff for a class project but not something to ship publicly as-is.
- `Spotify/SpotifyModels.cs` — the plain data types (`SpotifyTokenSet`,
  `SpotifyUser`, `SpotifyTrackInfo`, `SpotifyDevice`) shared across the
  above.

**Honest limits, from what Spotify's API actually offers (and what changed
under this app in Spotify's own February 2026 Developer Mode migration):**

1. **No stream-count endpoint exists, and Popularity is now deprecated too.**
   Spotify's Web API never exposed raw stream counts — the closest real
   metric was always Popularity (0–100). As of Feb 2026, Spotify marked that
   field **Deprecated** in its own docs, and Development Mode apps can get
   back `0` for every track instead of a real score. `StartShowdown_Click`
   detects an all-zero result and tells the player to use Billboard Year
   instead of silently running a "showdown" that always ties.
   Billboard Year itself can't be a live pull either — there's no free public
   Billboard chart API — so `MainWindow.xaml.cs`'s `BillboardYearHits`
   dictionary hand-curates real, well-known charting songs for 2015–2025
   (real titles/artists, in roughly correct relative popularity order) paired
   with **estimated** lifetime stream counts, rather than placeholder "Sample
   Hit A" names. The UI labels this "(est. streams)" so it's never mistaken
   for the real-Popularity playlist mode. 2026 reuses the 2025 list since
   that year's actual chart wasn't knowable when this was written.
2. **Playlist tracks only work for playlists you own or collaborate on.**
   Spotify's Feb 2026 migration renamed `GET /playlists/{id}/tracks` to
   `GET /playlists/{id}/items` **and** restricted it to playlists the signed-
   in user owns or collaborates on — a playlist you just follow (including
   Spotify's own editorial ones) now returns `403 Forbidden`. The same
   migration also renamed the per-entry field in that response from `track`
   to `item`, which silently broke parsing (a real, owned 600-track playlist
   would come back looking empty) until `SpotifyApiClient` was updated to
   check for either key. Both source screens say the ownership rule next to
   the playlist field, and a 403 from this endpoint gets rewritten into that
   same explanation instead of a raw status code.
3. **Playback needs Spotify Premium, and now plays through a device this app
   creates itself.** The first version of this remote-controlled whatever
   Spotify device the player already had open (desktop app, phone, web
   player) via `PUT /me/player` ("transfer playback") followed by
   `/me/player/play`. That turned out to be unreliable in practice: Spotify
   would return a success status code for the play call while queuing no
   audio at all, a known race where a device Spotify hasn't fully finished
   activating can accept a play command and still play nothing — confirmed
   here by asking Spotify directly what it thought was playing right after a
   "successful" play call, which came back "paused nothing."

   The fix, based on a working HTML/JS prototype of this same game: use
   Spotify's **Web Playback SDK** instead of remote control. The SDK is a
   JavaScript library that creates a brand-new Spotify Connect device that's
   already active the instant it connects — no transfer step, no activation
   race. `SpotifyWebPlaybackController` hosts that SDK inside a hidden
   WebView2 control (1x1, invisible, never shown to the player), bridges
   Spotify's OAuth token requests back to this app's existing token-refresh
   logic over `postMessage`, and hands back a `device_id` once the SDK
   reports the device is ready. `PlayNeedle_Click` then calls the same,
   already-correct `StartPlaybackAsync` / `PausePlaybackAsync` REST methods
   as before — nothing about *how* a track starts or stops changed, only
   which device receives the command.

   This needs the `"streaming"` OAuth scope (added to `SpotifyAuthService`)
   and the **WebView2 Runtime**, which ships pre-installed with Windows 10/11
   via Microsoft Edge. On an older or stripped-down Windows install without
   it, the [Evergreen Bootstrapper](https://developer.microsoft.com/microsoft-edge/webview2/)
   installs it in a few seconds. If no device shows up, the guess screen
   reports the underlying error (initialization/authentication/account
   error) coming back from the SDK, and a Premium-account error is rewritten
   into a plain "Spotify Premium is required to play snippets" message.

   Two more WebView2-specific wrinkles surfaced once this was actually
   tested against a live account, both fixed in `SpotifyWebPlaybackController`:
   - **The SDK's very first `player.connect()` right after a freshly-issued
     token sometimes throws a one-off `authentication_error` before working
     fine on an immediate retry** — Spotify's own token propagation appears
     to be a beat slower than the SDK's first handshake attempt. The embedded
     player page now retries `connect()` itself (up to 3 attempts, ~700ms
     apart) before reporting a real failure back to C#, so "drop the needle"
     shouldn't need a second click anymore.
   - **Audio was completely silent even though the device and track were
     correct.** Chromium (which WebView2 is built on) blocks audio playback
     in any page that hasn't received a real, trusted user click — its
     "autoplay policy." The embedded player page is invisible and never
     gets clicked, so the SDK connected, the API calls succeeded, and
     Spotify even reported the right track playing — but Chromium was
     silently muting the page underneath it all. Fixed by launching the
     WebView2 environment with the Chromium flag
     `--autoplay-policy=no-user-gesture-required`
     (`CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`), which is
     the standard, documented workaround for embedding audio/video in any
     headless WebView2/Electron/CEF host.

**Using it:** pick a mode, walk through the 4-step wizard once to register a
Client ID and redirect URI on Spotify's Developer Dashboard (same steps as
before, now wired to a real login), and the app remembers you next launch.
Log out from the source screen to clear the saved connection and go through
setup again with a different account. For Streams Showdown or Guess-the-Song
"A Playlist," paste a playlist you created (or collaborate on) — not one you
just follow.

## Leaderboards (real SQL, not mock data)

Both leaderboards now read and write through `frontend/LeaderboardApiClient.cs`
to the backend's real SQL-backed endpoints — `GET`/`POST /api/db/songleaderboard`
and `.../streamsleaderboard` — which run actual `SELECT`/`INSERT` statements
against `dbo.SongLeaderboard`/`dbo.StreamsLeaderboard` in SQL Server LocalDB
(see `backend/Data/Db.cs`). The old in-memory `_songLeaderboard`/
`_streamsLeaderboard` lists and the `/api/songmode/leaderboard` /
`/api/streamsmode/leaderboard` mock routes are gone from the front end's
code path entirely — the mock routes still exist in `backend/Program.cs` for
now (harmless, just unused by the WPF app), since removing them wasn't asked
for and they don't conflict with anything.

**This means the backend API has to be running at the same time as the front
end**, or the leaderboard panel on the Final screen will show "Couldn't
reach the leaderboard" instead of crashing (everything else — Spotify login,
playback, guessing — works fine even if the backend is down, since none of
that goes through it). To run both:

- **From the command line:** two terminals,
  `dotnet run --project backend` and `dotnet run --project frontend`.
- **From Visual Studio:** right-click the solution → **Set Startup Projects**
  → **Multiple startup projects** → set both `NeedleDrop.Api` and
  `NeedleDrop` (the frontend) to **Start**, then hit F5. Both launch together.

The backend defaults to `http://localhost:5080` outside a container (see
`backend/Program.cs`), which is exactly what `LeaderboardApiClient` points
at — no config needed for local dev. LocalDB itself needs no separate setup;
`Db.Initialize()` creates the `NeedleDropDb` database and both tables (plus
a one-time seed of 3 sample rows each) automatically the first time the
backend starts.

**If every `/api/db/*` call 500s with
`System.NotSupportedException: Globalization Invariant Mode is not supported`**
(visible in the backend's own console/terminal output, not the WPF app —
worth checking that window directly if leaderboards ever act up again),
that was `backend/NeedleDrop.Api.csproj` having `InvariantGlobalization`
set to `true`. That's a real, documented incompatibility —
`Microsoft.Data.SqlClient` needs full ICU/globalization support just to
open a connection and throws exactly this the moment `Db.Initialize()` or
any leaderboard query tries to connect. Fixed by removing that property
(the default is already `false`).

`backend/Properties/launchSettings.json` explicitly pins the backend to
`http://localhost:5080` with no HTTPS profile and `launchBrowser: false`.
This exists specifically because of a real Visual Studio gotcha: an
ASP.NET Core project with no `launchSettings.json` gets one auto-generated
the first time it's launched from Visual Studio (including via **Multiple
startup projects**), typically with a **random HTTPS port** and a browser
auto-launched to Swagger. That auto-generated profile's `ASPNETCORE_URLS`
can end up taking effect over `Program.cs`'s own `UseUrls("http://localhost:5080")`
call, so the backend can silently end up listening somewhere other than
where `LeaderboardApiClient` is looking — `dotnet run` from the command
line is unaffected (there's no launch profile involved at all in that
path), but "Multiple startup projects" in Visual Studio is exactly the
scenario where this can bite. With this file in place, Visual Studio uses
the profile that's already there instead of generating its own.

## Notes for next sprint

- `backend/Program.cs`'s `/api/songmode/*` and `/api/streamsmode/*` mock
  routes are now unused by the front end, which talks to Spotify directly
  and to `/api/db/*` for leaderboards — they could be deleted in a later
  cleanup pass, but were left in place since nothing currently asked for
  their removal and they're inert.
