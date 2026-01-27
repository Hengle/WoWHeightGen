using System.Text.Json;

namespace WoWHeightGenGUI.Configuration;

public class SettingsManager
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public AppSettings Settings { get; private set; } = new();

    public string SettingsDirectory => _settingsDirectory;

    public SettingsManager()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WoWHeightGenGUI");
        _settingsPath = Path.Combine(_settingsDirectory, SettingsFileName);
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(_settingsDirectory))
                Directory.CreateDirectory(_settingsDirectory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public string GetImGuiIniPath()
    {
        if (!Directory.Exists(_settingsDirectory))
            Directory.CreateDirectory(_settingsDirectory);

        return Path.Combine(_settingsDirectory, "imgui.ini");
    }
}
