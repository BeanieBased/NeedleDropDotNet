using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NeedleDrop.Spotify
{
    /// <summary>
    /// Real Spotify OAuth 2.0 Authorization Code flow with PKCE. No client
    /// secret is ever used or stored — that's the whole point of PKCE for a
    /// desktop app that ships to end users: there is no safe place to hide a
    /// secret in a distributed exe, so Spotify lets public clients prove
    /// their identity with a one-time verifier/challenge pair instead.
    ///
    /// Flow:
    ///  1. Build an /authorize URL with a code_challenge and open it in the
    ///     user's browser.
    ///  2. Spin up a tiny local HttpListener on the redirect URI so Spotify
    ///     can hand the authorization code back to this process.
    ///  3. Exchange that code (+ the original code_verifier) for an access
    ///     token and refresh token at Spotify's /api/token endpoint.
    /// </summary>
    public class SpotifyAuthService
    {
        private const string RedirectUri = "http://127.0.0.1:8080/callback";
        private const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";
        private const string TokenEndpoint = "https://accounts.spotify.com/api/token";

        // Scopes: profile read, liked songs + playlists read, playback
        // control, and "streaming" — that last one is required by the
        // Spotify Web Playback SDK (see SpotifyWebPlaybackController) to let
        // this app create its own Spotify Connect device for actual audio
        // playback, rather than only remote-controlling a device that's
        // already open elsewhere.
        private const string Scopes =
            "user-read-private user-read-email user-library-read " +
            "playlist-read-private playlist-read-collaborative " +
            "user-read-playback-state user-modify-playback-state streaming";

        private static readonly HttpClient Http = new();

        public async Task<SpotifyTokenSet> AuthenticateAsync(string clientId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID is required.", nameof(clientId));

            var codeVerifier = PkceHelper.GenerateCodeVerifier();
            var codeChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");

            var authorizeUrl =
                $"{AuthorizeEndpoint}?response_type=code" +
                $"&client_id={Uri.EscapeDataString(clientId)}" +
                $"&scope={Uri.EscapeDataString(Scopes)}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&state={state}" +
                $"&code_challenge_method=S256" +
                $"&code_challenge={codeChallenge}";

            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri.TrimEnd('/') + "/");
            listener.Start();

            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            var context = await listener.GetContextAsync();
            var query = ParseQueryString(context.Request.Url?.Query ?? "");

            // Always respond to the browser so the tab doesn't hang, even on error.
            var body = query.ContainsKey("error")
                ? "<html><body>Something went wrong connecting Spotify. You can close this tab and go back to Needle Drop.</body></html>"
                : "<html><body>Connected! You can close this tab and go back to Needle Drop.</body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
            listener.Stop();

            if (query.TryGetValue("error", out var error))
                throw new InvalidOperationException($"Spotify authorization was denied or failed: {error}");

            if (!query.TryGetValue("state", out var returnedState) || returnedState != state)
                throw new InvalidOperationException("Spotify's response didn't match this login attempt (state mismatch). Please try connecting again.");

            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Spotify didn't return an authorization code.");

            return await ExchangeCodeForTokensAsync(clientId, code, codeVerifier);
        }

        public async Task<SpotifyTokenSet> RefreshAsync(string clientId, string refreshToken)
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "refresh_token"),
                new System.Collections.Generic.KeyValuePair<string, string>("refresh_token", refreshToken),
                new System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId),
            });

            using var response = await Http.PostAsync(TokenEndpoint, form);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Spotify rejected the refresh request ({(int)response.StatusCode}): {json}");

            var refreshed = ParseTokenResponse(json);
            // A refresh response doesn't always include a new refresh_token —
            // Spotify may just extend the existing one, so keep the old one if so.
            if (string.IsNullOrEmpty(refreshed.RefreshToken))
                refreshed.RefreshToken = refreshToken;

            return refreshed;
        }

        private async Task<SpotifyTokenSet> ExchangeCodeForTokensAsync(string clientId, string code, string codeVerifier)
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "authorization_code"),
                new System.Collections.Generic.KeyValuePair<string, string>("code", code),
                new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId),
                new System.Collections.Generic.KeyValuePair<string, string>("code_verifier", codeVerifier),
            });

            using var response = await Http.PostAsync(TokenEndpoint, form);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Spotify rejected the token exchange ({(int)response.StatusCode}): {json}");

            return ParseTokenResponse(json);
        }

        private static SpotifyTokenSet ParseTokenResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString() ?? "";
            var expiresIn = root.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 3600;
            var refreshToken = root.TryGetProperty("refresh_token", out var refEl) ? refEl.GetString() ?? "" : "";

            return new SpotifyTokenSet
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            };
        }

        /// <summary>
        /// Minimal query-string parser for the redirect callback (code/state/error).
        /// Avoids pulling in the System.Web.HttpUtility assembly just for this.
        /// </summary>
        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return result;

            var trimmed = query.TrimStart('?');
            foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
                result[key] = value;
            }
            return result;
        }
    }
}
