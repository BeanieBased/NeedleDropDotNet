using NeedleDrop;
using Xunit;

namespace NeedleDrop.Tests;

/// <summary>
/// These test GameLogic.cs directly — plain static methods with no WPF
/// dependency — so they run as fast, ordinary unit tests without needing to
/// spin up an actual Window on an STA thread.
/// </summary>
public class GameLogicTests
{
    [Theory]
    [InlineData(812_000_000, "812M streams")]
    [InlineData(1_204_000_000, "1.20B streams")]
    [InlineData(0, "0M streams")]
    public void FormatStreams_MatchesExpectedDisplayString(long streams, string expected)
    {
        var result = GameLogic.FormatStreams(streams);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatStreams_ThrowsOnNegativeInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameLogic.FormatStreams(-1));
    }

    [Theory]
    [InlineData(1000, 500, true)]   // A clearly higher
    [InlineData(500, 1000, false)]  // B clearly higher
    [InlineData(750, 750, true)]    // tie goes to A
    public void DoesTrackAWin_MatchesExpectedResult(long streamsA, long streamsB, bool expected)
    {
        var result = GameLogic.DoesTrackAWin(streamsA, streamsB);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0/100 popularity")]
    [InlineData(78, "78/100 popularity")]
    [InlineData(100, "100/100 popularity")]
    public void FormatPopularity_MatchesExpectedDisplayString(int popularity, string expected)
    {
        var result = GameLogic.FormatPopularity(popularity);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void FormatPopularity_ThrowsOnOutOfRangeInput(int popularity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameLogic.FormatPopularity(popularity));
    }

    [Theory]
    [InlineData("Sample Track A", "Artist One", "sample track a", true)]
    [InlineData("Sample Track A", "Artist One", "Artist One", true)]
    [InlineData("Sample Track A", "Artist One", "Track", true)]
    [InlineData("Sample Track A", "Artist One", "Nonexistent Song", false)]
    [InlineData("Sample Track A", "Artist One", "", false)]
    [InlineData("Sample Track A", "Artist One", null, false)]
    public void IsCorrectGuess_MatchesExpectedResult(string title, string artist, string? guess, bool expected)
    {
        var result = GameLogic.IsCorrectGuess(title, artist, guess);
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
        var result = GameLogic.NormalizeInitials(input);
        Assert.Equal(expected, result);
    }
}
