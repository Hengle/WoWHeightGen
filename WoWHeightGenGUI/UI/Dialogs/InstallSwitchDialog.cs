using System.Numerics;
using ImGuiNET;
using WoWHeightGenLib.Configuration;
using WoWHeightGenLib.Models;

namespace WoWHeightGenGUI.UI.Dialogs;

/// <summary>
/// Dialog for switching between WoW installations.
/// Similar to InitialSetupDialog but as a modal that can be opened from the menu.
/// </summary>
public class InstallSwitchDialog
{
    private List<DetectedWowInstallation>? _installations;
    private Dictionary<int, string>? _versionCache;
    private int _selectedIndex = -1;
    private bool _useManual;
    private string _manualPath = "";
    private int _manualProductIndex;
    private string? _errorMessage;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool HasSelection { get; private set; }
    public string? SelectedPath { get; private set; }
    public string? SelectedProduct { get; private set; }

    private static readonly string[] ProductOptions =
    {
        "wow (Retail)",
        "wowt (PTR)",
        "wow_classic (Classic)",
        "wow_classic_era (Classic Era)",
        "wow_beta (Beta)"
    };

    private static readonly string[] ProductCodes =
    {
        "wow",
        "wowt",
        "wow_classic",
        "wow_classic_era",
        "wow_beta"
    };

    /// <summary>
    /// Open the dialog
    /// </summary>
    public void Open()
    {
        _isOpen = true;
        HasSelection = false;
        SelectedPath = null;
        SelectedProduct = null;
        _errorMessage = null;
        _useManual = false;

        // Refresh installations
        _installations = WowInstallationDetector.DetectInstallations();
        _versionCache = new Dictionary<int, string>();

        for (int i = 0; i < _installations.Count; i++)
        {
            var version = GetVersionFromBuildInfo(_installations[i].InstallPath, _installations[i].ProductInfo.Product);
            _versionCache[i] = version;
        }

        if (_installations.Count > 0)
        {
            _selectedIndex = 0;
        }
        else
        {
            _selectedIndex = -1;
        }
    }

    /// <summary>
    /// Close the dialog
    /// </summary>
    public void Close()
    {
        _isOpen = false;
    }

    /// <summary>
    /// Render the dialog (call every frame while open)
    /// </summary>
    public void Render()
    {
        if (!_isOpen) return;

        // Open the modal popup if not already open
        if (!ImGui.IsPopupOpen("Switch WoW Installation"))
        {
            ImGui.OpenPopup("Switch WoW Installation");
        }

        var viewport = ImGui.GetMainViewport();
        var windowSize = new Vector2(550, 420);
        var windowPos = viewport.WorkPos + (viewport.WorkSize - windowSize) / 2;

        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Appearing);

        var windowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking;

        bool open = true;
        if (ImGui.BeginPopupModal("Switch WoW Installation", ref open, windowFlags))
        {
            ImGui.TextWrapped("Select a World of Warcraft installation.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Detected installations section
            ImGui.Text("Detected Installations:");
            ImGui.Spacing();

            if (_installations == null || _installations.Count == 0)
            {
                ImGui.TextColored(new Vector4(1, 0.7f, 0.3f, 1), "No installations detected automatically.");
            }
            else
            {
                ImGui.BeginChild("InstallationsList", new Vector2(0, 150), ImGuiChildFlags.Border);

                for (int i = 0; i < _installations.Count; i++)
                {
                    var install = _installations[i];
                    var isSelected = !_useManual && _selectedIndex == i;
                    var version = _versionCache?.GetValueOrDefault(i, "Unknown") ?? "Unknown";

                    ImGui.PushID(i);

                    // Combine product name and version into single selectable
                    var displayText = $"{install.ProductInfo.UninstallName}\n  Version: {version}";
                    if (ImGui.Selectable(displayText, isSelected, ImGuiSelectableFlags.None, new Vector2(0, 36)))
                    {
                        _selectedIndex = i;
                        _useManual = false;
                        _errorMessage = null;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"Path: {install.InstallPath}");
                        ImGui.Text($"Product: {install.ProductInfo.Product}");
                        ImGui.Text($"Version: {version}");
                        ImGui.EndTooltip();
                    }

                    ImGui.PopID();
                }

                ImGui.EndChild();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Manual entry section
            if (ImGui.Checkbox("Add installation manually", ref _useManual))
            {
                if (_useManual)
                {
                    _selectedIndex = -1;
                }
                _errorMessage = null;
            }

            if (_useManual)
            {
                ImGui.Spacing();

                ImGui.Text("Installation Path:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##manualPath", ref _manualPath, 500))
                {
                    _errorMessage = null;
                }

                ImGui.Spacing();

                ImGui.Text("Product:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.Combo("##manualProduct", ref _manualProductIndex, ProductOptions, ProductOptions.Length))
                {
                    _errorMessage = null;
                }
            }

            // Error message
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), _errorMessage);
            }

            // Buttons
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var canConnect = _useManual
                ? !string.IsNullOrWhiteSpace(_manualPath)
                : _selectedIndex >= 0;

            var buttonWidth = 100.0f;
            var totalWidth = buttonWidth * 2 + 10;
            var startX = (ImGui.GetContentRegionAvail().X - totalWidth) / 2;

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);

            ImGui.BeginDisabled(!canConnect);
            if (ImGui.Button("Connect", new Vector2(buttonWidth, 28)))
            {
                TryConnect();
            }
            ImGui.EndDisabled();

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(buttonWidth, 28)))
            {
                Close();
            }

            ImGui.EndPopup();
        }

        if (!open)
        {
            Close();
        }
    }

    private void TryConnect()
    {
        _errorMessage = null;

        try
        {
            if (_useManual)
            {
                if (!Directory.Exists(_manualPath))
                {
                    _errorMessage = "Directory does not exist.";
                    return;
                }

                var buildInfoPath = Path.Combine(_manualPath, ".build.info");
                if (!File.Exists(buildInfoPath))
                {
                    _errorMessage = "Not a valid WoW installation (missing .build.info).";
                    return;
                }

                SelectedPath = _manualPath;
                SelectedProduct = ProductCodes[_manualProductIndex];
            }
            else if (_selectedIndex >= 0 && _installations != null)
            {
                var install = _installations[_selectedIndex];
                SelectedPath = install.InstallPath;
                SelectedProduct = install.ProductInfo.Product;
            }
            else
            {
                _errorMessage = "Please select an installation.";
                return;
            }

            HasSelection = true;
            Close();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
    }

    private static string GetVersionFromBuildInfo(string installPath, string product)
    {
        try
        {
            var buildInfoPath = Path.Combine(installPath, ".build.info");
            if (!File.Exists(buildInfoPath))
                return "Unknown";

            var lines = File.ReadAllLines(buildInfoPath);
            if (lines.Length < 2)
                return "Unknown";

            var headers = lines[0].Split('|');
            int versionIndex = -1;
            int productIndex = -1;

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Split('!')[0].Trim();
                if (header.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    versionIndex = i;
                else if (header.Equals("Product", StringComparison.OrdinalIgnoreCase))
                    productIndex = i;
            }

            if (versionIndex < 0)
                return "Unknown";

            for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
            {
                var values = lines[lineIdx].Split('|');

                if (productIndex >= 0 && productIndex < values.Length)
                {
                    var rowProduct = values[productIndex].Trim();
                    if (!rowProduct.Equals(product, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (versionIndex < values.Length)
                {
                    var version = values[versionIndex].Trim();
                    if (!string.IsNullOrEmpty(version))
                        return version;
                }
            }

            if (lines.Length > 1)
            {
                var values = lines[1].Split('|');
                if (versionIndex < values.Length)
                {
                    var version = values[versionIndex].Trim();
                    if (!string.IsNullOrEmpty(version))
                        return version;
                }
            }

            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
