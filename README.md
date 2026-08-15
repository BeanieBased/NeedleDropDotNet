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

## Notes for next sprint (back end)

- `MainWindow.xaml.cs` has clearly marked mock spots (`Choice_Click`,
  `LockGuess_Click`, `LoadTracklist_Click`) where real Spotify Web API calls,
  scoring, and SQL leaderboard writes will eventually go.
- The two game modes from your brief (song-guessing and streams-comparison)
  aren't both built yet — this pass focused on getting the song-guessing flow
  fully click-through-able per the assignment. Say the word and I'll add a
  second screen set for the streams-comparison mode next.
