using System.Text.Json;

namespace StudyMind.App;

public sealed class CyberNoteSettings
{
    public const int DefaultWidth = 430;
    public const int DefaultHeight = 620;
    public const int MinWidth = DefaultWidth;
    public const int MinHeight = DefaultHeight;
    public const int MaxWidth = DefaultWidth;
    public const int MaxHeight = DefaultHeight;

    public bool ShowTodayPlan { get; set; } = true;

    public bool ShowSchedule { get; set; } = true;

    public bool ShowTopics { get; set; } = true;

    public int CurrentPageIndex { get; set; }

    public int Width { get; set; } = DefaultWidth;

    public int Height { get; set; } = DefaultHeight;

    public int? X { get; set; }

    public int? Y { get; set; }

    public bool HasSavedPlacement => X.HasValue && Y.HasValue;
}

public static class CyberNoteSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StudyMind");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "cyber-note-settings.json");

    public static CyberNoteSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new CyberNoteSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return Normalize(JsonSerializer.Deserialize<CyberNoteSettings>(json, JsonOptions) ?? new CyberNoteSettings());
        }
        catch
        {
            return new CyberNoteSettings();
        }
    }

    public static void Save(CyberNoteSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Normalize(settings), JsonOptions));
        }
        catch
        {
            // Preference persistence should never prevent the companion window from working.
        }
    }

    private static CyberNoteSettings Normalize(CyberNoteSettings settings)
    {
        settings.Width = CyberNoteSettings.DefaultWidth;
        settings.Height = CyberNoteSettings.DefaultHeight;
        settings.CurrentPageIndex = Math.Clamp(settings.CurrentPageIndex, 0, 3);

        if (settings.X.HasValue != settings.Y.HasValue)
        {
            settings.X = null;
            settings.Y = null;
        }

        return settings;
    }
}
