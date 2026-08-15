using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Fixed port
builder.WebHost.UseUrls("http://localhost:5080");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Needle Drop API",
        Version = "v1",
        Description = "Mock back end for Needle Drop"
                   
    });
});

builder.Services.AddCors(options =>
{
    // Wide open
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Needle Drop API v1");
    c.RoutePrefix = "swagger";
});

// ================= Mock data =================

var songTracks = new[]
{
    new Track("t1", "Sample Track A", "Artist One"),
    new Track("t2", "Sample Track B", "Artist Two"),
    new Track("t3", "Sample Track C", "Artist Three"),
    new Track("t4", "Sample Track D", "Artist Four"),
    new Track("t5", "Sample Track E", "Artist Five"),
};

var matchups = new[]
{
    new Matchup("m1", new Track("t1", "Sample Track A", "Artist One"), 812_000_000,
                       new Track("t2", "Sample Track B", "Artist Two"), 640_000_000),
    new Matchup("m2", new Track("t3", "Sample Track C", "Artist Three"), 1_204_000_000,
                       new Track("t4", "Sample Track D", "Artist Four"), 990_000_000),
    new Matchup("m3", new Track("t5", "Sample Track E", "Artist Five"), 455_000_000,
                       new Track("t6", "Sample Track F", "Artist Six"), 610_000_000),
};

// In-memory "leaderboards" — stand-ins for the SQL tables described in the brief.
var songLeaderboard = new ConcurrentBag<LeaderboardEntry>(new[]
{
    new LeaderboardEntry("JWB", 8),
    new LeaderboardEntry("ALT", 7),
    new LeaderboardEntry("MRQ", 6),
});

var streamsLeaderboard = new ConcurrentBag<LeaderboardEntry>(new[]
{
    new LeaderboardEntry("JWB", 83),
    new LeaderboardEntry("ABC", 61),
    new LeaderboardEntry("QRS", 44),
});

var rng = new Random();

// ================= Health =================

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "NeedleDropApi", mode = "mock" }))
   .WithName("Health");

// ================= Guess the Song mode =================

app.MapGet("/api/songmode/track", () =>
{
    var track = songTracks[rng.Next(songTracks.Length)];
    var choices = songTracks
        .Where(t => t.Id != track.Id)
        .OrderBy(_ => rng.Next())
        .Take(3)
        .Append(track)
        .OrderBy(_ => rng.Next())
        .Select(t => new { t.Id, title = t.Title, artist = t.Artist })
        .ToArray();

    return Results.Ok(new
    {
        trackId = track.Id,
        clipStartSeconds = rng.Next(0, 30),
        choices
    });
})
.WithName("GetSongRound");

app.MapPost("/api/songmode/guess", (GuessRequest req) =>
{
    var track = songTracks.FirstOrDefault(t => t.Id == req.TrackId);
    if (track is null) return Results.NotFound(new { error = $"No track with id '{req.TrackId}'" });

    // Mocked scoring rule: a guess "counts" as correct anyway
    bool correct = !string.IsNullOrWhiteSpace(req.Guess) &&
        (track.Title.Contains(req.Guess, StringComparison.OrdinalIgnoreCase) ||
         track.Artist.Contains(req.Guess, StringComparison.OrdinalIgnoreCase));

    return Results.Ok(new
    {
        correct,
        correctTitle = track.Title,
        correctArtist = track.Artist
    });
})
.WithName("SubmitSongGuess");

app.MapGet("/api/songmode/leaderboard", () =>
    Results.Ok(songLeaderboard.OrderByDescending(e => e.Value).Take(10)))
   .WithName("GetSongLeaderboard");

app.MapPost("/api/songmode/leaderboard", (LeaderboardSubmission req) =>
{
    var initials = string.IsNullOrWhiteSpace(req.Initials) ? "YOU" : req.Initials.ToUpperInvariant();
    songLeaderboard.Add(new LeaderboardEntry(initials, req.Value));
    return Results.Ok(songLeaderboard.OrderByDescending(e => e.Value).Take(10));
})
.WithName("PostSongLeaderboard");

// ================= Streams Showdown mode =================

app.MapGet("/api/streamsmode/matchup", () =>
{
    var m = matchups[rng.Next(matchups.Length)];
    return Results.Ok(new
    {
        matchupId = m.Id,
        trackA = new { m.TrackA.Id, title = m.TrackA.Title, artist = m.TrackA.Artist },
        trackB = new { m.TrackB.Id, title = m.TrackB.Title, artist = m.TrackB.Artist }
        // Stream counts intentionally withheld until a guess is submitted, same as gameplay.
    });
})
.WithName("GetStreamsMatchup");

app.MapPost("/api/streamsmode/guess", (StreamsGuessRequest req) =>
{
    var m = matchups.FirstOrDefault(x => x.Id == req.MatchupId);
    if (m is null) return Results.NotFound(new { error = $"No matchup with id '{req.MatchupId}'" });

    bool pickedA = string.Equals(req.Pick, m.TrackA.Id, StringComparison.OrdinalIgnoreCase);
    bool aWon = m.StreamsA >= m.StreamsB;
    bool correct = pickedA ? aWon : !aWon;

    return Results.Ok(new
    {
        correct,
        streamsA = m.StreamsA,
        streamsB = m.StreamsB,
        winnerId = aWon ? m.TrackA.Id : m.TrackB.Id
    });
})
.WithName("SubmitStreamsGuess");

app.MapGet("/api/streamsmode/leaderboard", () =>
    Results.Ok(streamsLeaderboard.OrderByDescending(e => e.Value).Take(10)))
   .WithName("GetStreamsLeaderboard");

app.MapPost("/api/streamsmode/leaderboard", (LeaderboardSubmission req) =>
{
    var initials = string.IsNullOrWhiteSpace(req.Initials) ? "YOU" : req.Initials.ToUpperInvariant();
    streamsLeaderboard.Add(new LeaderboardEntry(initials, req.Value));
    return Results.Ok(streamsLeaderboard.OrderByDescending(e => e.Value).Take(10));
})
.WithName("PostStreamsLeaderboard");

Console.WriteLine();
Console.WriteLine("Needle Drop API (mock) running at http://localhost:5080");
Console.WriteLine("Swagger UI (for browser testing) at http://localhost:5080/swagger");
Console.WriteLine();

app.Run();

// ================= Models =================

record Track(string Id, string Title, string Artist);
record Matchup(string Id, Track TrackA, long StreamsA, Track TrackB, long StreamsB);
record LeaderboardEntry(string Initials, int Value);
record GuessRequest(string TrackId, string Guess);
record StreamsGuessRequest(string MatchupId, string Pick);
record LeaderboardSubmission(string Initials, int Value);
