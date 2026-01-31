using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using ImGuiNET;
using WoWHeightGenGUI.Configuration;
using WoWHeightGenGUI.Services;
using WoWHeightGenGUI.UI;
using WoWHeightGenGUI.UI.Dialogs;
using WoWHeightGenGUI.UI.Panels;
using WoWHeightGenLib.Services;

namespace WoWHeightGenGUI.App;

public class Application : IDisposable
{
    private IWindow _window = null!;
    private GL _gl = null!;
    private IInputContext _input = null!;
    private ImGuiController _imguiController = null!;

    private SettingsManager _settingsManager = null!;
    private RecentFilesManager _recentFilesManager = null!;
    private PanelManager _panelManager = null!;

    private MapGenerationContext? _context;
    private Db2Service? _db2Service;
    private InitialSetupDialog? _setupDialog;
    private bool _setupComplete;

    public MapGenerationContext? Context => _context;
    public Db2Service? Db2Service => _db2Service;
    public SettingsManager Settings => _settingsManager;
    public RecentFilesManager RecentFiles => _recentFilesManager;
    public GL GL => _gl;

    public void Run()
    {
        // Load settings first
        _settingsManager = new SettingsManager();
        _settingsManager.Load();

        _recentFilesManager = new RecentFilesManager(
            _settingsManager.SettingsDirectory,
            _settingsManager.Settings.MaxRecentFiles);
        _recentFilesManager.Load();

        // Create window
        var options = WindowOptions.Default;
        options.Title = "WoW Height Map Generator";
        options.Size = new Vector2D<int>(
            _settingsManager.Settings.WindowWidth,
            _settingsManager.Settings.WindowHeight);
        options.Position = new Vector2D<int>(
            _settingsManager.Settings.WindowX,
            _settingsManager.Settings.WindowY);
        options.VSync = true;

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClosing;
        _window.Resize += OnResize;
        _window.Move += OnMove;

        _window.Run();
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _input = _window.CreateInput();

        // Initialize ImGui
        _imguiController = new ImGuiController(_gl, _window, _input);

        // Configure ImGui
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigWindowsMoveFromTitleBarOnly = true;

        // Note: IniFilename is read-only in newer ImGui versions
        // Layout will be saved to the default location

        // Set up ImGui style
        SetupImGuiStyle();

        // Initialize panel manager
        _panelManager = new PanelManager(this);
        _panelManager.Initialize();

        // Check if setup is needed (no saved WoW installation)
        if (string.IsNullOrEmpty(_settingsManager.Settings.WowInstallPath))
        {
            _setupDialog = new InitialSetupDialog();
            _setupComplete = false;
        }
        else
        {
            // Try to auto-connect to saved WoW installation
            TryAutoConnect();
            _setupComplete = _context != null;

            // If auto-connect failed, show setup dialog
            if (!_setupComplete)
            {
                _setupDialog = new InitialSetupDialog();
            }
        }

        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
    }

    private void SetupImGuiStyle()
    {
        var style = ImGui.GetStyle();
        style.WindowRounding = 4.0f;
        style.FrameRounding = 2.0f;
        style.GrabRounding = 2.0f;
        style.ScrollbarRounding = 2.0f;

        // Dark theme colors
        var colors = style.Colors;
        colors[(int)ImGuiCol.WindowBg] = new System.Numerics.Vector4(0.1f, 0.1f, 0.1f, 1.0f);
        colors[(int)ImGuiCol.FrameBg] = new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 1.0f);
        colors[(int)ImGuiCol.FrameBgHovered] = new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 1.0f);
        colors[(int)ImGuiCol.FrameBgActive] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f);
        colors[(int)ImGuiCol.TitleBg] = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.0f);
        colors[(int)ImGuiCol.TitleBgActive] = new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 1.0f);
        colors[(int)ImGuiCol.Header] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.0f);
        colors[(int)ImGuiCol.HeaderHovered] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f);
        colors[(int)ImGuiCol.HeaderActive] = new System.Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);
        colors[(int)ImGuiCol.Button] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.0f);
        colors[(int)ImGuiCol.ButtonHovered] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f);
        colors[(int)ImGuiCol.ButtonActive] = new System.Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);
        colors[(int)ImGuiCol.Tab] = new System.Numerics.Vector4(0.18f, 0.18f, 0.18f, 1.0f);
        colors[(int)ImGuiCol.TabHovered] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f);
        colors[(int)ImGuiCol.TabActive] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.0f);
        colors[(int)ImGuiCol.DockingPreview] = new System.Numerics.Vector4(0.4f, 0.4f, 0.8f, 0.7f);
    }

    private void TryAutoConnect()
    {
        var settings = _settingsManager.Settings;
        if (!string.IsNullOrEmpty(settings.WowInstallPath) && !string.IsNullOrEmpty(settings.WowProduct))
        {
            try
            {
                ConnectToWow(settings.WowInstallPath, settings.WowProduct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to auto-connect to WoW: {ex.Message}");
            }
        }
    }

    public void ConnectToWow(string installPath, string product)
    {
        _context?.Dispose();
        _db2Service?.Dispose();

        _context = new MapGenerationContext(installPath, product);
        _context.Initialize();

        // Initialize DB2 service
        _db2Service = new Db2Service();
        _db2Service.Initialize(_context);

        // Save to settings
        _settingsManager.Settings.WowInstallPath = installPath;
        _settingsManager.Settings.WowProduct = product;
        _settingsManager.Save();

        // Notify panels that connection changed
        _panelManager.OnConnectionChanged();
    }

    public void DisconnectFromWow()
    {
        _db2Service?.Dispose();
        _db2Service = null;
        _context?.Dispose();
        _context = null;
    }

    private void OnUpdate(double deltaTime)
    {
        _imguiController.Update((float)deltaTime);
    }

    private void OnRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Show setup dialog if not complete
        if (!_setupComplete && _setupDialog != null)
        {
            _setupDialog.Render();

            if (_setupDialog.IsComplete)
            {
                // Try to connect with selected installation
                try
                {
                    ConnectToWow(_setupDialog.SelectedPath!, _setupDialog.SelectedProduct!);
                    _setupComplete = true;
                    _setupDialog = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect: {ex.Message}");
                    // Reset dialog for retry
                    _setupDialog = new InitialSetupDialog();
                }
            }
        }
        else
        {
            // Render main UI
            _panelManager.Render();
        }

        _imguiController.Render();
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl.Viewport(size);

        if (!_window.WindowState.HasFlag(WindowState.Maximized))
        {
            _settingsManager.Settings.WindowWidth = size.X;
            _settingsManager.Settings.WindowHeight = size.Y;
        }
        _settingsManager.Settings.WindowMaximized = _window.WindowState.HasFlag(WindowState.Maximized);
    }

    private void OnMove(Vector2D<int> position)
    {
        if (!_window.WindowState.HasFlag(WindowState.Maximized))
        {
            _settingsManager.Settings.WindowX = position.X;
            _settingsManager.Settings.WindowY = position.Y;
        }
    }

    private void OnClosing()
    {
        _settingsManager.Save();
        _recentFilesManager.Save();
    }

    public void RequestExit()
    {
        _window.Close();
    }

    public void Dispose()
    {
        // Dispose GL resources BEFORE disposing GL context/window
        _panelManager?.Dispose();      // Contains compositor, textures (GL resources)
        _imguiController?.Dispose();   // ImGui GL resources

        // Now safe to dispose GL and window
        _gl?.Dispose();
        _window?.Dispose();

        // Non-GL resources can be disposed any time
        _db2Service?.Dispose();
        _context?.Dispose();
        _input?.Dispose();
    }
}
