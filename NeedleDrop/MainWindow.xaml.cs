using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NeedleDrop
{
    /// <summary>
    ///
    /// Everything here is UI-only lol.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ---------- Guess the Song state ----------
        private int _round = 1;
        private const int TotalRounds = 10;
        private int _score = 0;
        private string _guessMode = "multiple"; // multiple is song or artist

        private readonly List<(string Initials, int Score)> _songLeaderboard = new()
        {
            ("JWB", 8), ("ALT", 7), ("MRQ", 6)
        };

        // ---------- Streams Showdown state ----------
        private int _streak = 0;
        private int _matchup = 0;

        // Mock ups of variables (titleA, artistA, streamsA, titleB, artistB, streamsB)
        private static readonly (string, string, int, string, string, int)[] SampleMatchups = new[]
        {
            ("Sample Track A", "Artist One", 812_000_000, "Sample Track B", "Artist Two", 640_000_000),
            ("Sample Track C", "Artist Three", 1_204_000_000, "Sample Track D", "Artist Four", 990_000_000),
            ("Sample Track E", "Artist Five", 455_000_000, "Sample Track F", "Artist Six", 610_000_000),
            ("Sample Track G", "Artist Seven", 2_010_000_000, "Sample Track H", "Artist Eight", 1_875_000_000),
        };

        private readonly List<(string Initials, int Streak)> _streamsLeaderboard = new()
        {
            ("JWB", 83), ("ABC", 61), ("QRS", 44)
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        // ===================== MODE SELECT =====================

        private void ChooseSongMode_Click(object sender, RoutedEventArgs e)
        {
            ScreenModeSelect.Visibility = Visibility.Collapsed;
            ShowWizardStep(1);
            ScreenSetup.Visibility = Visibility.Visible;
        }

        private void ChooseStreamsMode_Click(object sender, RoutedEventArgs e)
        {
            ScreenModeSelect.Visibility = Visibility.Collapsed;
            ScreenStreamsSource.Visibility = Visibility.Visible;
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
            catch { /* front end only here :p*/ }
        }

        private void CopyRedirect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("http://127.0.0.1:8080/callback");
                CopyRedirectBtn.Content = "Copied!";
            }
            catch { /* blehhhh fill the space */ }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            ScreenSetup.Visibility = Visibility.Collapsed;
            ScreenSource.Visibility = Visibility.Visible;
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
            ScreenSource.Visibility = Visibility.Collapsed;
            ShowWizardStep(1);
            ScreenSetup.Visibility = Visibility.Visible;
        }

        private void LoadTracklist_Click(object sender, RoutedEventArgs e)
        {
            _round = 1;
            _score = 0;
            ScreenSource.Visibility = Visibility.Collapsed;
            ScreenGame.Visibility = Visibility.Visible;
            DailyBadge.Visibility = OptDaily.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ResetRoundUi();
        }

        // ===================== GAME SCREEN (song mode) =====================

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

        private void PlayNeedle_Click(object sender, RoutedEventArgs e)
        {
            StatusLine.Text = "Spinning…";
            PlayBtn.IsEnabled = false;

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

            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                StatusLine.Text = "Your guess?";
                PlayBtn.Visibility = Visibility.Collapsed;

                if (_guessMode == "multiple")
                    ChoicesPanel.Visibility = Visibility.Visible;
                else
                {
                    GuessPanel.Visibility = Visibility.Visible;
                    GuessInput.Focus();
                }
            };
            timer.Start();
        }

        private void Choice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;

            bool isCorrect = ReferenceEquals(clicked, ChoicesPanel.Children[0]);
            foreach (var child in ChoicesPanel.Children)
                if (child is Button b) b.IsEnabled = false;

            clicked.BorderBrush = isCorrect ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush");
            if (isCorrect) _score++;
            ShowReveal();
        }

        private void LockGuess_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(GuessInput.Text)) _score++;
            ShowReveal();
        }

        private void ShowReveal()
        {
            RevealPanel.Visibility = Visibility.Visible;
            NextTrackBtn.Visibility = Visibility.Visible;
            StatusLine.Text = "Needle up.";
            ScoreText.Text = _score.ToString();
        }

        private void NextTrack_Click(object sender, RoutedEventArgs e)
        {
            _round++;
            if (_round > TotalRounds)
            {
                ScreenGame.Visibility = Visibility.Collapsed;
                ScreenFinal.Visibility = Visibility.Visible;
                FinalScoreText.Text = $"{_score}/{TotalRounds}";
                RenderSongLeaderboard();
                return;
            }
            ResetRoundUi();
        }

        // ===================== FINAL SCREEN (song mode) =====================

        private void RenderSongLeaderboard()
        {
            SongLeaderboardList.Children.Clear();
            foreach (var entry in _songLeaderboard.OrderByDescending(x => x.Score).Take(5))
                SongLeaderboardList.Children.Add(BuildLeaderboardRow(entry.Initials, entry.Score));
        }

        private void SubmitSongScore_Click(object sender, RoutedEventArgs e)
        {
            var initials = string.IsNullOrWhiteSpace(SongInitialsInput.Text) ? "YOU" : SongInitialsInput.Text.ToUpperInvariant();
            _songLeaderboard.Add((initials, _score));
            SongSubmitScoreBtn.IsEnabled = false;
            SongSubmitScoreBtn.Content = "Saved!";
            RenderSongLeaderboard();
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

        private void StartShowdown_Click(object sender, RoutedEventArgs e)
        {
            _streak = 0;
            _matchup = 0;

            bool fromBillboard = StreamsOptBillboard.IsChecked == true;
            if (fromBillboard && BillboardYearCombo.SelectedItem is ComboBoxItem item)
                StreamsSourceBadge.Text = $"BILLBOARD {item.Content}";
            else
                StreamsSourceBadge.Text = "FROM YOUR PLAYLIST";

            ScreenStreamsSource.Visibility = Visibility.Collapsed;
            ScreenStreamsGame.Visibility = Visibility.Visible;
            ResetMatchupUi();
        }

        // ===================== STREAMS SHOWDOWN: GAME =====================

        private void ResetMatchupUi()
        {
            var (titleA, artistA, streamsA, titleB, artistB, streamsB) = SampleMatchups[_matchup % SampleMatchups.Length];

            CardATitle.Text = titleA;
            CardAArtist.Text = artistA;
            CardAStreams.Text = FormatStreams(streamsA);
            CardAStreams.Visibility = Visibility.Collapsed;

            CardBTitle.Text = titleB;
            CardBArtist.Text = artistB;
            CardBStreams.Text = FormatStreams(streamsB);
            CardBStreams.Visibility = Visibility.Collapsed;

            StreamsCardA.IsEnabled = true;
            StreamsCardB.IsEnabled = true;
            StreamsCardA.ClearValue(Button.BorderBrushProperty);
            StreamsCardB.ClearValue(Button.BorderBrushProperty);

            StreamsStatusLine.Text = "Tap a card to lock your pick";
            StreakText.Text = _streak.ToString();
            NextMatchupBtn.Visibility = Visibility.Collapsed;
        }

        private static string FormatStreams(int streams) => streams >= 1_000_000_000
            ? $"{streams / 1_000_000_000.0:0.00}B streams"
            : $"{streams / 1_000_000.0:0}M streams";

        private void StreamsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;

            var (_, _, streamsA, _, _, streamsB) = SampleMatchups[_matchup % SampleMatchups.Length];
            bool pickedA = ReferenceEquals(clicked, StreamsCardA);
            bool correct = pickedA ? streamsA >= streamsB : streamsB >= streamsA;

            StreamsCardA.IsEnabled = false;
            StreamsCardB.IsEnabled = false;
            CardAStreams.Visibility = Visibility.Visible;
            CardBStreams.Visibility = Visibility.Visible;

            var success = (Brush)FindResource("SuccessBrush");
            var error = (Brush)FindResource("ErrorBrush");
            bool aWon = streamsA >= streamsB;
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

        private void NextMatchup_Click(object sender, RoutedEventArgs e)
        {
            bool lastWasWrong = NextMatchupBtn.Content as string == "See final streak →";
            if (lastWasWrong)
            {
                ScreenStreamsGame.Visibility = Visibility.Collapsed;
                ScreenStreamsFinal.Visibility = Visibility.Visible;
                FinalStreakText.Text = _streak.ToString();
                RenderStreamsLeaderboard();
                return;
            }

            _matchup++;
            ResetMatchupUi();
        }

        // ===================== STREAMS SHOWDOWN: FINAL =====================

        private void RenderStreamsLeaderboard()
        {
            StreamsLeaderboardList.Children.Clear();
            foreach (var entry in _streamsLeaderboard.OrderByDescending(x => x.Streak).Take(5))
                StreamsLeaderboardList.Children.Add(BuildLeaderboardRow(entry.Initials, entry.Streak));
        }

        private void SubmitStreamsScore_Click(object sender, RoutedEventArgs e)
        {
            var initials = string.IsNullOrWhiteSpace(StreamsInitialsInput.Text) ? "YOU" : StreamsInitialsInput.Text.ToUpperInvariant();
            _streamsLeaderboard.Add((initials, _streak));
            StreamsSubmitScoreBtn.IsEnabled = false;
            StreamsSubmitScoreBtn.Content = "Saved!";
            RenderStreamsLeaderboard();
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
    }
}
