using NeedleDrop.Api;
using Xunit;

namespace NeedleDrop.Api.Tests;

/// <summary>
/// Pure unit tests: no HTTP host, no database, just the scoring rules
/// themselves. These should run instantly and pass/fail purely on the
/// logic in GuessEvaluator.cs.
/// </summary>
public class GuessEvaluatorTests
{
    [Theory]
    [InlineData("Sample Track A", "Artist One", "sample track a", true)]   // matches title, wrong case
    [InlineData("Sample Track A", "Artist One", "Artist One", true)]       // matches artist exactly
    [InlineData("Sample Track A", "Artist One", "Track", true)]            // partial title match
    [InlineData("Sample Track A", "Artist One", "Nonexistent Song", false)]// no match
    [InlineData("Sample Track A", "Artist One", "", false)]                // blank guess
    [InlineData("Sample Track A", "Artist One", null, false)]              // null guess
    public void IsCorrectGuess_MatchesExpectedResult(string title, string artist, string? guess, bool expected)
    {
        var result = GuessEvaluator.IsCorrectGuess(title, artist, guess);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1000, 500, true)]   // A clearly higher
    [InlineData(500, 1000, false)]  // B clearly higher
    [InlineData(750, 750, true)]    // tie goes to A
    public void DoesTrackAWin_MatchesExpectedResult(long streamsA, long streamsB, bool expected)
    {
        var result = GuessEvaluator.DoesTrackAWin(streamsA, streamsB);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("jwb", "JWB")]
    [InlineData("  ab  ", "AB")]
    [InlineData("abcdef", "ABC")]   // longer than 3 chars gets truncated
    [InlineData("", "YOU")]
    [InlineData(null, "YOU")]
    public void NormalizeInitials_MatchesExpectedResult(string? input, string expected)
    {
        var result = GuessEvaluator.NormalizeInitials(input);
        Assert.Equal(expected, result);
    }
}
