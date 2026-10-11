using System.Runtime.InteropServices;

namespace LiteDrops;

/// <summary>UI strings in English and Japanese. The language is picked once at startup.</summary>
static class L
{
    public static bool Ja { get; private set; }

    /// <param name="setting">"en", "ja" or "auto" (follow the Windows display language).</param>
    public static void Init(string setting) => Ja = setting switch
    {
        "ja" => true,
        "en" => false,
        _ => (GetUserDefaultUILanguage() & 0x3FF) == 0x11, // LANG_JAPANESE
    };

    [DllImport("kernel32.dll")]
    static extern ushort GetUserDefaultUILanguage();

    static string T(string en, string ja) => Ja ? ja : en;

    // status
    public static string Mining => T("Mining", "採掘中");
    public static string Idle => T("Idle", "待機中");
    public static string Searching => T("Searching", "検索中");
    public static string Error => T("Error", "エラー");
    public static string LoggedOut => T("Logged out", "未ログイン");
    public static string Offline => T("Offline", "オフライン");
    public static string Stopped => T("Stopped", "停止");
    public static string Starting => T("Starting", "起動中");

    // cards
    public static string NowWatching => T("Now watching", "視聴中");
    public static string Games => T("Games", "ゲーム");
    public static string Campaigns => T("Campaigns", "キャンペーン");
    public static string GamesHint => T("Drag to reorder · top = highest priority", "ドラッグで並べ替え・上ほど優先");
    public static string NotWatching => T("Not watching", "視聴していません");
    public static string Minutes(int current, int required) => T($"{current} / {required} min", $"{current} / {required} 分");
    public static string CampaignLine(string name, DateTime endAt) => T($"{name} · ends {EndsIn(endAt)}", $"{name} · 終了まで{EndsIn(endAt)}");
    public static string NoCampaigns => T("No campaigns found yet", "まだキャンペーンが見つかっていません");
    public static string OpenChannel => T("Open this stream in the browser", "この配信をブラウザで開く");

    static string EndsIn(DateTime endAt)
    {
        var left = endAt - DateTime.UtcNow;
        if (left.TotalDays >= 2) return T($"in {(int)left.TotalDays} days", $"あと{(int)left.TotalDays}日");
        if (left.TotalHours >= 2) return T($"in {(int)left.TotalHours} hours", $"あと{(int)left.TotalHours}時間");
        if (left.TotalMinutes > 0) return T($"in {(int)left.TotalMinutes} min", $"あと{(int)left.TotalMinutes}分");
        return T("soon", "まもなく");
    }

    // login
    public static string LoginPrompt => T("Log in with your Twitch account to start mining.", "Twitch アカウントでログインすると採掘を始めます。");
    public static string LoginButton => T("Log in with Twitch", "Twitch でログイン");
    public static string EnterCode => T("Enter this code at twitch.tv/activate (opened in your browser), then confirm:",
        "ブラウザで開いた twitch.tv/activate に次のコードを入力して、許可してください：");
    public static string Copy => T("Copy", "コピー");
    public static string Copied => T("Copied", "コピー済み");
    public static string CodeExpired => T("The code expired. Please try again.", "コードの有効期限が切れました。もう一度お試しください。");
    public static string LoginFailed(string reason) => T($"Login failed: {reason}", $"ログインに失敗しました：{reason}");
    public static string LoginExpired => T("Your login expired. Please log in again.", "ログインの有効期限が切れました。もう一度ログインしてください。");
    public static string CantReach(string reason) => T($"Can't reach Twitch: {reason}", $"Twitch に接続できません：{reason}");
    public static string NotLoggedIn => T("Not logged in", "未ログイン");
    public static string LoggedInAs(string login) => T($"Logged in as {login}", $"{login} でログイン中");
    public static string LogOut => T("Log out", "ログアウト");

    // games
    public static string SearchPlaceholder => T("Search Twitch games…", "Twitch のゲームを検索…");
    public static string Add => T("Add", "追加");
    public static string Added => T("✓ Added", "✓ 追加済み");
    public static string NoGames => T("No games yet — search below to add one", "ゲームがありません。下の検索から追加してください");
    public static string Removed(string game) => T($"Removed “{game}”", $"「{game}」を削除しました");
    public static string Undo => T("Undo", "元に戻す");
    public static string DragTip => T("Drag to change the priority", "ドラッグして優先順位を変更");
    public static string CheckTip => T("Mine this game (on/off)", "このゲームを採掘する（オン/オフ）");
    public static string RemoveTip => T("Remove from the list", "リストから削除");
    public static string Clear => T("Clear", "クリア");

    // tray
    public static string Show => T("Show", "表示");
    public static string Exit => T("Exit", "終了");
    public static string StartWithWindows => T("Start with Windows", "Windows 起動時に開始");
    public static string Language => T("Language", "言語");
    public static string LanguageAuto => T("Auto (Windows setting)", "自動（Windows の設定）");
    public static string DropClaimed => T("Drop claimed", "ドロップを受け取りました");
    public static string AlreadyRunning => T("LiteDrops is already running (check the system tray).",
        "LiteDrops はすでに起動しています（タスクトレイを確認してください）。");

    // miner
    public static string LookingForCampaigns => T("Looking for campaigns…", "キャンペーンを探しています…");
    public static string NoLiveDrops => T("No live drops for the selected games", "選んだゲームに配信中のドロップがありません");
    public static string SelectGame => T("Select a game to mine", "採掘するゲームを選んでください");
    public static string ErrorRetry(string reason) => T($"Error: {reason} (retrying in 1 min)", $"エラー：{reason}（1分後に再試行）");
}
