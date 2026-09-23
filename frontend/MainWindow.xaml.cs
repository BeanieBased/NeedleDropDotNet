using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NeedleDrop.Spotify;

namespace NeedleDrop
{
    /// <summary>
    /// This window reproduces the Needle Drop web prototype's screens as an
    /// interactive WPF front end, for BOTH game modes described in the brief:
    /// "Guess the Song" (name a track from a one-second clip) and
    /// "Streams Showdown" (pick which of two tracks is more popular).
    ///
    /// This build talks to the REAL Spotify Web API: real PKCE OAuth login,
    /// real Liked Songs/playlist fetching, and real playback control on
    /// whatever device the user already has Spotify open on. Two honest
    /// limits, both because of what Spotify's API actually offers, not a
    /// shortcut taken here:
    ///   - There is no endpoint that returns raw stream counts, so Streams
    ///     Showdown compares real Popularity (0-100) when pulling from a
    ///     playlist. The Billboard-year option stays demo data, since there's
    ///     no free public Billboard API either.
    ///   - Actual audio playback requires Spotify Premium AND an already-open
    ///     Spotify client (desktop app, mobile app, web player) to remote
    ///     control — this app can start/stop a track on that device, but it
    ///     cannot play sound itself.
    /// Leaderboards are still local/in-memory for this build; wiring them to
    /// the SQL leaderboard tables from the DB assignment is the next step.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SpotifyAuthService _authService = new();
        private readonly SpotifyApiClient _apiClient = new();
        private readonly SpotifyWebPlaybackController _webPlayback = new();
        private readonly LeaderboardApiClient _leaderboardClient = new();
        private readonly Random _rng = new();

        private SpotifyTokenSet? _tokens;
        private string _clientId = "";
        private string _pendingMode = "song"; // which mode the wizard/connect flow is currently working towards

        // ---------- Guess the Song state ----------
        private int _round = 1;
        private const int TotalRounds = 10;
        private int _score = 0;
        private string _guessMode = "multiple"; // multiple | song | artist
        private List<SpotifyTrackInfo> _songTracks = new();
        private SpotifyTrackInfo? _currentSongTrack;
        private List<SpotifyTrackInfo> _currentChoices = new();

        // ---------- Streams Showdown state ----------
        private int _streak = 0;
        private bool _usingRealPopularityData = false;
        private List<SpotifyTrackInfo> _streamsPool = new();
        private int _billboardYear = 2020;
        private (string TitleA, string ArtistA, long ValueA, string TitleB, string ArtistB, long ValueB)? _currentMatchup;

        /// <summary>
        /// Curated Billboard-year mode data — real, well-known hit songs for each
        /// year, paired with hand-estimated lifetime Spotify stream counts. There
        /// is no free public Billboard chart API, so this can't be a live pull;
        /// rather than placeholder "Sample Hit A" names, these are actual charting
        /// songs for each year with stream figures estimated to the right rough
        /// order of magnitude (real relative ranking, approximate exact numbers) —
        /// clearly labeled "(estimated streams)" in the UI so it's never confused
        /// with the real-Popularity playlist mode. 2026 reuses 2025's list since
        /// this was built before that year's charts were knowable.
        /// </summary>
        private static readonly Dictionary<int, (string Title, string Artist, long Streams)[]> BillboardYearHits = new()
        {
            [2015] = new (string, string, long)[]
            {
                ("Uptown Funk", "Mark Ronson ft. Bruno Mars", 2_300_000_000L),
                ("See You Again", "Wiz Khalifa ft. Charlie Puth", 2_100_000_000L),
                ("Hello", "Adele", 1_900_000_000L),
                ("Sorry", "Justin Bieber", 1_700_000_000L),
                ("Lean On", "Major Lazer & DJ Snake ft. MØ", 1_800_000_000L),
                ("Thinking Out Loud", "Ed Sheeran", 1_600_000_000L),
                ("Can't Feel My Face", "The Weeknd", 1_300_000_000L),
                ("Bad Blood", "Taylor Swift ft. Kendrick Lamar", 900_000_000L),
            },
            [2016] = new (string, string, long)[]
            {
                ("Closer", "The Chainsmokers ft. Halsey", 2_500_000_000L),
                ("One Dance", "Drake ft. WizKid & Kyla", 2_200_000_000L),
                ("Stressed Out", "Twenty One Pilots", 1_900_000_000L),
                ("Cheap Thrills", "Sia ft. Sean Paul", 1_500_000_000L),
                ("Don't Let Me Down", "The Chainsmokers ft. Daya", 1_400_000_000L),
                ("Work", "Rihanna ft. Drake", 1_300_000_000L),
                ("Panda", "Desiigner", 1_100_000_000L),
                ("Send My Love", "Adele", 700_000_000L),
            },
            [2017] = new (string, string, long)[]
            {
                ("Shape of You", "Ed Sheeran", 4_000_000_000L),
                ("Despacito", "Luis Fonsi ft. Daddy Yankee", 3_500_000_000L),
                ("Perfect", "Ed Sheeran", 3_200_000_000L),
                ("Something Just Like This", "The Chainsmokers & Coldplay", 2_300_000_000L),
                ("Thunder", "Imagine Dragons", 2_300_000_000L),
                ("HUMBLE.", "Kendrick Lamar", 1_800_000_000L),
                ("Bad and Boujee", "Migos ft. Lil Uzi Vert", 1_600_000_000L),
                ("Wild Thoughts", "DJ Khaled ft. Rihanna & Bryson Tiller", 1_400_000_000L),
            },
            [2018] = new (string, string, long)[]
            {
                ("Havana", "Camila Cabello ft. Young Thug", 2_600_000_000L),
                ("Sicko Mode", "Travis Scott", 2_500_000_000L),
                ("God's Plan", "Drake", 2_400_000_000L),
                ("rockstar", "Post Malone ft. 21 Savage", 2_400_000_000L),
                ("In My Feelings", "Drake", 1_700_000_000L),
                ("Psycho", "Post Malone ft. Ty Dolla $ign", 1_800_000_000L),
                ("One Kiss", "Calvin Harris & Dua Lipa", 1_200_000_000L),
                ("Perfect Duet", "Ed Sheeran & Beyoncé", 1_000_000_000L),
            },
            [2019] = new (string, string, long)[]
            {
                ("Sunflower", "Post Malone & Swae Lee", 3_000_000_000L),
                ("Dance Monkey", "Tones and I", 2_900_000_000L),
                ("Bad Guy", "Billie Eilish", 2_800_000_000L),
                ("Someone You Loved", "Lewis Capaldi", 2_800_000_000L),
                ("Señorita", "Shawn Mendes & Camila Cabello", 2_200_000_000L),
                ("Old Town Road", "Lil Nas X ft. Billy Ray Cyrus", 2_000_000_000L),
                ("7 rings", "Ariana Grande", 1_900_000_000L),
                ("Sucker", "Jonas Brothers", 1_100_000_000L),
            },
            [2020] = new (string, string, long)[]
            {
                ("Blinding Lights", "The Weeknd", 4_500_000_000L),
                ("Circles", "Post Malone", 2_700_000_000L),
                ("Don't Start Now", "Dua Lipa", 2_400_000_000L),
                ("Watermelon Sugar", "Harry Styles", 2_000_000_000L),
                ("Mood", "24kGoldn ft. iann dior", 1_900_000_000L),
                ("The Box", "Roddy Ricch", 1_600_000_000L),
                ("Savage Love", "Jawsh 685 & Jason Derulo", 1_500_000_000L),
                ("ROCKSTAR", "DaBaby ft. Roddy Ricch", 1_800_000_000L),
            },
            [2021] = new (string, string, long)[]
            {
                ("Stay", "The Kid LAROI & Justin Bieber", 2_600_000_000L),
                ("Heat Waves", "Glass Animals", 3_000_000_000L),
                ("good 4 u", "Olivia Rodrigo", 2_300_000_000L),
                ("Levitating", "Dua Lipa ft. DaBaby", 2_000_000_000L),
                ("Industry Baby", "Lil Nas X & Jack Harlow", 1_900_000_000L),
                ("drivers license", "Olivia Rodrigo", 1_800_000_000L),
                ("MONTERO (Call Me By Your Name)", "Lil Nas X", 1_600_000_000L),
                ("Kiss Me More", "Doja Cat ft. SZA", 1_500_000_000L),
            },
            [2022] = new (string, string, long)[]
            {
                ("Heat Waves", "Glass Animals", 3_000_000_000L),
                ("As It Was", "Harry Styles", 2_800_000_000L),
                ("Anti-Hero", "Taylor Swift", 1_600_000_000L),
                ("Bad Habit", "Steve Lacy", 1_400_000_000L),
                ("Unholy", "Sam Smith & Kim Petras", 1_200_000_000L),
                ("About Damn Time", "Lizzo", 1_100_000_000L),
                ("Late Night Talking", "Harry Styles", 1_000_000_000L),
                ("Break My Soul", "Beyoncé", 700_000_000L),
            },
            [2023] = new (string, string, long)[]
            {
                ("Cruel Summer", "Taylor Swift", 1_900_000_000L),
                ("Flowers", "Miley Cyrus", 1_800_000_000L),
                ("Kill Bill", "SZA", 1_400_000_000L),
                ("Calm Down", "Rema & Selena Gomez", 1_500_000_000L),
                ("Last Night", "Morgan Wallen", 900_000_000L),
                ("Seven", "Jung Kook ft. Latto", 900_000_000L),
                ("Paint The Town Red", "Doja Cat", 800_000_000L),
                ("vampire", "Olivia Rodrigo", 700_000_000L),
            },
            [2024] = new (string, string, long)[]
            {
                ("Birds of a Feather", "Billie Eilish", 1_500_000_000L),
                ("Espresso", "Sabrina Carpenter", 1_300_000_000L),
                ("Lose Control", "Teddy Swims", 1_200_000_000L),
                ("Good Luck, Babe!", "Chappell Roan", 1_100_000_000L),
                ("Not Like Us", "Kendrick Lamar", 900_000_000L),
                ("A Bar Song (Tipsy)", "Shaboozey", 800_000_000L),
                ("Please Please Please", "Sabrina Carpenter", 700_000_000L),
                ("I Had Some Help", "Post Malone ft. Morgan Wallen", 700_000_000L),
            },
            [2025] = new (string, string, long)[]
            {
                ("Birds of a Feather", "Billie Eilish", 1_700_000_000L),
                ("Espresso", "Sabrina Carpenter", 1_600_000_000L),
                ("Good Luck, Babe!", "Chappell Roan", 1_400_000_000L),
                ("Not Like Us", "Kendrick Lamar", 1_200_000_000L),
                ("Lose Control", "Teddy Swims", 1_400_000_000L),
                ("A Bar Song (Tipsy)", "Shaboozey", 1_000_000_000L),
                ("Die With A Smile", "Lady Gaga & Bruno Mars", 1_500_000_000L),
                ("APT.", "ROSÉ & Bruno Mars", 1_300_000_000L),
            },
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        // ===================== MODE SELECT =====================

        private void ChooseSongMode_Click(object sender, RoutedEventArgs e)
        {
            _pendingMode = "song";
            BeginConnectFlowAsync();
        }

        private void ChooseStreamsMode_Click(object sender, RoutedEventArgs e)
        {
            _pendingMode = "streams";
            BeginConnectFlowAsync();
        }

        /// <summary>
        /// Decides where to send the player next: straight through to the
        /// right source screen if we already have a live token, a silent
        /// refresh if we have a saved refresh token, or the setup wizard
        /// for a first-time connection.
        /// </summary>
        private async void BeginConnectFlowAsync()
        {
            ScreenModeSelect.Visibility = Visibility.Collapsed;

            if (_tokens is not null && !_tokens.IsExpired)
            {
                GoToSourceScreenForPendingMode();
                return;
            }

            var (savedClientId, savedRefreshToken) = LocalSettings.Load();
            if (!string.IsNullOrWhiteSpace(savedClientId) && !string.IsNullOrWhiteSpace(savedRefreshToken))
            {
                try
                {
                    _clientId = savedClientId;
                    _tokens = await _authService.RefreshAsync(_clientId, savedRefreshToken);
                    LocalSettings.Save(_clientId, _tokens.RefreshToken);
                    _apiClient.SetAccessToken(_tokens.AccessToken);
                    GoToSourceScreenForPendingMode();
                    return;
                }
                catch
                {
                    // Saved refresh token is no longer good (revoked, expired past reuse, etc.) —
                    // fall through to the normal wizard instead of failing silently.
                }
            }

            ShowWizardStep(1);
            ScreenSetup.Visibility = Visibility.Visible;
        }

        private void GoToSourceScreenForPendingMode()
        {
            if (_pendingMode == "song")
            {
                WhoAmIRun.Text = "connected";
                ScreenSource.Visibility = Visibility.Visible;
            }
            else
            {
                ScreenStreamsSource.Visibility = Visibility.Visible;
            }
        }

        private void BackToModeSelect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var screen in AllScreens()) screen.Visibility = Visibility.Collapsed;
            ScreenModeSelect.Visibility = Visibility.Visible;
        }

        private IEnumerable<UIElement> AllScreens() => new UIElement[]
        {
            ScreenModeSelect, ScreenSetup, ScreenSource, ScreenGame, ScreenFinal,
            ScreenStreamsSource, ScreenStreamsGame, ScreenStreamsFinal
        };

        // ===================== SETUP WIZARD (song mode) =====================

        private void SetStepDot(int step)
        {
            var line = (Brush)FindResource("LineBrush");
            var gold = (Brush)FindResource("GoldBrush");
            Dot1.Background = line; Dot2.Background = line; Dot3.Background = line; Dot4.Background = line;
            switch (step)
            {
                case 1: Dot1.Background = gold; break;
                case 2: Dot2.Background = gold; break;
                case 3: Dot3.Background = gold; break;
                case 4: Dot4.Background = gold; break;
            }
        }

        private void ShowWizardStep(int step)
        {
            WizardStep1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            WizardStep2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            WizardStep3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            WizardStep4.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
            SetStepDot(step);
        }

        private void WizardNext1_Click(object sender, RoutedEventArgs e) => ShowWizardStep(2);
        private void WizardBack2_Click(object sender, RoutedEventArgs e) => ShowWizardStep(1);
        private void WizardNext2_Click(object sender, RoutedEventArgs e) => ShowWizardStep(3);
        private void WizardBack3_Click(object sender, RoutedEventArgs e) => ShowWizardStep(2);
        private void WizardNext3_Click(object sender, RoutedEventArgs e) => ShowWizardStep(4);
        private void WizardBack4_Click(object sender, RoutedEventArgs e) => ShowWizardStep(3);
        private void SkipToPaste_Click(object sender, RoutedEventArgs e) => ShowWizardStep(4);

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://developer.spotify.com/dashboard") { UseShellExecute = true });
            }
            catch
            {
                ShowSetupError("Couldn't open your browser automatically — go to developer.spotify.com/dashboard manually.");
            }
        }

        private void CopyRedirect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("http://127.0.0.1:8080/callback");
                CopyRedirectBtn.Content = "Copied!";
            }
            catch
            {
                // Clipboard access can fail in a locked-down environment; not worth blocking setup over.
            }
        }

        private void ShowSetupError(string message)
        {
            SetupErrorText.Text = message;
            SetupErrorBox.Visibility = Visibility.Visible;
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            var clientId = ClientIdInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                ShowSetupError("Paste in your app's Client ID first — it's on your app's page in the Spotify Developer Dashboard.");
                return;
            }

            SetupErrorBox.Visibility = Visibility.Collapsed;
            ConnectBtn.IsEnabled = false;
            ConnectBtn.Content = "Waiting for Spotify…";

            try
            {
                _clientId = clientId;
                _tokens = await _authService.AuthenticateAsync(clientId);
                _apiClient.SetAccessToken(_tokens.AccessToken);

                var user = await _apiClient.GetCurrentUserAsync();
                LocalSettings.Save(clientId, _tokens.RefreshToken);

                ScreenSetup.Visibility = Visibility.Collapsed;
                WhoAmIRun.Text = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Id : user.DisplayName;
                GoToSourceScreenForPendingMode();
            }
            catch (Exception ex)
            {
                ShowSetupError($"Couldn't connect to Spotify: {ex.Message}");
            }
            finally
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = "Connect to Spotify";
            }
        }

        /// <summary>Refreshes the access token in place if it's expired or about to be.</summary>
        private async Task EnsureFreshTokenAsync()
        {
            if (_tokens is null) throw new InvalidOperationException("Not connected to Spotify yet.");
            if (!_tokens.IsExpired) return;

            _tokens = await _authService.RefreshAsync(_clientId, _tokens.RefreshToken);
            _apiClient.SetAccessToken(_tokens.AccessToken);
            LocalSettings.Save(_clientId, _tokens.RefreshToken);
        }

        // ===================== SOURCE SCREEN (song mode) =====================

        private void SourceOption_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            foreach (var opt in new[] { OptLiked, OptPlaylist, OptDaily })
                opt.IsChecked = ReferenceEquals(opt, clicked);

            PlaylistInputWrap.Visibility = ReferenceEquals(clicked, OptPlaylist) ? Visibility.Visible : Visibility.Collapsed;
            DailyHint.Visibility = ReferenceEquals(clicked, OptDaily) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ModeOption_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            foreach (var opt in new[] { ModeMultiple, ModeSong, ModeArtist })
                opt.IsChecked = ReferenceEquals(opt, clicked);

            if (ReferenceEquals(clicked, ModeMultiple)) _guessMode = "multiple";
            else if (ReferenceEquals(clicked, ModeSong)) _guessMode = "song";
            else _guessMode = "artist";
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _tokens = null;
            _clientId = "";
            LocalSettings.Clear();

            ScreenSource.Visibility = Visibility.Collapsed;
            ScreenStreamsSource.Visibility = Visibility.Collapsed;
            ShowWizardStep(1);
            ScreenSetup.Visibility = Visibility.Visible;
        }

        private void ShowSourceError(string message)
        {
            SourceErrorText.Text = message;
            SourceErrorBox.Visibility = Visibility.Visible;
        }

        private async void LoadTracklist_Click(object sender, RoutedEventArgs e)
        {
            SourceErrorBox.Visibility = Visibility.Collapsed;
            var loadBtn = (Button)sender;
            loadBtn.IsEnabled = false;
            var originalContent = loadBtn.Content;
            loadBtn.Content = "Pulling tracklist…";

            try
            {
                await EnsureFreshTokenAsync();

                List<SpotifyTrackInfo> tracks;
                if (OptPlaylist.IsChecked == true)
                {
                    var playlistInput = PlaylistUrlInput.Text.Trim();
                    if (string.IsNullOrWhiteSpace(playlistInput))
                    {
                        ShowSourceError("Paste a playlist URL or ID first.");
                        return;
                    }
                    tracks = await _apiClient.GetPlaylistTracksAsync(playlistInput);
                }
                else
                {
                    // "Daily Drop" reuses Liked Songs and just seeds a smaller, badge-labeled round —
                    // there's no server here to coordinate a shared daily set across players.
                    tracks = await _apiClient.GetLikedSongsAsync();
                }

                if (tracks.Count < 4)
                {
                    ShowSourceError("That source doesn't have enough playable tracks (need at least 4). Try a bigger playlist or Liked Songs.");
                    return;
                }

                _songTracks = tracks;
                _round = 1;
                _score = 0;
                ScreenSource.Visibility = Visibility.Collapsed;
                ScreenGame.Visibility = Visibility.Visible;
                DailyBadge.Visibility = OptDaily.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                StartNextRound();
            }
            catch (Exception ex)
            {
                ShowSourceError($"Couldn't load that tracklist: {ex.Message}");
            }
            finally
            {
                loadBtn.IsEnabled = true;
                loadBtn.Content = originalContent;
            }
        }

        // ===================== GAME SCREEN (song mode) =====================

        private void StartNextRound()
        {
            _currentSongTrack = _songTracks[_rng.Next(_songTracks.Count)];

            // Build the 4 multiple-choice options from real tracks: the correct
            // one plus 3 other real, distinct tracks from the same source.
            var distractorPool = _songTracks.Where(t => t.Id != _currentSongTrack.Id).ToList();
            var distractors = distractorPool.OrderBy(_ => _rng.Next()).Take(3).ToList();
            _currentChoices = new List<SpotifyTrackInfo> { _currentSongTrack };
            _currentChoices.AddRange(distractors);
            _currentChoices = _currentChoices.OrderBy(_ => _rng.Next()).ToList();

            var choiceButtons = new[] { Choice0Btn, Choice1Btn, Choice2Btn, Choice3Btn };
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (i < _currentChoices.Count)
                {
                    choiceButtons[i].Content = $"{_currentChoices[i].Title} — {_currentChoices[i].Artist}";
                    choiceButtons[i].Visibility = Visibility.Visible;
                }
                else
                {
                    choiceButtons[i].Visibility = Visibility.Collapsed;
                }
            }

            ResetRoundUi();
        }

        private void ResetRoundUi()
        {
            RoundCount.Text = $"Round {_round} / {TotalRounds}";
            ScoreText.Text = _score.ToString();
            StatusLine.Text = "Ready when you are";
            ChoicesPanel.Visibility = Visibility.Collapsed;
            GuessPanel.Visibility = Visibility.Collapsed;
            RevealPanel.Visibility = Visibility.Collapsed;
            NextTrackBtn.Visibility = Visibility.Collapsed;
            PlayBtn.Visibility = Visibility.Visible;
            PlayBtn.IsEnabled = true;
            GuessInput.Text = string.Empty;

            foreach (var child in ChoicesPanel.Children)
            {
                if (child is Button b)
                {
                    b.IsEnabled = true;
                    b.ClearValue(Button.BackgroundProperty);
                    b.ClearValue(Button.BorderBrushProperty);
                }
            }
        }

        /// <summary>
        /// Makes sure the Spotify Web Playback SDK device (hosted in the
        /// hidden WebView2 control) is up and has reported a device id.
        /// Cheap to call every time — SpotifyWebPlaybackController itself
        /// short-circuits once a device id is already known.
        /// </summary>
        private async Task<string> EnsureWebPlayerReadyAsync()
        {
            return await _webPlayback.InitializeAsync(SpotifyWebView2, GetFreshAccessTokenAsync, TimeSpan.FromSeconds(20));
        }

        /// <summary>Callback the SDK's JS side calls (via the C#↔JS bridge) whenever it needs a token.</summary>
        private async Task<string> GetFreshAccessTokenAsync()
        {
            await EnsureFreshTokenAsync();
            return _tokens?.AccessToken ?? "";
        }

        private async void PlayNeedle_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSongTrack is null) return;

            StatusLine.Text = "Waking up the player…";
            PlayBtn.IsEnabled = false;

            try
            {
                await EnsureFreshTokenAsync();

                // The Web Playback SDK creates its OWN Spotify Connect device
                // inside the hidden WebView2 control, already active the moment
                // it's ready — no transfer step, no activation race, unlike
                // remote-controlling a device the user already had open.
                string deviceId;
                try
                {
                    deviceId = await EnsureWebPlayerReadyAsync();
                }
                catch (Exception ex)
                {
                    StatusLine.Text = "Couldn't start the built-in player.";
                    ShowSourceErrorOnGameScreen($"Couldn't start playback: {ex.Message}");
                    PlayBtn.IsEnabled = true;
                    return;
                }

                StatusLine.Text = "Spinning…";

                var armAnim = new DoubleAnimation(-32, -8, TimeSpan.FromMilliseconds(500))
                {
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                };
                TonearmRotation.BeginAnimation(RotateTransform.AngleProperty, armAnim);

                var spinAnim = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(1000))
                {
                    RepeatBehavior = new RepeatBehavior(1)
                };
                RecordRotation.BeginAnimation(RotateTransform.AngleProperty, spinAnim);

                var startPositionMs = (int)Math.Max(0, _rng.Next(0, (int)Math.Max(1, _currentSongTrack.DurationMs - 5000)));
                await _apiClient.StartPlaybackAsync(deviceId, _currentSongTrack.Uri, startPositionMs);

                // One-second snippet, same "needle drop" window as the original.
                await Task.Delay(1000);
                await _apiClient.PausePlaybackAsync(deviceId);

                StatusLine.Text = "Your guess?";
                PlayBtn.Visibility = Visibility.Collapsed;

                if (_guessMode == "multiple")
                    ChoicesPanel.Visibility = Visibility.Visible;
                else
                {
                    GuessPanel.Visibility = Visibility.Visible;
                    GuessInput.Focus();
                }
            }
            catch (Exception ex)
            {
                StatusLine.Text = "Playback failed.";
                ShowSourceErrorOnGameScreen($"Couldn't play that track: {ex.Message}");
                PlayBtn.IsEnabled = true;
            }
        }

        /// <summary>Reuses the setup-screen error banner styling by writing straight to the status line for in-round errors.</summary>
        private void ShowSourceErrorOnGameScreen(string message) => StatusLine.Text = message;

        private void Choice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked || _currentSongTrack is null) return;

            var clickedIndex = Array.IndexOf(new[] { Choice0Btn, Choice1Btn, Choice2Btn, Choice3Btn }, clicked);
            bool isCorrect = clickedIndex >= 0 && clickedIndex < _currentChoices.Count &&
                              _currentChoices[clickedIndex].Id == _currentSongTrack.Id;

            foreach (var child in ChoicesPanel.Children)
                if (child is Button b) b.IsEnabled = false;

            clicked.BorderBrush = isCorrect ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
            if (isCorrect) _score++;
            ShowReveal();
        }

        private void LockGuess_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSongTrack is not null &&
                GameLogic.IsCorrectGuess(_currentSongTrack.Title, _currentSongTrack.Artist, GuessInput.Text))
            {
                _score++;
            }
            ShowReveal();
        }

        private void ShowReveal()
        {
            if (_currentSongTrack is not null)
            {
                RevealTitle.Text = _currentSongTrack.Title;
                RevealArtist.Text = _currentSongTrack.Artist;
            }
            RevealPanel.Visibility = Visibility.Visible;
            NextTrackBtn.Visibility = Visibility.Visible;
            StatusLine.Text = "Needle up.";
            ScoreText.Text = _score.ToString();
        }

        private async void NextTrack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _round++;
                if (_round > TotalRounds)
                {
                    ScreenGame.Visibility = Visibility.Collapsed;
                    ScreenFinal.Visibility = Visibility.Visible;
                    FinalScoreText.Text = $"{_score}/{TotalRounds}";
                    await RenderSongLeaderboardAsync();
                    return;
                }
                StartNextRound();
            }
            catch (Exception ex)
            {
                // Belt-and-suspenders: RenderSongLeaderboardAsync already
                // catches its own errors, but this guarantees nothing from
                // this handler can ever reach WPF as an unhandled exception
                // (which otherwise hangs the window and then crashes it).
                // Owned by "this" explicitly — an unowned MessageBox can
                // appear off-screen or on the wrong monitor on some setups
                // while still being fully modal, which blocks all input to
                // the main window without ever looking like a dialog is up
                // (indistinguishable from a frozen app).
                MessageBox.Show(this, $"Something went wrong moving to the next screen:\n\n{ex.Message}",
                    "Needle Drop", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ===================== FINAL SCREEN (song mode) =====================

        /// <summary>
        /// Pulls the real leaderboard from NeedleDrop.Api's SQL-backed
        /// /api/db/songleaderboard endpoint (backend/Data/Db.cs — a real
        /// SELECT against SQL Server LocalDB, not mock data). The backend
        /// API is a separate process from this app, so if it isn't running
        /// this shows a friendly explanation instead of crashing the game.
        /// Shows a "Loading…" row immediately so a slow/unreachable backend
        /// reads as "still working" rather than "the app is frozen" — the
        /// actual network call is capped at 3 seconds either way (see
        /// LeaderboardApiClient), so this never hangs indefinitely.
        /// </summary>
        private async Task RenderSongLeaderboardAsync()
        {
            SongLeaderboardList.Children.Clear();
            SongLeaderboardList.Children.Add(BuildLeaderboardLoadingRow());
            try
            {
                var rows = await WithHardTimeoutAsync(_leaderboardClient.GetSongLeaderboardAsync());
                SongLeaderboardList.Children.Clear();
                foreach (var entry in rows.OrderByDescending(x => x.Score).Take(5))
                    SongLeaderboardList.Children.Add(BuildLeaderboardRow(entry.Initials, entry.Score));
            }
            catch (Exception ex)
            {
                SongLeaderboardList.Children.Clear();
                SongLeaderboardList.Children.Add(BuildLeaderboardErrorRow(ex));
            }
        }

        private async void SubmitSongScore_Click(object sender, RoutedEventArgs e)
        {
            var initials = GameLogic.NormalizeInitials(SongInitialsInput.Text);
            SongSubmitScoreBtn.IsEnabled = false;
            SongSubmitScoreBtn.Content = "Saving…";
            try
            {
                await WithHardTimeoutAsync(_leaderboardClient.PostSongScoreAsync(initials, _score));
                SongSubmitScoreBtn.Content = "Saved!";
            }
            catch (Exception ex)
            {
                SongSubmitScoreBtn.Content = "Couldn't save";
                SongSubmitScoreBtn.IsEnabled = true;
                System.Diagnostics.Debug.WriteLine($"Song leaderboard POST failed: {ex.Message}");
            }

            try
            {
                await RenderSongLeaderboardAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Song leaderboard refresh failed: {ex.Message}");
            }
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            SongSubmitScoreBtn.IsEnabled = true;
            SongSubmitScoreBtn.Content = "Submit";
            ScreenFinal.Visibility = Visibility.Collapsed;
            ScreenSource.Visibility = Visibility.Visible;
        }

        // ===================== STREAMS SHOWDOWN: SOURCE =====================

        private void StreamsSourceOption_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            foreach (var opt in new[] { StreamsOptPlaylist, StreamsOptBillboard })
                opt.IsChecked = ReferenceEquals(opt, clicked);

            StreamsPlaylistInputWrap.Visibility = ReferenceEquals(clicked, StreamsOptPlaylist) ? Visibility.Visible : Visibility.Collapsed;
            StreamsBillboardInputWrap.Visibility = ReferenceEquals(clicked, StreamsOptBillboard) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowStreamsSourceError(string message)
        {
            StreamsSourceErrorText.Text = message;
            StreamsSourceErrorBox.Visibility = Visibility.Visible;
        }

        private async void StartShowdown_Click(object sender, RoutedEventArgs e)
        {
            StreamsSourceErrorBox.Visibility = Visibility.Collapsed;
            _streak = 0;

            bool fromBillboard = StreamsOptBillboard.IsChecked == true;

            if (fromBillboard)
            {
                _usingRealPopularityData = false;

                var selectedYear = BillboardYearCombo.SelectedItem is ComboBoxItem item &&
                                    int.TryParse(item.Content?.ToString(), out var parsedYear)
                    ? parsedYear
                    : 2020;
                // 2026's chart wasn't knowable when this list was written — fall back to 2025.
                _billboardYear = BillboardYearHits.ContainsKey(selectedYear) ? selectedYear : 2025;

                StreamsSourceBadge.Text = $"BILLBOARD {_billboardYear} (ESTIMATED STREAMS)";

                ScreenStreamsSource.Visibility = Visibility.Collapsed;
                ScreenStreamsGame.Visibility = Visibility.Visible;
                ResetMatchupUi();
                return;
            }

            var startBtn = (Button)sender;
            startBtn.IsEnabled = false;
            var originalContent = startBtn.Content;
            startBtn.Content = "Loading tracks…";

            try
            {
                await EnsureFreshTokenAsync();

                var playlistInput = StreamsPlaylistUrlInput.Text.Trim();
                if (string.IsNullOrWhiteSpace(playlistInput))
                {
                    ShowStreamsSourceError("Paste a playlist URL or ID first.");
                    return;
                }

                var tracks = await _apiClient.GetPlaylistTracksAsync(playlistInput);
                if (tracks.Count < 2)
                {
                    ShowStreamsSourceError("That playlist doesn't have enough playable tracks (need at least 2).");
                    return;
                }

                // Spotify marked the Track "popularity" field Deprecated in its Feb 2026
                // API changes, and Development Mode apps can get back 0 for every track
                // instead of a real score. If that's happened, comparing "0 vs 0" would
                // silently always favor track A — tell the player instead of faking it.
                if (tracks.All(t => t.Popularity == 0))
                {
                    ShowStreamsSourceError(
                        "Spotify returned no popularity data for this playlist (it's a deprecated field, and " +
                        "Development Mode apps often don't get real values anymore). Real-data Streams Showdown " +
                        "can't compare these tracks meaningfully right now — try Billboard Year (demo data) instead.");
                    return;
                }

                _streamsPool = tracks;
                _usingRealPopularityData = true;
                StreamsSourceBadge.Text = "FROM YOUR PLAYLIST (REAL POPULARITY)";

                ScreenStreamsSource.Visibility = Visibility.Collapsed;
                ScreenStreamsGame.Visibility = Visibility.Visible;
                ResetMatchupUi();
            }
            catch (Exception ex)
            {
                ShowStreamsSourceError($"Couldn't load that playlist: {ex.Message}");
            }
            finally
            {
                startBtn.IsEnabled = true;
                startBtn.Content = originalContent;
            }
        }

        // ===================== STREAMS SHOWDOWN: GAME =====================

        private (string TitleA, string ArtistA, long ValueA, string TitleB, string ArtistB, long ValueB) GetNextMatchup()
        {
            if (_usingRealPopularityData && _streamsPool.Count >= 2)
            {
                // Completely random pairing — the brief calls for no deliberate
                // difficulty-tuning by picking close streaming/popularity numbers.
                var a = _streamsPool[_rng.Next(_streamsPool.Count)];
                SpotifyTrackInfo b;
                do { b = _streamsPool[_rng.Next(_streamsPool.Count)]; } while (b.Id == a.Id && _streamsPool.Count > 1);

                return (a.Title, a.Artist, a.Popularity, b.Title, b.Artist, b.Popularity);
            }

            // Random pairing from that year's curated real hits — same
            // no-deliberate-difficulty-tuning rule as the real-data branch above.
            var pool = BillboardYearHits.TryGetValue(_billboardYear, out var yearHits) ? yearHits : BillboardYearHits[2020];
            var songA = pool[_rng.Next(pool.Length)];
            (string Title, string Artist, long Streams) songB;
            do { songB = pool[_rng.Next(pool.Length)]; } while (songB.Title == songA.Title && pool.Length > 1);

            return (songA.Title, songA.Artist, songA.Streams, songB.Title, songB.Artist, songB.Streams);
        }

        private string FormatMatchupValue(long value) =>
            _usingRealPopularityData ? GameLogic.FormatPopularity((int)value) : GameLogic.FormatStreams(value);

        private void ResetMatchupUi()
        {
            var matchup = GetNextMatchup();
            _currentMatchup = matchup;

            CardATitle.Text = matchup.TitleA;
            CardAArtist.Text = matchup.ArtistA;
            CardAStreams.Text = FormatMatchupValue(matchup.ValueA);
            CardAStreams.Visibility = Visibility.Collapsed;

            CardBTitle.Text = matchup.TitleB;
            CardBArtist.Text = matchup.ArtistB;
            CardBStreams.Text = FormatMatchupValue(matchup.ValueB);
            CardBStreams.Visibility = Visibility.Collapsed;

            StreamsCardA.IsEnabled = true;
            StreamsCardB.IsEnabled = true;
            StreamsCardA.ClearValue(Button.BorderBrushProperty);
            StreamsCardB.ClearValue(Button.BorderBrushProperty);

            StreamsStatusLine.Text = "Tap a card to lock your pick";
            StreakText.Text = _streak.ToString();
            NextMatchupBtn.Visibility = Visibility.Collapsed;
        }

        private void StreamsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked || _currentMatchup is null) return;

            var matchup = _currentMatchup.Value;
            bool pickedA = ReferenceEquals(clicked, StreamsCardA);
            bool aWon = GameLogic.DoesTrackAWin(matchup.ValueA, matchup.ValueB);
            bool correct = pickedA ? aWon : !aWon;

            StreamsCardA.IsEnabled = false;
            StreamsCardB.IsEnabled = false;
            CardAStreams.Visibility = Visibility.Visible;
            CardBStreams.Visibility = Visibility.Visible;

            var success = (Brush)FindResource("SuccessBrush");
            var error = (Brush)FindResource("ErrorBrush");
            StreamsCardA.BorderBrush = aWon ? success : error;
            StreamsCardB.BorderBrush = aWon ? error : success;

            if (correct)
            {
                _streak++;
                StreamsStatusLine.Text = "Nice call. Streak's alive.";
            }
            else
            {
                StreamsStatusLine.Text = $"That's the run — final streak: {_streak}";
            }

            StreakText.Text = _streak.ToString();
            NextMatchupBtn.Visibility = Visibility.Visible;
            NextMatchupBtn.Content = correct ? "Next matchup →" : "See final streak →";
        }

        private async void NextMatchup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool lastWasWrong = NextMatchupBtn.Content as string == "See final streak →";
                if (lastWasWrong)
                {
                    ScreenStreamsGame.Visibility = Visibility.Collapsed;
                    ScreenStreamsFinal.Visibility = Visibility.Visible;
                    FinalStreakText.Text = _streak.ToString();
                    await RenderStreamsLeaderboardAsync();
                    return;
                }

                ResetMatchupUi();
            }
            catch (Exception ex)
            {
                // Owned by "this" explicitly — an unowned MessageBox can
                // appear off-screen or on the wrong monitor on some setups
                // while still being fully modal, which blocks all input to
                // the main window without ever looking like a dialog is up
                // (indistinguishable from a frozen app).
                MessageBox.Show(this, $"Something went wrong moving to the next screen:\n\n{ex.Message}",
                    "Needle Drop", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ===================== STREAMS SHOWDOWN: FINAL =====================

        /// <summary>Same real SQL-backed pull as RenderSongLeaderboardAsync, against /api/db/streamsleaderboard.</summary>
        private async Task RenderStreamsLeaderboardAsync()
        {
            StreamsLeaderboardList.Children.Clear();
            StreamsLeaderboardList.Children.Add(BuildLeaderboardLoadingRow());
            try
            {
                var rows = await WithHardTimeoutAsync(_leaderboardClient.GetStreamsLeaderboardAsync());
                StreamsLeaderboardList.Children.Clear();
                foreach (var entry in rows.OrderByDescending(x => x.Score).Take(5))
                    StreamsLeaderboardList.Children.Add(BuildLeaderboardRow(entry.Initials, entry.Score));
            }
            catch (Exception ex)
            {
                StreamsLeaderboardList.Children.Clear();
                StreamsLeaderboardList.Children.Add(BuildLeaderboardErrorRow(ex));
            }
        }

        private async void SubmitStreamsScore_Click(object sender, RoutedEventArgs e)
        {
            var initials = GameLogic.NormalizeInitials(StreamsInitialsInput.Text);
            StreamsSubmitScoreBtn.IsEnabled = false;
            StreamsSubmitScoreBtn.Content = "Saving…";
            try
            {
                await WithHardTimeoutAsync(_leaderboardClient.PostStreamsScoreAsync(initials, _streak));
                StreamsSubmitScoreBtn.Content = "Saved!";
            }
            catch (Exception ex)
            {
                StreamsSubmitScoreBtn.Content = "Couldn't save";
                StreamsSubmitScoreBtn.IsEnabled = true;
                System.Diagnostics.Debug.WriteLine($"Streams leaderboard POST failed: {ex.Message}");
            }

            try
            {
                await RenderStreamsLeaderboardAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Streams leaderboard refresh failed: {ex.Message}");
            }
        }

        private void PlayStreamsAgain_Click(object sender, RoutedEventArgs e)
        {
            StreamsSubmitScoreBtn.IsEnabled = true;
            StreamsSubmitScoreBtn.Content = "Submit";
            ScreenStreamsFinal.Visibility = Visibility.Collapsed;
            ScreenStreamsSource.Visibility = Visibility.Visible;
        }

        // ===================== SHARED =====================

        private Grid BuildLeaderboardRow(string initials, int value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = initials,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextBrush"),
                FontSize = 13
            };
            var score = new TextBlock
            {
                Text = value.ToString(),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("GoldBrush"),
                FontSize = 13
            };

            Grid.SetColumn(name, 0);
            Grid.SetColumn(score, 1);
            grid.Children.Add(name);
            grid.Children.Add(score);
            return grid;
        }

        /// <summary>
        /// Shown in place of leaderboard rows when NeedleDrop.Api can't be
        /// reached — most commonly because the backend project just isn't
        /// running. This is a separate process from the WPF app (see
        /// README's "Running the backend alongside the front end"), so it's
        /// a genuinely common state, not a bug, and shouldn't crash the game.
        /// </summary>
        private TextBlock BuildLeaderboardErrorRow(Exception ex)
        {
            var isConnectionIssue = ex is HttpRequestException or TaskCanceledException or OperationCanceledException;
            return new TextBlock
            {
                Text = isConnectionIssue
                    ? "Couldn't reach the leaderboard — make sure the NeedleDrop.Api backend project is also running (see README)."
                    : $"Couldn't load the leaderboard: {ex.Message}",
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };
        }

        /// <summary>
        /// Placeholder shown the instant a leaderboard fetch starts, so a
        /// slow or unreachable backend reads as "still loading" rather than
        /// a frozen window — the fetch itself is hard-capped at 3 seconds
        /// (LeaderboardApiClient), so this is never on screen for long.
        /// </summary>
        private TextBlock BuildLeaderboardLoadingRow() => new()
        {
            Text = "Loading leaderboard…",
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
        };

        /// <summary>
        /// Second, independent layer of "this can never hang forever" on
        /// top of LeaderboardApiClient's own internal 3-second cancellation:
        /// races the real call against a plain Task.Delay from OUT HERE, at
        /// the call site. If the inner call's own cancellation somehow
        /// doesn't fire (or fires but the Task it was racing against never
        /// actually unblocks — e.g. a background thread stuck in a genuinely
        /// uncancellable native call), this still guarantees the UI moves on
        /// within ~4 seconds, at the cost of a leaked background task rather
        /// than a frozen window. A frozen window is worse than a leaked task.
        /// </summary>
        private static async Task<T> WithHardTimeoutAsync<T>(Task<T> task)
        {
            var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(4)));
            if (winner != task)
                throw new TimeoutException("The leaderboard request took too long and was given up on.");
            return await task; // already completed — rethrows if it faulted
        }

        private static async Task WithHardTimeoutAsync(Task task)
        {
            var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(4)));
            if (winner != task)
                throw new TimeoutException("The leaderboard request took too long and was given up on.");
            await task; // already completed — rethrows if it faulted
        }
    }
}
