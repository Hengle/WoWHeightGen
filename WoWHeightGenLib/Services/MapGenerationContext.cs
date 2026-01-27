using CASCLib;
using WoWHeightGenLib.Configuration;

namespace WoWHeightGenLib.Services
{
    /// <summary>
    /// Manages the context for map generation operations, including CASC handler and configuration.
    /// </summary>
    public class MapGenerationContext : IDisposable
    {
        private readonly Dictionary<uint, (byte, byte, byte)> _areaColorTable = new();

        /// <summary>
        /// Gets the configuration settings for map generation.
        /// </summary>
        public MapGenerationConfig Config { get; }

        /// <summary>
        /// Gets the WoW installation path.
        /// </summary>
        public string InstallPath { get; }

        /// <summary>
        /// Gets the CASC product identifier.
        /// </summary>
        public string Product { get; }

        /// <summary>
        /// Gets the version name from the CASC configuration.
        /// </summary>
        public string VersionName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the CASC configuration.
        /// </summary>
        public CASCConfig? CascConfig { get; private set; }

        /// <summary>
        /// Gets the CASC handler for file operations.
        /// </summary>
        public CASCHandler? CascHandler { get; private set; }

        /// <summary>
        /// Gets the WoW-specific root handler.
        /// </summary>
        public WowRootHandler? WowRootHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the MapGenerationContext class.
        /// </summary>
        /// <param name="installPath">The WoW installation path.</param>
        /// <param name="product">The CASC product identifier.</param>
        /// <param name="config">Optional configuration settings. Uses default if not provided.</param>
        public MapGenerationContext(
            string installPath,
            string product,
            MapGenerationConfig? config = null)
        {
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
            Product = product ?? throw new ArgumentNullException(nameof(product));
            Config = config ?? MapGenerationConfig.Default;
        }

        /// <summary>
        /// Initializes the CASC handler and configuration.
        /// </summary>
        public void Initialize()
        {
            CascConfig = CASCConfig.LoadLocalStorageConfig(InstallPath, Product);
            CascHandler = CASCHandler.OpenStorage(CascConfig);
            VersionName = CascConfig.VersionName;

            WowRootHandler = CascHandler.Root as WowRootHandler
                ?? throw new InvalidOperationException("Invalid WoW root handler");
            WowRootHandler.SetFlags(Config.FirstInstalledLocale, false);
        }

        /// <summary>
        /// Gets or creates a random color for the specified area ID.
        /// </summary>
        /// <param name="areaId">The area ID.</param>
        /// <returns>An RGB color tuple.</returns>
        public (byte, byte, byte) GetOrCreateAreaColor(uint areaId)
        {
            if (!_areaColorTable.TryGetValue(areaId, out var color))
            {
                color = (
                    (byte)Random.Shared.Next(256),
                    (byte)Random.Shared.Next(256),
                    (byte)Random.Shared.Next(256)
                );
                _areaColorTable[areaId] = color;
            }
            return color;
        }

        /// <summary>
        /// Disposes the CASC handler resources.
        /// </summary>
        public void Dispose()
        {
            // CASCHandler doesn't implement IDisposable
            GC.SuppressFinalize(this);
        }
    }
}
