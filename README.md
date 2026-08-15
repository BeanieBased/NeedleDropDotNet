# Needle Drop — .NET Front End

A WPF (.NET 8) front end for Needle Drop, covering **both** modes from the
brief. This week's deliverable is the **GUI only** — every control is
interactive (buttons navigate screens, toggle chips switch selection, the
turntable actually spins, battle cards pick a winner, etc.) but nothing is
wired to a real Spotify account, Billboard data, or a SQL leaderboard yet.

## What's here

- `NeedleDrop/` — the WPF project (`MainWindow.xaml` + `MainWindow.xaml.cs`).
  A mode-select screen opens the app, then branches into:
  - **Guess the Song**
    1. **Setup** — the 4-step "create a Spotify app" wizard
    2. **Source** — pick Liked Songs / a Playlist / Daily Drop, guessing mode, Random Start toggle
    3. **Game** — the spinning turntable, multiple choice or type-in guessing, reveal panel
    4. **Final** — score recap, a "missed tracks" list, initials entry, and a mock leaderboard
  - **Streams Showdown**
    1. **Source** — pull matchups from a playlist, or skip Spotify and pick a Billboard chart year
    2. **Game** — two tracks face off; tap the one you think has more streams, keep the streak alive, one wrong pick ends the run
    3. **Final** — final streak, initials entry, and a mock leaderboard

  Per the brief, both modes have their own separate leaderboard where you
  type 3 initials and it lists as e.g. `JWB — 83`. Right now that list lives
  in memory (`_songLeaderboard` / `_streamsLeaderboard` in the code-behind) —
  swapping those for real SQL calls is next sprint's back-end work.
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
  dotnet run --project NeedleDrop
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

Run it (from the published exe, or via `dotnet run --project NeedleDrop.Api`)
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

## Notes for next sprint

- `MainWindow.xaml.cs` has clearly marked mock spots (`Choice_Click`,
  `LockGuess_Click`, `LoadTracklist_Click`) where the front end will
  eventually call this API instead of using its own local sample data.
- `NeedleDrop.Api/Program.cs` has the same kind of markers — swap the
  hard-coded arrays and in-memory `ConcurrentBag` leaderboards for real
  Spotify/Billboard calls and a SQL database when that sprint comes up.
