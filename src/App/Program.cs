using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Antiraskladka.Core;
namespace Antiraskladka;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Antiraskladka_SingleInstance", out bool first);
        if (!first) { MessageBox.Show("Антираскладка уже запущена — значок в трее.", "Антираскладка"); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }
}

/// <summary>Скрытое окно: значок в трее, горячие клавиши, хук мыши.</summary>
sealed class MainWindow : Form
{
    readonly NotifyIcon _tray = new();
    readonly PopupButton _popup;
    Settings _s = Settings.Load();
    LayoutConverter _conv = null!;
    Task<TypoCorrector>? _typos;
    IntPtr _latHkl, _cyrHkl, _hook, _lastForeign;
    Native.HookProc? _hookProc;
    Native.POINT _down; uint _lastDownTime; bool _busy;
    readonly System.Windows.Forms.Timer _fgTimer = new() { Interval = 300 };
    readonly uint _myPid = (uint)Environment.ProcessId;

    public MainWindow()
    {
        ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None; Opacity = 0; Size = new Size(1, 1);
        _popup = new PopupButton(() => DoConvert(false));
        var menu = new ContextMenuStrip();
        menu.Items.Add("Сменить раскладку выделенного", null, (_, _) => DoConvert(true));
        menu.Items.Add("Только исправить опечатки в выделенном", null, (_, _) => DoConvert(true, Mode.TyposOnly));
        menu.Items.Add("Добавить выделенное слово в словарь", null, (_, _) => DoConvert(true, Mode.AddToDictionary));
        menu.Items.Add("Никогда не исправлять выделенное слово", null, (_, _) => DoConvert(true, Mode.IgnoreWord));
        menu.Items.Add("Настройки…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => { _tray.Visible = false; Application.Exit(); });
        _tray.ContextMenuStrip = menu;
        _tray.Icon = MakeIcon(); _tray.Text = "Антираскладка"; _tray.Visible = true;
        _tray.DoubleClick += (_, _) => OpenSettings();
        _fgTimer.Tick += (_, _) => { var h = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(h, out var pid); if (h != IntPtr.Zero && pid != _myPid) _lastForeign = h; };
        _fgTimer.Start();
    }

    protected override void OnLoad(EventArgs e) { base.OnLoad(e); Hide(); Apply(); }
    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(IsHandleCreated ? false : value);

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            g.FillEllipse(new SolidBrush(Color.FromArgb(0, 120, 215)), 0, 0, 31, 31);
            using var f = new Font("Segoe UI", 11, FontStyle.Bold);
            TextRenderer.DrawText(g, "Ф", f, new Rectangle(0, 0, 32, 32), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    void Apply()
    {
        // раскладки
        KeyboardLayout? lat = Layouts.ById(_s.LatinLayout), cyr = Layouts.ById(_s.CyrillicLayout);
        var auto = (lat == null || cyr == null) ? WindowsLayouts.AutoPair() : null;
        lat ??= auto?.latin ?? Layouts.UsQwerty;
        cyr ??= auto?.cyr ?? Layouts.Ru;
        _conv = new LayoutConverter(lat, cyr);
        _latHkl = Layouts.ById(_s.LatinLayout) == null && auto != null ? auto.Value.latHkl : WindowsLayouts.Find(true, lat.Id);
        _cyrHkl = Layouts.ById(_s.CyrillicLayout) == null && auto != null ? auto.Value.cyrHkl : WindowsLayouts.Find(false, cyr.Id);
        // горячие клавиши
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
        var (mods, vk) = SplitKeys(_s.Hotkey);
        if (vk != 0 && !Native.RegisterHotKey(Handle, 1, mods | Native.MOD_NOREPEAT, vk))
            _tray.ShowBalloonTip(3000, "Антираскладка", "Не удалось занять горячую клавишу " + KeyName(_s.Hotkey), ToolTipIcon.Warning);
        if (_s.AltHotkey) Native.RegisterHotKey(Handle, 2, Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT, (uint)Keys.Q);
        // хук мыши
        if (_s.PopupButton && _hook == IntPtr.Zero)
        {
            _hookProc = MouseHook;
            _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _hookProc, Native.GetModuleHandle(null), 0);
        }
        else if (!_s.PopupButton && _hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        SetAutostart(_s.Autostart);
        // исправление опечаток: словари грузятся в фоне один раз, личный словарь — заново
        if (_s.TypoCorrection)
            _typos = Task.Run(() => TypoCorrector.LoadDefault(new UserDictionary(Settings.UserWordsPath, Settings.IgnorePath)));
        else _typos = null;
    }

    enum Mode { Smart, TyposOnly, AddToDictionary, IgnoreWord }

    public static (uint mods, uint vk) SplitKeys(Keys k)
    {
        uint m = 0;
        if (k.HasFlag(Keys.Control)) m |= Native.MOD_CONTROL;
        if (k.HasFlag(Keys.Shift)) m |= Native.MOD_SHIFT;
        if (k.HasFlag(Keys.Alt)) m |= Native.MOD_ALT;
        return (m, (uint)(k & Keys.KeyCode));
    }
    public static string KeyName(Keys k) => new KeysConverter().ConvertToString(k) ?? k.ToString();

    static void SetAutostart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (on) key.SetValue("Antiraskladka", "\"" + Environment.ProcessPath + "\"");
            else key.DeleteValue("Antiraskladka", false);
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY) BeginInvoke(() => DoConvert(false));
        base.WndProc(ref m);
    }

    IntPtr MouseHook(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            var d = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(l);
            int msg = (int)w;
            if (msg == Native.WM_LBUTTONDOWN)
            {
                bool dbl = d.time - _lastDownTime < (uint)Native.GetDoubleClickTime() && Math.Abs(d.pt.X - _down.X) < 5 && Math.Abs(d.pt.Y - _down.Y) < 5;
                if (!_popup.Bounds.Contains(d.pt.X, d.pt.Y)) BeginInvoke(_popup.HidePopup);
                _lastDownTime = d.time; _down = d.pt;
                if (dbl) ShowPopupSoon(d.pt);
            }
            else if (msg == Native.WM_LBUTTONUP)
            {
                int dx = d.pt.X - _down.X, dy = d.pt.Y - _down.Y;
                if (dx * dx + dy * dy > 100 && !_popup.Bounds.Contains(d.pt.X, d.pt.Y)) ShowPopupSoon(d.pt);
            }
        }
        return Native.CallNextHookEx(_hook, n, w, l);
    }

    void ShowPopupSoon(Native.POINT pt)
    {
        var fg = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(fg, out var pid);
        if (pid == _myPid || _busy) return;
        BeginInvoke(async () => { await Task.Delay(120); _popup.ShowAt(new Point(pt.X + 12, pt.Y + 16)); });
    }

    async void DoConvert(bool fromTray, Mode mode = Mode.Smart)
    {
        if (_busy) return;
        _busy = true;
        _popup.HidePopup();
        try
        {
            if (fromTray && _lastForeign != IntPtr.Zero) { Native.SetForegroundWindow(_lastForeign); await Task.Delay(150); }
            var target = Native.GetForegroundWindow();
            await WaitModifiersReleased();
            var backup = ClipboardBackup.Save();
            try
            {
                uint seq = Native.GetClipboardSequenceNumber();
                Native.Keys((0x11, false), (0x43, false), (0x43, true), (0x11, true)); // Ctrl+C
                string? text = null;
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(20);
                    if (Native.GetClipboardSequenceNumber() != seq) { await Task.Delay(30); text = TryGetText(); break; }
                }
                if (string.IsNullOrEmpty(text)) { if (fromTray) _tray.ShowBalloonTip(2000, "Антираскладка", "Сначала выделите текст.", ToolTipIcon.Info); return; }
                TypoCorrector? typos = null;
                if (mode != Mode.Smart || _typos != null)
                    typos = await (_typos ?? Task.Run(() => TypoCorrector.LoadDefault(new UserDictionary(Settings.UserWordsPath, Settings.IgnorePath))));
                if (mode is Mode.AddToDictionary or Mode.IgnoreWord)
                {
                    var w = text.Trim();
                    bool ok = w.Length > 0 && !w.Any(char.IsWhiteSpace) && (mode == Mode.AddToDictionary ? typos!.User.Add(w) : typos!.User.Ignore(w));
                    _tray.ShowBalloonTip(2000, "Антираскладка", !ok ? "Выделите одно слово, которого ещё нет в списке." :
                        mode == Mode.AddToDictionary ? $"«{w}» добавлено в личный словарь." : $"«{w}» больше не будет исправляться.", ToolTipIcon.Info);
                    return;
                }
                string result; Direction? dir;
                if (mode == Mode.TyposOnly) { result = typos!.Correct(text); dir = null; }
                else { var fr = TextFixer.Fix(text, _conv, typos); result = fr.Text; dir = fr.Dir; }
                if (result == text) { if (mode == Mode.TyposOnly) _tray.ShowBalloonTip(1500, "Антираскладка", "Опечаток не найдено.", ToolTipIcon.Info); return; }
                if (!TrySetText(result)) return;
                await Task.Delay(30);
                Native.Keys((0x11, false), (0x56, false), (0x56, true), (0x11, true)); // Ctrl+V
                await Task.Delay(250);
                if (_s.SwitchLanguage && dir != null)
                {
                    var hkl = dir == Direction.LatinToCyrillic ? _cyrHkl : _latHkl;
                    if (hkl != IntPtr.Zero) Native.PostMessage(target, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
                }
            }
            finally { await Task.Delay(150); backup.Restore(); }
        }
        catch (Exception ex) { _tray.ShowBalloonTip(3000, "Антираскладка", "Ошибка: " + ex.Message, ToolTipIcon.Error); }
        finally { _busy = false; }
    }

    static async Task WaitModifiersReleased()
    {
        for (int i = 0; i < 50; i++)
        {
            bool any = new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (Native.GetAsyncKeyState(k) & 0x8000) != 0);
            if (!any) return;
            await Task.Delay(20);
        }
    }

    static string? TryGetText() { for (int i = 0; i < 5; i++) { try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { Thread.Sleep(30); } } return null; }
    static bool TrySetText(string s) { for (int i = 0; i < 5; i++) { try { Clipboard.SetText(s); return true; } catch { Thread.Sleep(30); } } return false; }

    void OpenSettings()
    {
        using var f = new SettingsForm(_s);
        if (f.ShowDialog() == DialogResult.OK) { _s = f.Result; _s.Save(); Apply(); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
        _tray.Visible = false;
        base.OnFormClosing(e);
    }
}

/// <summary>Сохраняет содержимое буфера обмена (основные форматы) и возвращает его обратно.</summary>
sealed class ClipboardBackup
{
    readonly Dictionary<string, object> _data = new();
    static readonly string[] Formats = { DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.Bitmap, DataFormats.FileDrop };
    public static ClipboardBackup Save()
    {
        var b = new ClipboardBackup();
        try
        {
            var obj = Clipboard.GetDataObject();
            if (obj != null)
                foreach (var f in Formats)
                    if (obj.GetDataPresent(f)) { var v = obj.GetData(f); if (v != null) b._data[f] = v; }
        }
        catch { }
        return b;
    }
    public void Restore()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (_data.Count == 0) { Clipboard.Clear(); return; }
                var d = new DataObject();
                foreach (var kv in _data) d.SetData(kv.Key, kv.Value);
                Clipboard.SetDataObject(d, true);
                return;
            }
            catch { Thread.Sleep(40); }
        }
    }
}

/// <summary>Маленькая плавающая кнопка, которая не забирает фокус у окна с текстом.</summary>
sealed class PopupButton : Form
{
    readonly System.Windows.Forms.Timer _hide = new() { Interval = 4000 };
    public PopupButton(Action onClick)
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(0, 120, 215); Padding = new Padding(1);
        var b = new Label
        {
            Text = "⇄ Сменить раскладку", AutoSize = true, ForeColor = Color.White, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f), Padding = new Padding(8, 4, 8, 4), BackColor = Color.FromArgb(0, 120, 215)
        };
        b.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) onClick(); };
        Controls.Add(b);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _hide.Tick += (_, _) => HidePopup();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/ | 0x8 /*TOPMOST*/; return cp; }
    }
    public void ShowAt(Point p)
    {
        var scr = Screen.FromPoint(p).WorkingArea;
        if (!Visible) Show();
        Location = new Point(Math.Min(p.X, scr.Right - Width), Math.Min(p.Y, scr.Bottom - Height));
        _hide.Stop(); _hide.Start();
    }
    public void HidePopup() { _hide.Stop(); if (Visible) Hide(); }
}
