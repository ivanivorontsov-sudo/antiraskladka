using Antiraskladka.Core;
namespace Antiraskladka;

sealed class SettingsForm : Form
{
    public Settings Result { get; private set; }
    readonly ComboBox _lat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly ComboBox _cyr = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly TextBox _hot = new() { ReadOnly = true, Width = 300 };
    readonly CheckBox _alt = new() { Text = "Также Ctrl+Shift+Q", AutoSize = true };
    readonly CheckBox _popup = new() { Text = "Показывать кнопку «Сменить раскладку» после выделения мышью", AutoSize = true };
    readonly CheckBox _switch = new() { Text = "Переключать язык ввода на нужную раскладку", AutoSize = true };
    readonly CheckBox _auto = new() { Text = "Запускать вместе с Windows", AutoSize = true };
    readonly CheckBox _typo = new() { Text = "Исправлять опечатки (русский и английский)", AutoSize = true };
    Keys _hotKey;

    record Item(string Id, string Name) { public override string ToString() => Name; }

    public SettingsForm(Settings s)
    {
        Result = s;
        Text = "Антираскладка — настройки"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(12);
        Font = new Font("Segoe UI", 9f);
        Fill(_lat, Layouts.LatinLayouts, s.LatinLayout); Fill(_cyr, Layouts.CyrillicLayouts, s.CyrillicLayout);
        _hotKey = s.Hotkey; _hot.Text = MainWindow.KeyName(_hotKey);
        _hot.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu) return;
            _hotKey = e.KeyData; _hot.Text = MainWindow.KeyName(_hotKey);
        };
        _alt.Checked = s.AltHotkey; _popup.Checked = s.PopupButton; _switch.Checked = s.SwitchLanguage; _auto.Checked = s.Autostart; _typo.Checked = s.TypoCorrection;

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        void Row(string label, Control c) { t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }); t.Controls.Add(c); }
        Row("Латинская раскладка:", _lat);
        Row("Кириллическая раскладка:", _cyr);
        Row("Горячая клавиша:", _hot);
        t.Controls.Add(new Label { Text = "(щёлкните в поле и нажмите сочетание)", AutoSize = true, ForeColor = Color.Gray }); t.Controls.Add(_alt);
        foreach (var c in new Control[] { _popup, _switch, _auto, _typo }) { t.Controls.Add(c); t.SetColumnSpan(c, 2); }
        var dict = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var openWords = new Button { Text = "Мои слова…", AutoSize = true };
        var openIgnore = new Button { Text = "Не исправлять…", AutoSize = true };
        openWords.Click += (_, _) => OpenFile(Settings.UserWordsPath);
        openIgnore.Click += (_, _) => OpenFile(Settings.IgnorePath);
        dict.Controls.Add(new Label { Text = "Личный словарь:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        dict.Controls.Add(openWords); dict.Controls.Add(openIgnore);
        t.Controls.Add(dict); t.SetColumnSpan(dict, 2);
        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        p.Controls.Add(cancel); p.Controls.Add(ok); t.Controls.Add(p); t.SetColumnSpan(p, 2);
        var help = new Label { AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Color.DimGray,
            Text = "Как пользоваться: выделите неправильно набранный текст в любой программе и нажмите горячую клавишу " +
                   "или кнопку «Сменить раскладку» рядом с курсором. «Авто из Windows» берёт раскладки, установленные в системе.\n" +
                   "Если включено исправление опечаток: текст в правильной раскладке, но с опечатками, будет только исправлен; " +
                   "текст в чужой раскладке — переведён и исправлен. Изменения словаря применяются после «Сохранить»." };
        t.Controls.Add(help); t.SetColumnSpan(help, 2);
        Controls.Add(t); AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, _) => Result = new Settings
        {
            LatinLayout = ((Item)_lat.SelectedItem!).Id, CyrillicLayout = ((Item)_cyr.SelectedItem!).Id,
            Hotkey = _hotKey, AltHotkey = _alt.Checked, PopupButton = _popup.Checked, SwitchLanguage = _switch.Checked, Autostart = _auto.Checked, TypoCorrection = _typo.Checked
        };
    }

    static void OpenFile(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) File.WriteAllText(path, "# одно слово на строку\n");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Антираскладка"); }
    }

    static void Fill(ComboBox cb, IEnumerable<KeyboardLayout> list, string sel)
    {
        cb.Items.Add(new Item("auto", "Авто из Windows (рекомендуется)"));
        foreach (var l in list) cb.Items.Add(new Item(l.Id, l.Name));
        cb.SelectedItem = cb.Items.Cast<Item>().FirstOrDefault(i => i.Id == sel) ?? cb.Items[0];
    }
}
