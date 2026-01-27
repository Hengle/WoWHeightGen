namespace WoWHeightGenGUI.Configuration;

public class AppSettings
{
    // WoW Configuration
    public string? WowInstallPath { get; set; }
    public string? WowProduct { get; set; }

    // UI Preferences
    public bool RestoreLayout { get; set; } = true;
    public float UiScale { get; set; } = 1.0f;

    // Export Defaults
    public string DefaultExportFormat { get; set; } = "png";
    public int JpegQuality { get; set; } = 90;
    public string? LastExportPath { get; set; }

    // Viewer Settings
    public float DefaultZoom { get; set; } = 1.0f;
    public bool ShowGrid { get; set; } = false;
    public int MaxRecentFiles { get; set; } = 20;

    // Virtual Texturing
    public int TileCacheSize { get; set; } = 64;
    public int TileSize { get; set; } = 512;

    // Window State
    public int WindowWidth { get; set; } = 1600;
    public int WindowHeight { get; set; } = 900;
    public int WindowX { get; set; } = 100;
    public int WindowY { get; set; } = 100;
    public bool WindowMaximized { get; set; } = false;
}
