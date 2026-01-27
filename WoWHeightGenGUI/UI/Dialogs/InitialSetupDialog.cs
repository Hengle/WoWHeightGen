using System.Numerics;
using ImGuiNET;
using WoWHeightGenLib.Configuration;
using WoWHeightGenLib.Models;

namespace WoWHeightGenGUI.UI.Dialogs;

public class InitialSetupDialog
{
    private List<DetectedWowInstallation>? _installations;
    private int _selectedIndex = -1;
    private bool _useManual;
    private string _manualPath = "";
    private int _manualProductIndex;
    private string? _errorMessage;
    private bool _isConnecting;

    public bool IsComplete { get; private set; }
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

    public void Render()
    {
        // Detect installations on first render
        if (_installations == null)
        {
            _installations = WowInstallationDetector.DetectInstallations();
            if (_installations.Count > 0)
            {
                _selectedIndex = 0;
            }
        }

        var viewport = ImGui.GetMainViewport();
        var windowSize = new Vector2(550, 400);
        var windowPos = viewport.WorkPos + (viewport.WorkSize - windowSize) / 2;

        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);

        var windowFlags = ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoDocking;

        if (ImGui.Begin("Welcome to WoW Height Map Generator", windowFlags))
        {
            ImGui.TextWrapped("Select a World of Warcraft installation to get started.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Detected installations section
            ImGui.Text("Detected Installations:");
            ImGui.Spacing();

            if (_installations.Count == 0)
            {
                ImGui.TextColored(new Vector4(1, 0.7f, 0.3f, 1), "No installations detected automatically.");
            }
            else
            {
                ImGui.BeginChild("InstallationsList", new Vector2(0, 120), ImGuiChildFlags.Border);

                for (int i = 0; i < _installations.Count; i++)
                {
                    var install = _installations[i];
                    var isSelected = !_useManual && _selectedIndex == i;

                    if (ImGui.Selectable($"{install.ProductInfo.UninstallName}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 20)))
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
                        ImGui.EndTooltip();
                    }
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

            // Connect button
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var canConnect = _useManual
                ? !string.IsNullOrWhiteSpace(_manualPath)
                : _selectedIndex >= 0;

            ImGui.BeginDisabled(!canConnect || _isConnecting);

            var buttonSize = new Vector2(120, 30);
            var buttonPos = (ImGui.GetContentRegionAvail().X - buttonSize.X) / 2;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + buttonPos);

            if (ImGui.Button(_isConnecting ? "Connecting..." : "Connect", buttonSize))
            {
                TryConnect();
            }

            ImGui.EndDisabled();
        }
        ImGui.End();
    }

    private void TryConnect()
    {
        _isConnecting = true;
        _errorMessage = null;

        try
        {
            if (_useManual)
            {
                // Validate manual path
                if (!Directory.Exists(_manualPath))
                {
                    _errorMessage = "Directory does not exist.";
                    _isConnecting = false;
                    return;
                }

                var buildInfoPath = Path.Combine(_manualPath, ".build.info");
                if (!File.Exists(buildInfoPath))
                {
                    _errorMessage = "Not a valid WoW installation (missing .build.info).";
                    _isConnecting = false;
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

            IsComplete = true;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isConnecting = false;
        }
    }
}
