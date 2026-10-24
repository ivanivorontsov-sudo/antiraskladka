using System.Text.Json;
namespace Antiraskladka;

public sealed class Settings
{
    public string LatinLayout { get; set; } = "auto";   // auto = из Windows
    public string CyrillicLayout { get; set; } = "auto";
    public Keys Hotkey { get; set; } = Keys.Pause;
    public bool AltHotkey { get; set; } = true;          // Ctrl+Shift+Q
    public bool PopupButton { get; set; } = true;
    public bool SwitchLanguage { get; set; } = true;
    public bool Autostart { get; set; } = true;

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Antiraskladka");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { }
        return new();
    }
    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
