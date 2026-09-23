using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeedleDrop.Spotify
{
    /// <summary>
    /// Thin wrapper around the real Spotify Web API (https://api.spotify.com/v1).
    /// Every method here hits Spotify's live servers — nothing in this class
    /// is mock data. The one thing Spotify's API does NOT expose anywhere is a
    /// raw stream count, so Streams Showdown uses track Popularity (0-100)
    /// instead when it's pulling from a real playlist.
    ///
    /// Two things changed under this app's feet since it was first written,
    /// both from Spotify's own February 2026 Developer Mode migration:
    ///   - GET /playlists/{id}/tracks was renamed to GET /playlists/{id}/items,
    ///     AND that endpoint now only works for playlists the signed-in user
    ///     OWNS or COLLABORATES on — not playlists they just follow (including
    ///     Spotify's own editorial playlists). Trying to pull tracks from a
    ///     playlist you don't own returns a 403 Forbidden. Make your own
    ///     playlist (or use one you collaborate on) to test this mode.
    ///   - The Track object's "popularity" field is now marked Deprecated in
    ///     Spotify's docs and Development Mode apps may get back 0 for every
    ///     track instead of a real score. LoadStreamsPool below detects an
    ///     all-zero result and the UI warns the player instead of silently
    ///     running a "showdown" that always ties.
    ///   - The per-entry field in a playlist-items response was renamed from
    ///     "track" to "item" (the "track" key is gone entirely, not just
    ///     deprecated) — ParseTrack below is called with whichever key is
    ///     actually present so both the old Liked Songs shape ("track") and
    ///     the new playlist-items shape ("item") parse correctly.
    /// </summary>
    public class SpotifyApiClient
    {
        private const string BaseUrl = "https://api.spotify.com/v1";
        private readonly HttpClient _http = new();

        public void SetAccessToken(string accessToken)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        public async Task<SpotifyUser> GetCurrentUserAsync()
        {
            var json = await GetJsonAsync($"{BaseUrl}/me");
            var root = json.RootElement;
            return new SpotifyUser
            {
                Id = root.GetProperty("id").GetString() ?? "",
                DisplayName = root.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "",
                Product = root.TryGetProperty("product", out var p) ? p.GetString() ?? "" : "",
            };
        }

        public async Task<List<SpotifyTrackInfo>> GetLikedSongsAsync(int limit = 50)
        {
            var tracks = new List<SpotifyTrackInfo>();
            var url = $"{BaseUrl}/me/tracks?limit={Math.Min(limit, 50)}";

            while (url is not null && tracks.Count < limit)
            {
                var json = await GetJsonAsync(url);
                var root = json.RootElement;
                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    JsonElement track;
                    if (!item.TryGetProperty("track", out track) && !item.TryGetProperty("item", out track)) continue;
                    if (track.ValueKind != JsonValueKind.Object) continue;

                    var parsed = ParseTrack(track);
                    if (parsed is not null) tracks.Add(parsed);
                }
                url = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String
                    ? nextEl.GetString()
                    : null;
            }

            return tracks;
        }

        public async Task<List<SpotifyTrackInfo>> GetPlaylistTracksAsync(string playlistIdOrUrl, int limit = 50)
        {
            var playlistId = ExtractPlaylistId(playlistIdOrUrl);
            if (string.IsNullOrWhiteSpace(playlistId))
                throw new ArgumentException("That doesn't look like a valid playlist URL or ID.", nameof(playlistIdOrUrl));

            var tracks = new List<SpotifyTrackInfo>();
            // As of Spotify's February 2026 migration this is "/items", not the
            // old "/tracks" path, and it 403s for any playlist you don't own or
            // collaborate on (see the class-level note above).
            var url = $"{BaseUrl}/playlists/{playlistId}/items?limit={Math.Min(limit, 100)}";

            while (url is not null && tracks.Count < limit)
            {
                JsonDocument json;
                try
                {
                    json = await GetJsonAsync(url);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("(403)"))
                {
                    throw new InvalidOperationException(
                        "Spotify says no (403 Forbidden). As of Spotify's Feb 2026 API changes, this only works for a " +
                        "playlist YOU own or collaborate on — not one you just follow (including Spotify's own editorial " +
                        "playlists). Try pasting one of your own playlists instead.", ex);
                }

                var root = json.RootElement;
                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    // Spotify's Feb 2026 migration renamed this per-entry field from
                    // "track" to "item"; try both so this keeps working either way.
                    JsonElement track;
                    if (!item.TryGetProperty("item", out track) && !item.TryGetProperty("track", out track)) continue;
                    if (track.ValueKind != JsonValueKind.Object) continue;

                    var parsed = ParseTrack(track);
                    if (parsed is not null) tracks.Add(parsed);
                }
                url = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String
                    ? nextEl.GetString()
                    : null;
            }

            return tracks;
        }

        /// <summary>
        /// Asks Spotify directly "what do you think is playing right now?" via
        /// GET /me/player. This is ground truth from Spotify's own servers —
        /// useful specifically when a /play call reports success (2xx) but it's
        /// unclear whether it actually took effect, since Spotify can accept a
        /// play request and still end up with nothing queued if the device
        /// wasn't fully ready. Returns a short human-readable summary rather
        /// than a structured type, since this is purely a diagnostic readout.
        /// </summary>
        public async Task<string> DescribeCurrentPlaybackAsync()
        {
            using var response = await _http.GetAsync($"{BaseUrl}/me/player");

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return "Spotify says: nothing playing anywhere on this account.";

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return $"Spotify's playback-state check failed ({(int)response.StatusCode}): {body}";

            if (string.IsNullOrWhiteSpace(body))
                return "Spotify says: nothing playing anywhere on this account.";

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var isPlaying = root.TryGetProperty("is_playing", out var ip) && ip.GetBoolean();
            var deviceName = root.TryGetProperty("device", out var dev) && dev.TryGetProperty("name", out var dn)
                ? dn.GetString() ?? "unknown device"
                : "unknown device";
            var progressMs = root.TryGetProperty("progress_ms", out var pm) && pm.ValueKind == JsonValueKind.Number
                ? pm.GetInt64()
                : 0;

            string trackDesc = "nothing";
            if (root.TryGetProperty("item", out var itemEl) && itemEl.ValueKind == JsonValueKind.Object)
            {
                var name = itemEl.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
                trackDesc = $"\"{name}\"";
            }

            return $"Spotify says: {(isPlaying ? "playing" : "paused")} {trackDesc} on {deviceName} at {progressMs}ms.";
        }

        public async Task<List<SpotifyDevice>> GetAvailableDevicesAsync()
        {
            var json = await GetJsonAsync($"{BaseUrl}/me/player/devices");
            var devices = new List<SpotifyDevice>();
            foreach (var item in json.RootElement.GetProperty("devices").EnumerateArray())
            {
                devices.Add(new SpotifyDevice
                {
                    Id = item.GetProperty("id").GetString() ?? "",
                    Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    IsActive = item.TryGetProperty("is_active", out var a) && a.GetBoolean(),
                    VolumePercent = item.TryGetProperty("volume_percent", out var v) && v.ValueKind == JsonValueKind.Number
                        ? v.GetInt32()
                        : 100,
                });
            }
            return devices;
        }

        /// <summary>
        /// Explicitly hands playback control to the given device before starting
        /// a track. Calling /me/player/play directly on a device that Spotify
        /// doesn't already consider "active" can return success while producing
        /// no audio at all — transferring first (Spotify's own recommended
        /// sequence) is what actually wakes that device up.
        /// </summary>
        public async Task TransferPlaybackAsync(string deviceId, bool play = false)
        {
            var payload = JsonSerializer.Serialize(new { device_ids = new[] { deviceId }, play });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var response = await _http.PutAsync($"{BaseUrl}/me/player", content);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                var body = await response.Content.ReadAsStringAsync();
                // Non-fatal on purpose: some devices are already active and Spotify
                // can 404/403 a redundant transfer. StartPlaybackAsync below still runs.
                System.Diagnostics.Debug.WriteLine($"Transfer playback returned {(int)response.StatusCode}: {body}");
            }
        }

        /// <summary>
        /// Sets Spotify Connect's own per-device volume — the thing that's
        /// actually responsible for "the app says it's playing but I hear
        /// nothing" more often than any bug in the play call itself.
        /// </summary>
        public async Task SetVolumeAsync(string deviceId, int volumePercent)
        {
            var url = $"{BaseUrl}/me/player/volume?volume_percent={volumePercent}&device_id={Uri.EscapeDataString(deviceId)}";
            using var response = await _http.PutAsync(url, content: null);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                var body = await response.Content.ReadAsStringAsync();
                // Non-fatal: some devices (older speakers, some mobile builds) don't
                // support remote volume control at all and 403 this — playback can
                // still proceed, just without this safety net.
                System.Diagnostics.Debug.WriteLine($"Set volume returned {(int)response.StatusCode}: {body}");
            }
        }

        public async Task StartPlaybackAsync(string deviceId, string trackUri, int positionMs = 0)
        {
            var url = $"{BaseUrl}/me/player/play?device_id={Uri.EscapeDataString(deviceId)}";
            var payload = JsonSerializer.Serialize(new
            {
                uris = new[] { trackUri },
                position_ms = positionMs,
            });

            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var response = await _http.PutAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException(
                        "Spotify refused to start playback (403). This almost always means the account isn't Spotify " +
                        "Premium — the Web API's playback-control endpoints only work for Premium accounts, even though " +
                        "browsing tracks and playlists works fine on Free.");
                }
                throw new InvalidOperationException($"Spotify couldn't start playback ({(int)response.StatusCode}): {body}");
            }
        }

        public async Task PausePlaybackAsync(string deviceId)
        {
            var url = $"{BaseUrl}/me/player/pause?device_id={Uri.EscapeDataString(deviceId)}";
            using var response = await _http.PutAsync(url, content: null);
            // Spotify returns 403 if playback already stopped on its own — not worth failing the round over.
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Forbidden)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Spotify couldn't pause playback ({(int)response.StatusCode}): {body}");
            }
        }

        private async Task<JsonDocument> GetJsonAsync(string url)
        {
            using var response = await _http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Spotify request failed ({(int)response.StatusCode}) for {url}: {body}");

            return JsonDocument.Parse(body);
        }

        /// <summary>
        /// Skips tracks with no id — local files and removed tracks show up
        /// this way in liked-songs/playlist responses and can't be played back.
        /// </summary>
        private static SpotifyTrackInfo? ParseTrack(JsonElement track)
        {
            if (track.ValueKind != JsonValueKind.Object) return null;
            if (!track.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String) return null;

            var id = idEl.GetString() ?? "";
            if (string.IsNullOrEmpty(id)) return null;

            var title = track.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var artist = "";
            if (track.TryGetProperty("artists", out var artistsEl) && artistsEl.ValueKind == JsonValueKind.Array)
            {
                var names = new List<string>();
                foreach (var a in artistsEl.EnumerateArray())
                    if (a.TryGetProperty("name", out var an)) names.Add(an.GetString() ?? "");
                artist = string.Join(", ", names);
            }

            var uri = track.TryGetProperty("uri", out var uriEl) ? uriEl.GetString() ?? $"spotify:track:{id}" : $"spotify:track:{id}";
            var durationMs = track.TryGetProperty("duration_ms", out var durEl) ? durEl.GetInt64() : 0;
            var popularity = track.TryGetProperty("popularity", out var popEl) && popEl.ValueKind == JsonValueKind.Number ? popEl.GetInt32() : 0;

            return new SpotifyTrackInfo
            {
                Id = id,
                Uri = uri,
                Title = title,
                Artist = artist,
                DurationMs = durationMs,
                Popularity = popularity,
            };
        }

        /// <summary>Accepts a bare playlist ID or a full open.spotify.com URL.</summary>
        private static string ExtractPlaylistId(string playlistIdOrUrl)
        {
            if (string.IsNullOrWhiteSpace(playlistIdOrUrl)) return "";

            var trimmed = playlistIdOrUrl.Trim();
            if (!trimmed.Contains('/') && !trimmed.Contains('?')) return trimmed;

            try
            {
                var uri = new Uri(trimmed);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var idx = Array.IndexOf(segments, "playlist");
                if (idx >= 0 && idx + 1 < segments.Length) return segments[idx + 1];
            }
            catch (UriFormatException)
            {
                // Not a URL — fall through and treat the raw string as an ID below.
            }

            // Last resort: strip a query string off whatever was pasted.
            var qIndex = trimmed.IndexOf('?');
            return qIndex >= 0 ? trimmed[..qIndex] : trimmed;
        }
    }
}
