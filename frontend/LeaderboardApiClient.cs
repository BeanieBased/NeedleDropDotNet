using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NeedleDrop
{
    public record LeaderboardRow(int Id, string Initials, int Score);

    /// <summary>
    /// Talks to the real SQL-backed leaderboard endpoints in NeedleDrop.Api
    /// (backend/Data/Db.cs, via backend/Program.cs's /api/db/* routes) —
    /// actual SELECT/INSERT statements against SQL Server LocalDB (or the
    /// containerized SQL Server, if DB_HOST is set for that process), not
    /// mock/in-memory data. This is the piece that replaces the
    /// hard-coded _songLeaderboard/_streamsLeaderboard lists that used to
    /// live directly in MainWindow.
    ///
    /// This assumes the backend API is running at http://localhost:5080
    /// (its default when run outside a container — see backend/Program.cs).
    /// That's a SEPARATE process/project from this WPF app; it has to be
    /// started too (`dotnet run --project backend`, or set both `frontend`
    /// and `backend` as startup projects in Visual Studio) for the
    /// leaderboards to load or save anything. If it's not running, every
    /// call here throws, and MainWindow shows a friendly message instead of
    /// crashing — the rest of the game (Spotify login, playback, guessing)
    /// doesn't depend on this API at all.
    /// </summary>
    public class LeaderboardApiClient
    {
        private const string BaseUrl = "http://localhost:5080";
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        // No Timeout set on the HttpClient itself on purpose — HttpClient's
        // own Timeout has known edge cases where it doesn't reliably abort
        // an in-flight connect attempt on every OS/network combination. An
        // explicit CancellationTokenSource below is a harder, more direct
        // guarantee: after 3 seconds this call WILL be cancelled, full stop,
        // so a backend that's down (or silently dropping the connection
        // instead of refusing it) can't make the UI wait indefinitely.
        private static readonly HttpClient Http = new();
        private const int TimeoutSeconds = 3;

        public Task<List<LeaderboardRow>> GetSongLeaderboardAsync() => GetAsync("/api/db/songleaderboard");
        public Task<List<LeaderboardRow>> GetStreamsLeaderboardAsync() => GetAsync("/api/db/streamsleaderboard");

        public Task<List<LeaderboardRow>> PostSongScoreAsync(string initials, int score) =>
            PostAsync("/api/db/songleaderboard", initials, score);

        public Task<List<LeaderboardRow>> PostStreamsScoreAsync(string initials, int score) =>
            PostAsync("/api/db/streamsleaderboard", initials, score);

        // Every actual network call is wrapped in Task.Run below. This is a
        // deliberate, heavier hammer than normal: if some piece of network
        // stack on a given machine (a VPN client, antivirus, or other
        // Winsock-layer software) implements what LOOKS like an async
        // HttpClient call but secretly blocks synchronously somewhere
        // inside it, that block would normally happen wherever the calling
        // continuation resumes — which, for code awaited from a WPF click
        // handler, is the UI thread's own message pump. That would freeze
        // the whole window exactly the way "stuck on Loading Leaderboard,
        // can't click anything, drops behind other windows" looks, with no
        // exception ever thrown to catch. Task.Run forces the entire call
        // onto a background thread pool thread instead, so even a
        // misbehaving synchronous-under-the-hood network stack can only
        // block that background thread, never the UI thread.
        private Task<List<LeaderboardRow>> GetAsync(string path) => Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var json = await Http.GetStringAsync(BaseUrl + path, cts.Token);
            return JsonSerializer.Deserialize<List<LeaderboardRow>>(json, JsonOpts) ?? new List<LeaderboardRow>();
        });

        private Task<List<LeaderboardRow>> PostAsync(string path, string initials, int score) => Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            // Matches backend/Program.cs's `record LeaderboardSubmission(string Initials, int Value)` —
            // ASP.NET Core's default JSON body binding is case-insensitive, so lowercase keys here are fine.
            var payload = JsonSerializer.Serialize(new { initials, value = score });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(BaseUrl + path, content, cts.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            return JsonSerializer.Deserialize<List<LeaderboardRow>>(json, JsonOpts) ?? new List<LeaderboardRow>();
        });
    }
}
