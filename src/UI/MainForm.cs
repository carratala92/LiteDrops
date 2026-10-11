using System.Diagnostics;
using LiteDrops.Mining;
using LiteDrops.Twitch;

namespace LiteDrops.UI;

sealed class MainForm : Form
{
    readonly AppSettings _settings;
    readonly TwitchApi _api;
    readonly Icon _icon;

    // header
    readonly StatusPill _status = new() { Text = "Starting", Size = new Size(150, 34) };
    // now watching
    readonly CardPanel _watchCard = new() { Heading = "Now watching", Dock = DockStyle.Top, Height = 204 };
    readonly Panel _watchView = new() { BackColor = Theme.Card };
    readonly Label _channel = MakeLabel(Theme.Big, Theme.Text);
    readonly Label _game = MakeLabel(Theme.Body, Theme.SubText);
    readonly Label _drop = MakeLabel(Theme.BodyBold, Theme.Text);
    readonly SlimProgress _progress = new() { Height = 10 };
    readonly Label _minutes = MakeLabel(Theme.Body, Theme.Text);
    readonly Label _percent = MakeLabel(Theme.BodyBold, Theme.Accent, ContentAlignment.MiddleRight);
    readonly Label _campaign = MakeLabel(Theme.Small, Theme.SubText);
    // login
    readonly Panel _loginView = new() { BackColor = Theme.Card, Visible = false };
    readonly Label _loginText = MakeLabel(Theme.Body, Theme.SubText);
    readonly TextBox _loginCode = new()
    {
        ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text,
        Font = Theme.Code, TabStop = false,
    };
    readonly AccentButton _copyButton = new() { Text = "Copy", Size = new Size(80, 34), Visible = false };
    readonly AccentButton _loginButton = new() { Text = "Log in with Twitch", Size = new Size(170, 36) };
    // games
    readonly CardPanel _gamesCard = new() { Heading = "Games", Dock = DockStyle.Fill };
    readonly GameListBox _gameList = new();
    readonly DarkTextBox _gameInput = new() { Height = 38 };
    readonly AccentButton _addButton = new() { Text = "Add", Size = new Size(76, 38) };
    readonly Label _gamesHint = MakeLabel(Theme.Small, Theme.SubText);
    readonly SuggestionListBox _suggestions = new() { Visible = false };
    readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };
    CancellationTokenSource? _searchCts;
    // campaigns
    readonly CardPanel _campaignCard = new() { Heading = "Campaigns", Dock = DockStyle.Bottom, Height = 196 };
    readonly CampaignListBox _campaignList = new();
    readonly Label _campaignEmpty = MakeLabel(Theme.Small, Theme.SubText);
    // footer
    readonly Label _user = MakeLabel(Theme.Body, Theme.SubText);
    readonly LinkLabel _logout = new()
    {
        Text = "Log out", AutoSize = true, Font = Theme.BodyBold, LinkColor = Theme.Accent,
        ActiveLinkColor = Theme.AccentDim, LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Theme.Background,
    };
    readonly Label _ram = MakeLabel(Theme.Body, Theme.SubText, ContentAlignment.MiddleRight);

    readonly NotifyIcon _tray;
    readonly System.Windows.Forms.Timer _statsTimer = new() { Interval = 3000 };

    Miner? _miner;
    CancellationTokenSource? _minerCts;
    CancellationTokenSource? _loginCts;
    bool _exiting;
    DateTime _lastTrim;

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        _api = new TwitchApi(settings.DeviceId) { AccessToken = settings.AccessToken };
        _icon = LoadIcon();

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "LiteDrops";
        Icon = _icon;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        ClientSize = new Size(480, 760);
        MinimumSize = new Size(440, 640);
        Padding = new Padding(14, 0, 14, 0);
        StartPosition = FormStartPosition.CenterScreen;

        BuildHeader();
        BuildWatchCard();
        BuildGamesCard();
        BuildCampaignCard();
        var footer = BuildFooter();

        // docking order: last added is laid out first
        Controls.Add(_gamesCard);
        Controls.Add(Spacer(DockStyle.Top));
        Controls.Add(_watchCard);
        Controls.Add(_header);
        Controls.Add(Spacer(DockStyle.Bottom));
        Controls.Add(_campaignCard);
        Controls.Add(Spacer(DockStyle.Bottom));
        Controls.Add(footer);
        ResumeLayout(false);

        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "LiteDrops",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _tray.ContextMenuStrip.Items.Add("Show", null, (_, _) => ShowFromTray());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.DoubleClick += (_, _) => ShowFromTray();

        _statsTimer.Tick += (_, _) => UpdateStats();
        _statsTimer.Start();
        Log.Written += line => Debug.WriteLine(line);
    }

    readonly Panel _header = new() { Dock = DockStyle.Top, Height = 66, BackColor = Theme.Background };

    void BuildHeader()
    {
        var logo = new PictureBox
        {
            Image = new Icon(_icon, 64, 64).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(0, 15, 36, 36),
        };
        var title = new Label
        {
            Text = "LiteDrops", Font = Theme.Title, ForeColor = Theme.Text, AutoSize = true,
            Location = new Point(42, 14), BackColor = Theme.Background,
        };
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _status.Location = new Point(_header.Width - _status.Width, 16);
        _header.Controls.AddRange([logo, title, _status]);
        _header.Resize += (_, _) => _status.Left = _header.ClientSize.Width - _status.Width;
    }

    void BuildWatchCard()
    {
        _watchView.Dock = DockStyle.Fill;
        _loginView.Dock = DockStyle.Fill;
        _watchCard.Padding = new Padding(16, 38, 16, 12);
        _channel.SetBounds(0, 0, 400, 30);
        _game.SetBounds(0, 32, 400, 22);
        _drop.SetBounds(0, 56, 400, 22);
        _progress.SetBounds(0, 88, 360, 10);
        _percent.SetBounds(360, 80, 50, 24);
        _minutes.SetBounds(0, 104, 200, 22);
        _campaign.SetBounds(0, 126, 400, 20);
        foreach (var c in new Control[] { _channel, _game, _drop, _campaign })
            c.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _percent.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _watchView.Controls.AddRange([_channel, _game, _drop, _progress, _percent, _minutes, _campaign]);
        _watchView.Resize += (_, _) =>
        {
            var w = _watchView.ClientSize.Width;
            _percent.Left = w - _percent.Width;
            _progress.Width = Math.Max(10, _percent.Left - 8);
        };

        _loginText.SetBounds(0, 0, 410, 44);
        _loginText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _loginText.Text = "Log in with your Twitch account to start mining.";
        _loginCode.SetBounds(0, 50, 190, 40);
        _copyButton.Location = new Point(200, 52);
        _copyButton.Click += (_, _) =>
        {
            if (_loginCode.Text.Length == 0) return;
            Clipboard.SetText(_loginCode.Text);
            _copyButton.Text = "Copied";
        };
        _loginButton.Location = new Point(0, 92);
        _loginButton.Click += async (_, _) => await LoginAsync();
        _loginView.Controls.AddRange([_loginText, _loginCode, _copyButton, _loginButton]);

        _watchCard.Controls.AddRange([_watchView, _loginView]);
    }

    void BuildGamesCard()
    {
        _gamesCard.Padding = new Padding(16, 38, 16, 14);
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
        _gameList.SetBounds(0, 0, 230, 200);
        _gameList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        _gameList.SelectionChanged += GamesChanged;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Move up", null, (_, _) => MoveGame(-1));
        menu.Items.Add("Move down", null, (_, _) => MoveGame(+1));
        menu.Items.Add("Remove", null, (_, _) => RemoveGame());
        _gameList.ContextMenuStrip = menu;
        _gameList.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var index = _gameList.IndexFromPoint(e.Location);
            if (index >= 0) _gameList.SelectedIndex = index;
        };

        _gamesHint.Text = "Checked games are mined, top to bottom.\nRight-click a game to reorder or remove it.\n\nType below to search Twitch for a game.";
        _gamesHint.AutoSize = false;
        _gamesHint.TextAlign = ContentAlignment.TopLeft;
        _gameInput.Box.PlaceholderText = "Add game…";
        _gameInput.Box.KeyDown += (_, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    AddGame();
                    break;
                case Keys.Down when _suggestions.Visible:
                    e.Handled = true;
                    _suggestions.SelectedIndex = Math.Min(_suggestions.Items.Count - 1, _suggestions.SelectedIndex + 1);
                    break;
                case Keys.Up when _suggestions.Visible:
                    e.Handled = true;
                    _suggestions.SelectedIndex = Math.Max(-1, _suggestions.SelectedIndex - 1);
                    break;
                case Keys.Escape:
                    HideSuggestions();
                    break;
            }
        };
        _gameInput.Box.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            if (_gameInput.Box.Text.Trim().Length >= 2) _searchTimer.Start();
            else HideSuggestions();
        };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await SearchGamesAsync(_gameInput.Box.Text.Trim());
        };
        _suggestions.MouseClick += (_, e) =>
        {
            if (_suggestions.IndexFromPoint(e.Location) >= 0) AddGame();
        };
        _addButton.Click += (_, _) => AddGame();
        body.Controls.AddRange([_gameList, _suggestions, _gamesHint, _gameInput, _addButton]);
        body.Resize += (_, _) =>
        {
            var w = body.ClientSize.Width;
            var h = body.ClientSize.Height;
            var listW = (int)(w * 0.48);
            _gameList.SetBounds(0, 0, listW, h);
            var rightX = listW + 12;
            var rightW = w - rightX;
            _addButton.SetBounds(w - _addButton.Width, h - _addButton.Height, _addButton.Width, _addButton.Height);
            _gameInput.SetBounds(rightX, h - _gameInput.Height, rightW - _addButton.Width - 8, _gameInput.Height);
            _gamesHint.SetBounds(rightX, 2, rightW, Math.Max(0, _gameInput.Top - 8));
            _suggestions.SetBounds(rightX, 0, rightW, Math.Max(0, _gameInput.Top - 6));
        };
        _gamesCard.Controls.Add(body);
        ReloadGames();
    }

    void BuildCampaignCard()
    {
        _campaignCard.Padding = new Padding(16, 38, 12, 10);
        _campaignList.Dock = DockStyle.Fill;
        _campaignEmpty.Dock = DockStyle.Fill;
        _campaignEmpty.TextAlign = ContentAlignment.MiddleCenter;
        _campaignEmpty.Text = "No campaigns found yet";
        _campaignCard.Controls.AddRange([_campaignList, _campaignEmpty]);
        UpdateCampaignEmpty();
    }

    Panel BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Background };
        _user.SetBounds(0, 10, 220, 24);
        _user.AutoSize = true;
        _logout.Location = new Point(230, 13);
        _logout.LinkClicked += (_, _) => Logout();
        _ram.SetBounds(300, 10, 150, 24);
        _ram.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        footer.Controls.AddRange([_logout, _user, _ram]);
        footer.Resize += (_, _) => _ram.Left = footer.ClientSize.Width - _ram.Width;
        return footer;
    }

    static Label MakeLabel(Font font, Color color, ContentAlignment align = ContentAlignment.MiddleLeft) => new()
    {
        Font = font, ForeColor = color, BackColor = Color.Transparent, TextAlign = align,
        AutoSize = false, AutoEllipsis = true, UseMnemonic = false,
    };

    static Panel Spacer(DockStyle dock) => new() { Dock = dock, Height = 12, BackColor = Theme.Background };

    static Icon LoadIcon()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    // ------------------------------------------------------------ lifecycle

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyDarkTitleBar(Handle);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ShowWatching(null);
        TwitchUser? user = null;
        try
        {
            user = await _api.ValidateAsync(CancellationToken.None);
        }
        catch (HttpRequestException ex)
        {
            SetStatus("Offline", Theme.Red);
            _loginText.Text = $"Can't reach Twitch: {ex.Message}";
        }
        if (user is null) ShowLogin();
        else StartMiner(user);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && !_exiting) HideToTray();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && _settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _exiting = true;
        _minerCts?.Cancel();
        _loginCts?.Cancel();
        _tray.Visible = false;
        base.OnFormClosing(e);
    }

    void HideToTray()
    {
        Hide();
        Memory.Trim();
        _lastTrim = DateTime.UtcNow;
    }

    void ShowFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void ExitApp()
    {
        _exiting = true;
        Close();
    }

    // ------------------------------------------------------------ login

    void ShowLogin()
    {
        _watchView.Visible = false;
        _loginView.Visible = true;
        _loginCode.Text = "";
        _copyButton.Visible = false;
        _loginButton.Visible = true;
        _loginButton.Enabled = true;
        _user.Text = "Not logged in";
        _logout.Visible = false;
        SetStatus("Logged out", Theme.SubText);
    }

    async Task LoginAsync()
    {
        _loginCts?.Cancel();
        _loginCts = new CancellationTokenSource();
        var ct = _loginCts.Token;
        _loginButton.Enabled = false;
        try
        {
            var code = await _api.StartDeviceLoginAsync(ct);
            _loginText.Text = "Enter this code at twitch.tv/activate (opened in your browser), then confirm:";
            _loginCode.Text = code.UserCode;
            _copyButton.Text = "Copy";
            _copyButton.Visible = true;
            _loginButton.Visible = false;
            Clipboard.SetText(code.UserCode);
            Process.Start(new ProcessStartInfo(code.VerificationUri.ToString()) { UseShellExecute = true });

            string? token = null;
            while (token is null && DateTime.UtcNow < code.ExpiresAt)
            {
                await Task.Delay(code.Interval, ct);
                token = await _api.PollDeviceLoginAsync(code, ct);
            }
            if (token is null)
            {
                _loginText.Text = "The code expired. Please try again.";
                ShowLogin();
                return;
            }
            _api.AccessToken = token;
            var user = await _api.ValidateAsync(ct) ?? throw new TwitchApiException("Login was not accepted");
            _settings.AccessToken = token;
            _settings.Save();
            Log.Info("Logged in");
            StartMiner(user);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is TwitchApiException or HttpRequestException)
        {
            Log.Error($"Login failed: {ex.Message}");
            _loginText.Text = $"Login failed: {ex.Message}";
            ShowLogin();
        }
    }

    void Logout()
    {
        _minerCts?.Cancel();
        _miner = null;
        _api.AccessToken = null;
        _settings.AccessToken = null;
        _settings.Save();
        Log.Info("Logged out");
        _loginText.Text = "Log in with your Twitch account to start mining.";
        _campaignList.Items.Clear();
        UpdateCampaignEmpty();
        ShowLogin();
    }

    // ------------------------------------------------------------ mining

    void StartMiner(TwitchUser user)
    {
        _loginView.Visible = false;
        _watchView.Visible = true;
        _user.Text = $"Logged in as {user.Login}";
        _logout.Visible = true;
        _logout.Left = Math.Min(_user.Left + TextRenderer.MeasureText(_user.Text, _user.Font).Width + 12, _ram.Left - _logout.Width);

        _minerCts?.Cancel();
        _minerCts = new CancellationTokenSource();
        var ct = _minerCts.Token;
        var miner = new Miner(_api, _settings, user);
        _miner = miner;
        miner.Changed += snapshot =>
        {
            if (IsHandleCreated && !_exiting) BeginInvoke(() => { if (_miner == miner) Render(snapshot); });
        };
        _ = Task.Run(async () =>
        {
            try
            {
                await miner.RunAsync(ct);
            }
            catch (AuthExpiredException)
            {
                BeginInvoke(() =>
                {
                    _settings.AccessToken = null;
                    _settings.Save();
                    _loginText.Text = "Your login expired. Please log in again.";
                    ShowLogin();
                });
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Log.Error($"Miner stopped: {ex}");
                BeginInvoke(() => SetStatus("Stopped", Theme.Red));
            }
        }, ct);
    }

    void Render(MinerSnapshot snapshot)
    {
        var (text, color) = snapshot.Status switch
        {
            MinerStatus.Mining => ("Mining", Theme.Green),
            MinerStatus.Idle => ("Idle", Theme.Amber),
            MinerStatus.Starting => ("Searching", Theme.Accent),
            MinerStatus.Error => ("Error", Theme.Red),
            _ => ("Logged out", Theme.SubText),
        };
        SetStatus(text, color);
        if (snapshot.Status == MinerStatus.LoginRequired) return;
        ShowWatching(snapshot.Watching, snapshot.StatusText);

        _campaignList.BeginUpdate();
        _campaignList.Items.Clear();
        foreach (var row in snapshot.Campaigns) _campaignList.Items.Add(row);
        _campaignList.EndUpdate();
        UpdateCampaignEmpty();

        var tip = snapshot.Watching is { } w
            ? $"LiteDrops — {w.GameName} {w.CurrentMinutes}/{w.RequiredMinutes} min"
            : $"LiteDrops — {snapshot.StatusText}";
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
    }

    void ShowWatching(WatchInfo? w, string? idleText = null)
    {
        if (w is null)
        {
            _channel.Text = "Not watching";
            _game.Text = idleText ?? "";
            _drop.Text = "";
            _progress.Value = 0;
            _minutes.Text = "";
            _percent.Text = "";
            _campaign.Text = "";
            return;
        }
        var ratio = w.RequiredMinutes == 0 ? 0 : (double)w.CurrentMinutes / w.RequiredMinutes;
        _channel.Text = w.Channel.DisplayName;
        _game.Text = w.GameName;
        _drop.Text = w.DropName;
        _progress.Value = ratio;
        _minutes.Text = $"{w.CurrentMinutes} / {w.RequiredMinutes} min";
        _percent.Text = $"{ratio:0%}";
        _campaign.Text = $"{w.CampaignName} · ends {FormatEnds(w.CampaignEndAt)}";
    }

    static string FormatEnds(DateTime endAt)
    {
        var left = endAt - DateTime.UtcNow;
        return left.TotalDays >= 2 ? $"in {(int)left.TotalDays} days"
            : left.TotalHours >= 2 ? $"in {(int)left.TotalHours} hours"
            : left.TotalMinutes > 0 ? $"in {(int)left.TotalMinutes} min"
            : "soon";
    }

    void UpdateCampaignEmpty()
    {
        _campaignEmpty.Visible = _campaignList.Items.Count == 0;
        _campaignList.Visible = !_campaignEmpty.Visible;
    }

    void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.DotColor = color;
    }

    void UpdateStats()
    {
        _ram.Text = $"RAM {Memory.PrivateWorkingSetMB():0} MB";
        if (!Visible && DateTime.UtcNow - _lastTrim > TimeSpan.FromMinutes(5))
        {
            Memory.Trim();
            _lastTrim = DateTime.UtcNow;
        }
    }

    // ------------------------------------------------------------ games

    void ReloadGames()
    {
        var selected = _gameList.SelectedItem;
        _gameList.BeginUpdate();
        _gameList.Items.Clear();
        foreach (var g in _settings.Games) _gameList.Items.Add(g);
        if (selected is not null && _gameList.Items.Contains(selected)) _gameList.SelectedItem = selected;
        if (_gameList.SelectedIndex < 0 && _gameList.Items.Count > 0) _gameList.TopIndex = 0;
        _gameList.EndUpdate();
    }

    void AddGame()
    {
        var typed = _gameInput.Box.Text.Trim();
        // prefer Twitch's spelling: the highlighted suggestion, or an exact match among them
        var name = _suggestions.Visible && _suggestions.SelectedItem is string picked
            ? picked
            : _suggestions.Items.Cast<string>().FirstOrDefault(n => n.Equals(typed, StringComparison.OrdinalIgnoreCase))
              ?? typed;
        if (name.Length == 0) return;
        _gameInput.Box.Clear();
        HideSuggestions();
        var existing = _settings.Games.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Enabled = true;
        }
        else
        {
            // copy-on-write: the miner may be enumerating the current list on another thread
            _settings.Games = [.. _settings.Games, new GameEntry { Name = name }];
        }
        ReloadGames();
        GamesChanged();
    }

    async Task SearchGamesAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;
        try
        {
            var names = await _api.SearchGamesAsync(query, ct);
            if (ct.IsCancellationRequested || _gameInput.Box.Text.Trim() != query) return;
            _suggestions.BeginUpdate();
            _suggestions.Items.Clear();
            foreach (var n in names) _suggestions.Items.Add(n);
            _suggestions.EndUpdate();
            _suggestions.Visible = names.Count > 0;
            _gamesHint.Visible = !_suggestions.Visible;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is TwitchApiException or HttpRequestException or AuthExpiredException)
        {
            Log.Warn($"Game search failed: {ex.Message}");
        }
    }

    void HideSuggestions()
    {
        _searchCts?.Cancel();
        _suggestions.Items.Clear();
        _suggestions.Visible = false;
        _gamesHint.Visible = true;
    }

    void RemoveGame()
    {
        if (_gameList.SelectedItem is not GameEntry game) return;
        _settings.Games = _settings.Games.Where(g => g != game).ToList();
        ReloadGames();
        GamesChanged();
    }

    void MoveGame(int delta)
    {
        if (_gameList.SelectedItem is not GameEntry game) return;
        var list = _settings.Games.ToList();
        var index = list.IndexOf(game);
        var target = index + delta;
        if (target < 0 || target >= list.Count) return;
        (list[index], list[target]) = (list[target], list[index]);
        _settings.Games = list;
        ReloadGames();
        _gameList.SelectedItem = game;
        GamesChanged();
    }

    void GamesChanged()
    {
        _settings.Save();
        _miner?.Wake();
    }
}
