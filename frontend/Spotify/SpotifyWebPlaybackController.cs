using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace NeedleDrop.Spotify
{
    /// <summary>
    /// Hosts the Spotify Web Playback SDK (https://sdk.scdn.co/spotify-player.js)
    /// inside a hidden WebView2 control so this app gets its OWN Spotify
    /// Connect device — one that's already active the moment it's created —
    /// instead of remote-controlling whatever device the user happened to
    /// have open (desktop app, phone, etc.).
    ///
    /// Why this exists: the original approach called /me/player/play against
    /// an existing device after transferring playback to it. That call
    /// reliably returned a success status code while queuing no audio at
    /// all — confirmed via SpotifyApiClient.DescribeCurrentPlaybackAsync(),
    /// which showed "paused nothing" immediately after a "successful" play
    /// call. This is a documented Spotify Connect race: a device Spotify
    /// hasn't fully finished activating can accept a play command and still
    /// play nothing. The user's own working HTML/JS prototype (script.js)
    /// never hit this problem, because it used the Web Playback SDK instead
    /// of remote control — the SDK's device is active from the instant
    /// player.connect() succeeds, so there's no transfer/activation race to
    /// lose.
    ///
    /// Once DeviceId is populated here, MainWindow uses the EXISTING,
    /// already-correct SpotifyApiClient.StartPlaybackAsync/PausePlaybackAsync
    /// REST methods against THIS device id — nothing about how a track is
    /// started or stopped changes, only which device receives the command.
    /// </summary>
    public class SpotifyWebPlaybackController
    {
        private WebView2? _webView;
        private TaskCompletionSource<string>? _readyTcs;
        private Func<Task<string>>? _getAccessToken;

        public string? DeviceId { get; private set; }
        public bool IsReady => !string.IsNullOrEmpty(DeviceId);

        /// <summary>
        /// Sets up the WebView2 environment, writes the embedded player page
        /// to a temp folder, navigates to it, and waits for the Web Playback
        /// SDK to report a ready device id (or for something to go wrong).
        /// Safe to call more than once — later calls just await the same
        /// readiness if a device id is already known.
        /// </summary>
        public async Task<string> InitializeAsync(WebView2 webView, Func<Task<string>> getAccessToken, TimeSpan timeout)
        {
            if (IsReady && DeviceId is not null) return DeviceId;

            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _getAccessToken = getAccessToken ?? throw new ArgumentNullException(nameof(getAccessToken));
            _readyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            var userDataFolder = Path.Combine(Path.GetTempPath(), "NeedleDropWebView2");
            Directory.CreateDirectory(userDataFolder);

            // Chromium (which WebView2 is built on) blocks audio playback in
            // any page that hasn't received a real, trusted user click —
            // its "autoplay policy." Our player page is invisible and never
            // gets a click, so without this flag the SDK connects fine, the
            // API calls succeed, and Spotify even reports the right track —
            // but literally nothing plays, because Chromium is silently
            // muting the page. This is the standard, documented workaround
            // for embedding audio/video in a headless WebView2/Electron/CEF
            // host: tell Chromium not to require a gesture at all.
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder, options: options);
            await _webView.EnsureCoreWebView2Async(env);

            var playerFolder = Path.Combine(Path.GetTempPath(), "NeedleDropWebPlayer");
            Directory.CreateDirectory(playerFolder);
            var htmlPath = Path.Combine(playerFolder, "player.html");
            File.WriteAllText(htmlPath, PlayerHtml);

            // A virtual host mapping gives the page a real-looking origin
            // (https://needledrop.local/player.html) instead of a bare
            // file:// path — the Spotify SDK and its OAuth flow are picky
            // about running from a proper origin.
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "needledrop.local", playerFolder, CoreWebView2HostResourceAccessKind.Allow);

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.Navigate("https://needledrop.local/player.html");

            var completed = await Task.WhenAny(_readyTcs.Task, Task.Delay(timeout));
            if (completed != _readyTcs.Task)
                throw new TimeoutException("Timed out waiting for the Spotify Web Playback SDK to report a ready device.");

            return await _readyTcs.Task; // rethrows if it was faulted (initialization/authentication/account error)
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                switch (type)
                {
                    case "getToken":
                        // The JS side needs a fresh access token for the SDK's
                        // getOAuthToken callback. Round-trip through the same
                        // refresh logic the rest of the app already uses.
                        var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() ?? "" : "";
                        string token;
                        try
                        {
                            token = _getAccessToken is not null ? await _getAccessToken() : "";
                        }
                        catch
                        {
                            token = "";
                        }
                        var reply = JsonSerializer.Serialize(new { type = "token", requestId, token });
                        _webView?.CoreWebView2.PostWebMessageAsString(reply);
                        break;

                    case "ready":
                        DeviceId = root.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrEmpty(DeviceId))
                            _readyTcs?.TrySetResult(DeviceId);
                        else
                            _readyTcs?.TrySetException(new InvalidOperationException("Spotify player reported ready with no device id."));
                        break;

                    case "error":
                        var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown Spotify player error." : "Unknown Spotify player error.";
                        _readyTcs?.TrySetException(new InvalidOperationException(message));
                        break;
                }
            }
            catch (Exception ex)
            {
                _readyTcs?.TrySetException(ex);
            }
        }

        /// <summary>
        /// The embedded player page. Deliberately mirrors the structure of
        /// the user's own working script.js (initPlayer/onSpotifyWebPlaybackSDKReady)
        /// as closely as possible, since that's the proven-working reference —
        /// just swapping DOM/localStorage plumbing for postMessage calls back
        /// into C#.
        /// </summary>
        private const string PlayerHtml = @"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""><title>Needle Drop Player</title></head>
<body>
<script src=""https://sdk.scdn.co/spotify-player.js""></script>
<script>
  let player = null;
  let tokenRequestSeq = 0;
  const pendingTokenRequests = {};

  window.chrome.webview.addEventListener('message', (event) => {
    let msg;
    try { msg = JSON.parse(event.data); } catch (e) { return; }
    if (msg.type === 'token' && pendingTokenRequests[msg.requestId]) {
      pendingTokenRequests[msg.requestId](msg.token || '');
      delete pendingTokenRequests[msg.requestId];
    }
  });

  function post(obj) {
    window.chrome.webview.postMessage(JSON.stringify(obj));
  }

  function requestToken() {
    return new Promise((resolve) => {
      const requestId = 'req' + (++tokenRequestSeq);
      pendingTokenRequests[requestId] = resolve;
      post({ type: 'getToken', requestId });
    });
  }

  // The SDK's very first connect() attempt right after a freshly-issued
  // token quite often throws a one-off authentication_error before working
  // fine on an immediate retry (Spotify's own token/edge propagation is a
  // beat slower than the SDK's first handshake). Rather than surface that
  // to the player as a failure and make them click twice, retry once or
  // twice ourselves before giving up for real.
  let connectAttempts = 0;
  const MAX_CONNECT_ATTEMPTS = 3;

  function tryConnect() {
    connectAttempts++;
    player.connect();
  }

  function handleRetryableError(prefix, message) {
    if (connectAttempts < MAX_CONNECT_ATTEMPTS) {
      setTimeout(tryConnect, 700);
    } else {
      post({ type: 'error', message: prefix + message });
    }
  }

  window.onSpotifyWebPlaybackSDKReady = () => {
    player = new Spotify.Player({
      name: 'Needle Drop Game',
      getOAuthToken: cb => { requestToken().then(t => cb(t)); },
      volume: 0.8
    });

    player.addListener('ready', ({ device_id }) => {
      post({ type: 'ready', deviceId: device_id });
    });

    player.addListener('not_ready', () => {
      // Device went offline (window minimized elsewhere, etc.) — nothing
      // actionable to do here beyond letting a future play attempt retry.
    });

    player.addListener('initialization_error', ({ message }) => {
      handleRetryableError('Spotify player failed to initialize: ', message);
    });

    player.addListener('authentication_error', ({ message }) => {
      handleRetryableError('Spotify authentication failed: ', message);
    });

    player.addListener('account_error', ({ message }) => {
      // Not retryable — a real Premium-account problem, retrying won't change it.
      post({ type: 'error', message: 'Spotify Premium is required to play snippets. (' + message + ')' });
    });

    tryConnect();
  };
</script>
</body>
</html>";
    }
}
