using System;

namespace NeedleDrop.Spotify
{
    /// <summary>Access + refresh token pair returned by Spotify's token endpoint.</summary>
    public class SpotifyTokenSet
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// True once the token is within 30 seconds of expiring, so callers
        /// refresh a little early instead of racing a mid-request expiry.
        /// </summary>
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddSeconds(-30);
    }

    public class SpotifyUser
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Product { get; set; } = ""; // "premium", "free", "open" ...
    }

    public class SpotifyTrackInfo
    {
        public string Id { get; set; } = "";
        public string Uri { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public long DurationMs { get; set; }
        public int Popularity { get; set; }
    }

    public class SpotifyDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }

        /// <summary>
        /// Spotify Connect's own per-device volume (0-100), separate from the
        /// device's system/OS volume. A device can be found, "active", and
        /// successfully told to play, and still be completely silent if this
        /// is 0 — which happens easily if it was last turned down from another
        /// app or another session.
        /// </summary>
        public int VolumePercent { get; set; } = 100;
    }
}
