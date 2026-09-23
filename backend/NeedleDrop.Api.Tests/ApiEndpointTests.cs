using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NeedleDrop.Api.Tests;

/// <summary>
/// Integration tests: these spin up the actual API in-memory (via
/// WebApplicationFactory) and send real HTTP requests at it, the same way
/// curl or Swagger would. Db.Initialize() is wrapped in a try/catch in
/// Program.cs, so these still run fine even on a machine with no SQL Server
/// reachable — only the /api/db/* routes would fail in that case, and
/// nothing here calls those.
/// </summary>
public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOkStatus()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetSongRound_ReturnsExactlyFourChoices()
    {
        var response = await _client.GetAsync("/api/songmode/track");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var choices = doc.RootElement.GetProperty("choices");

        Assert.Equal(4, choices.GetArrayLength());
    }

    [Fact]
    public async Task SubmitSongGuess_WithUnknownTrackId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/songmode/guess", new
        {
            trackId = "not-a-real-track",
            guess = "anything"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStreamsMatchup_DoesNotRevealStreamCountsUpfront()
    {
        var response = await _client.GetAsync("/api/streamsmode/matchup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        // The matchup endpoint should only expose track info, not the answer.
        Assert.False(doc.RootElement.TryGetProperty("streamsA", out _));
        Assert.False(doc.RootElement.TryGetProperty("streamsB", out _));
    }
}
