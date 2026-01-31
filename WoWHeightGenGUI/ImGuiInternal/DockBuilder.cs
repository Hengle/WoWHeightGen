using System.Numerics;
using System.Runtime.InteropServices;

namespace WoWHeightGenGUI.ImGuiInternal;

/// <summary>
/// P/Invoke bindings for ImGui DockBuilder internal API.
/// These functions allow programmatic creation of dock layouts at startup.
/// Requires a cimgui.dll built with internal API exports (e.g., from DearImguiSharp).
/// </summary>
public static class DockBuilder
{
    private const string CimguiLib = "cimgui";

    #region Native P/Invoke Declarations

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderDockWindow")]
    private static extern void igDockBuilderDockWindow(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string window_name,
        uint node_id);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderAddNode")]
    private static extern uint igDockBuilderAddNode(uint node_id, int flags);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderRemoveNode")]
    private static extern void igDockBuilderRemoveNode(uint node_id);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderSetNodeSize")]
    private static extern void igDockBuilderSetNodeSize(uint node_id, ImVec2 size);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderSetNodePos")]
    private static extern void igDockBuilderSetNodePos(uint node_id, ImVec2 pos);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderSplitNode")]
    private static extern unsafe uint igDockBuilderSplitNode(
        uint node_id,
        int split_dir,
        float size_ratio_for_node_at_dir,
        uint* out_id_at_dir,
        uint* out_id_at_opposite_dir);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderFinish")]
    private static extern void igDockBuilderFinish(uint node_id);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderGetNode")]
    private static extern IntPtr igDockBuilderGetNode(uint node_id);

    [DllImport(CimguiLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDockBuilderGetCentralNode")]
    private static extern IntPtr igDockBuilderGetCentralNode(uint node_id);

    #endregion

    #region Public API

    /// <summary>
    /// Dock a window to a specific dock node.
    /// </summary>
    /// <param name="windowName">The window title to dock</param>
    /// <param name="nodeId">The dock node ID to dock to</param>
    public static void DockWindow(string windowName, uint nodeId)
    {
        igDockBuilderDockWindow(windowName, nodeId);
    }

    /// <summary>
    /// Create a new dock node.
    /// </summary>
    /// <param name="nodeId">The ID for the new node (0 to auto-generate)</param>
    /// <param name="flags">Node flags (use DockNodeFlags.DockSpace for main dockspace)</param>
    /// <returns>The created node's ID</returns>
    public static uint AddNode(uint nodeId = 0, DockNodeFlags flags = DockNodeFlags.None)
    {
        return igDockBuilderAddNode(nodeId, (int)flags);
    }

    /// <summary>
    /// Remove a dock node and all its contents.
    /// Call this before AddNode to clear existing layout.
    /// </summary>
    public static void RemoveNode(uint nodeId)
    {
        igDockBuilderRemoveNode(nodeId);
    }

    /// <summary>
    /// Set the size of a dock node.
    /// </summary>
    public static void SetNodeSize(uint nodeId, Vector2 size)
    {
        igDockBuilderSetNodeSize(nodeId, new ImVec2 { X = size.X, Y = size.Y });
    }

    /// <summary>
    /// Set the position of a dock node.
    /// </summary>
    public static void SetNodePos(uint nodeId, Vector2 pos)
    {
        igDockBuilderSetNodePos(nodeId, new ImVec2 { X = pos.X, Y = pos.Y });
    }

    /// <summary>
    /// Split a dock node into two parts.
    /// </summary>
    /// <param name="nodeId">The node to split</param>
    /// <param name="direction">Direction to split</param>
    /// <param name="ratio">Size ratio for the new node (0.0-1.0)</param>
    /// <param name="outIdAtDir">Output: ID of the new node in the split direction</param>
    /// <param name="outIdAtOpposite">Output: ID of the remaining node</param>
    /// <returns>The ID of the newly created node</returns>
    public static unsafe uint SplitNode(
        uint nodeId,
        Direction direction,
        float ratio,
        out uint outIdAtDir,
        out uint outIdAtOpposite)
    {
        uint atDir = 0;
        uint atOpposite = 0;
        var result = igDockBuilderSplitNode(nodeId, (int)direction, ratio, &atDir, &atOpposite);
        outIdAtDir = atDir;
        outIdAtOpposite = atOpposite;
        return result;
    }

    /// <summary>
    /// Finalize the dock layout. Must be called after all DockWindow calls.
    /// </summary>
    public static void Finish(uint nodeId)
    {
        igDockBuilderFinish(nodeId);
    }

    /// <summary>
    /// Check if a dock node exists.
    /// </summary>
    public static bool NodeExists(uint nodeId)
    {
        return igDockBuilderGetNode(nodeId) != IntPtr.Zero;
    }

    /// <summary>
    /// Get the central node of a dockspace (the remaining area after splits).
    /// </summary>
    public static IntPtr GetCentralNode(uint nodeId)
    {
        return igDockBuilderGetCentralNode(nodeId);
    }

    #endregion

    #region Helper Struct

    /// <summary>
    /// ImVec2 struct matching cimgui's layout for P/Invoke.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ImVec2
    {
        public float X;
        public float Y;
    }

    #endregion
}

/// <summary>
/// Split direction for DockBuilder.SplitNode
/// </summary>
public enum Direction
{
    Left = 0,
    Right = 1,
    Up = 2,
    Down = 3
}

/// <summary>
/// Flags for DockBuilder.AddNode
/// </summary>
[Flags]
public enum DockNodeFlags
{
    None = 0,
    /// <summary>
    /// Mark this node as a DockSpace (required for root dockspace nodes)
    /// </summary>
    DockSpace = 1 << 10
}
