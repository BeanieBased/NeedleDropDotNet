using System;

namespace NeedleDrop
{
    /// <summary>
    /// Small pieces of game logic pulled out of MainWindow.xaml.cs specifically
    /// so they can be unit tested without needing a live WPF window/STA thread.
    /// Everything here is a plain static method with no UI dependency.
    /// </summary>
    public static class GameLogic
    {
        /// <summary>
        /// Turns a raw stream count into the short display string used on the
        /// Streams Showdown battle cards, e.g. 812000000 -> "812M streams",
        /// 1204000000 -> "1.20B streams".
        /// </summary>
        public static string FormatStreams(long streams)
        {
            if (streams < 0) throw new ArgumentOutOfRangeException(nameof(streams), "Stream count can't be negative.");

            return streams >= 1_000_000_000
                ? $"{streams / 1_000_000_000.0:0.00}B streams"
                : $"{streams / 1_000_000.0:0}M streams";
        }

        /// <summary>
        /// Decides which of two stream counts "wins" a Streams Showdown matchup.
        /// Ties go to A, matching the code-behind's existing tie-breaking rule.
        /// </summary>
        public static bool DoesTrackAWin(long streamsA, long streamsB) => streamsA >= streamsB;

        /// <summary>
        /// Formats a Spotify "popularity" score (0-100) for display. Popularity is
        /// the closest real, publicly-available metric Spotify's API exposes —
        /// there is no endpoint that returns raw stream counts, so once the app is
        /// talking to the real API, Streams Showdown compares popularity instead.
        /// </summary>
        public static string FormatPopularity(int popularity)
        {
            if (popularity < 0 || popularity > 100)
                throw new ArgumentOutOfRangeException(nameof(popularity), "Popularity must be between 0 and 100.");

            return $"{popularity}/100 popularity";
        }

        /// <summary>
        /// Case-insensitive check for whether a typed guess matches a track's
        /// title or artist. Used by the "By Song" / "By Artist" guess modes.
        /// </summary>
        public static bool IsCorrectGuess(string title, string artist, string? guess)
        {
            if (string.IsNullOrWhiteSpace(guess)) return false;
            return title.Contains(guess, StringComparison.OrdinalIgnoreCase) ||
                   artist.Contains(guess, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes whatever the player typed into the initials box before
        /// it's saved to the leaderboard: blank becomes "YOU", everything else
        /// is upper-cased and capped at 3 characters (arcade high-score style).
        /// </summary>
        public static string NormalizeInitials(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "YOU";

            var upper = input.Trim().ToUpperInvariant();
            return upper.Length <= 3 ? upper : upper.Substring(0, 3);
        }
    }
}
