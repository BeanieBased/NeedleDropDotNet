namespace NeedleDrop.Api;

/// <summary>
/// Small pieces of scoring logic pulled out of the Program.cs route handlers
/// specifically so they can be unit tested directly, with no HTTP host and
/// no database involved.
/// </summary>
public static class GuessEvaluator
{
    /// <summary>
    /// Mocked scoring rule for "Guess the Song": a guess counts as correct if
    /// it case-insensitively appears in the track's title or artist.
    /// </summary>
    public static bool IsCorrectGuess(string title, string artist, string? guess)
    {
        if (string.IsNullOrWhiteSpace(guess)) return false;

        return title.Contains(guess, StringComparison.OrdinalIgnoreCase) ||
               artist.Contains(guess, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Decides which side of a Streams Showdown matchup has more streams.
    /// Ties go to A.
    /// </summary>
    public static bool DoesTrackAWin(long streamsA, long streamsB) => streamsA >= streamsB;

    /// <summary>
    /// Same normalization rule the leaderboard endpoints use before an
    /// insert: blank becomes "YOU", everything else is upper-cased and
    /// capped at 3 characters.
    /// </summary>
    public static string NormalizeInitials(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "YOU";

        var upper = input.Trim().ToUpperInvariant();
        return upper.Length <= 3 ? upper : upper.Substring(0, 3);
    }
}
