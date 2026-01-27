using TACTSharp;

namespace WoWHeightGenLib.Configuration
{
    /// <summary>
    /// Configuration settings for map generation operations.
    /// </summary>
    public class MapGenerationConfig
    {
        /// <summary>
        /// The default locale to use for WoW data files.
        /// </summary>
        public RootInstance.LocaleFlags FirstInstalledLocale { get; set; } = RootInstance.LocaleFlags.enUS;

        /// <summary>
        /// The output directory path for generated map images.
        /// </summary>
        public string OutputPath { get; set; } = "Output";

        /// <summary>
        /// The size of the map grid (64x64 tiles).
        /// </summary>
        public int MapSize { get; set; } = 64;

        /// <summary>
        /// The resolution of each height chunk (128x128 pixels).
        /// </summary>
        public int HeightChunkResolution { get; set; } = 128;

        /// <summary>
        /// The total resolution of the generated height map (8192x8192 pixels).
        /// </summary>
        public int HeightMapResolution { get; set; } = 8192;

        /// <summary>
        /// Gets the default configuration instance.
        /// </summary>
        public static MapGenerationConfig Default { get; } = new();
    }
}
