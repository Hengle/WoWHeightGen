using System.Text.Json;

namespace WoWHeightGenGUI.Configuration;

public enum FileType
{
    Blp,
    Wdt,
    HeightMap,
    AreaMap,
    Minimap
}

public record RecentFileEntry(
    uint FileDataId,
    FileType Type,
    string Product,
    DateTime LastOpened,
    string? DisplayName = null);

public class RecentFilesManager
{
    private const string RecentFilesName = "recent.json";
    private readonly string _filePath;
    private readonly int _maxEntries;
    private List<RecentFileEntry> _entries = new();

    public RecentFilesManager(string settingsDirectory, int maxEntries = 20)
    {
        _filePath = Path.Combine(settingsDirectory, RecentFilesName);
        _maxEntries = maxEntries;
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _entries = JsonSerializer.Deserialize<List<RecentFileEntry>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load recent files: {ex.Message}");
            _entries = new();
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_entries, options);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save recent files: {ex.Message}");
        }
    }

    public void AddEntry(uint fileDataId, FileType type, string product, string? displayName = null)
    {
        // Remove existing entry for same file
        _entries.RemoveAll(e => e.FileDataId == fileDataId && e.Type == type);

        // Add new entry at front
        _entries.Insert(0, new RecentFileEntry(fileDataId, type, product, DateTime.Now, displayName));

        // Trim to max size
        if (_entries.Count > _maxEntries)
            _entries = _entries.Take(_maxEntries).ToList();

        Save();
    }

    public IReadOnlyList<RecentFileEntry> GetRecentFiles() => _entries.AsReadOnly();

    public void Clear()
    {
        _entries.Clear();
        Save();
    }
}
