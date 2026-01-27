using TACTSharp;
using WoWHeightGenLib.Configuration;

namespace WoWHeightGenLib.Services
{
    /// <summary>
    /// Manages the context for map generation operations using TACTSharp for file access.
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
        /// Gets the version name from the build configuration.
        /// </summary>
        public string VersionName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the TACTSharp build instance for file operations.
        /// </summary>
        public BuildInstance? Build { get; private set; }

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
        /// Initializes the TACTSharp build instance and loads configuration.
        /// </summary>
        public void Initialize()
        {
            // Step 1: Initialize build instance and configure settings
            Build = new BuildInstance();
            Build.Settings.BaseDir = InstallPath;
            Build.Settings.Product = Product.ToLowerInvariant();
            Build.Settings.Locale = Config.FirstInstalledLocale;

            // Step 2: Discover config files from .build.info
            var (buildConfig, cdnConfig) = DiscoverConfigFiles(InstallPath, Product);

            // Step 3: Load configs and build
            Build.LoadConfigs(buildConfig, cdnConfig);
            Build.Load();

            // Step 4: Extract version name
            VersionName = ExtractVersionName(Build);
        }

        private (string buildConfig, string cdnConfig) DiscoverConfigFiles(string installPath, string product)
        {
            // Parse .build.info to get config hashes
            string buildInfoPath = Path.Combine(installPath, ".build.info");

            if (!File.Exists(buildInfoPath))
                throw new FileNotFoundException($".build.info not found at {buildInfoPath}");

            // Read and parse .build.info
            var lines = File.ReadAllLines(buildInfoPath);
            if (lines.Length < 2)
                throw new InvalidDataException(".build.info has insufficient data");

            // Parse header line to get column indices
            var headers = lines[0].Split('|');
            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                var headerName = headers[i].Split('!')[0];
                headerMap[headerName] = i;
            }

            // Find the matching product line
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split('|');

                // Check if Product column exists and matches
                if (headerMap.TryGetValue("Product", out int productIdx) &&
                    values[productIdx].Equals(product, StringComparison.OrdinalIgnoreCase))
                {
                    string buildKey = values[headerMap["Build Key"]];
                    string cdnKey = values[headerMap["CDN Key"]];

                    // Determine data folder (usually "Data" for WoW)
                    string dataFolder = "Data";

                    // Construct config file paths using the hash structure
                    string buildCfgPath = Path.Combine(installPath, dataFolder, "config",
                        buildKey.Substring(0, 2), buildKey.Substring(2, 2), buildKey);
                    string cdnCfgPath = Path.Combine(installPath, dataFolder, "config",
                        cdnKey.Substring(0, 2), cdnKey.Substring(2, 2), cdnKey);

                    if (!File.Exists(buildCfgPath))
                        throw new FileNotFoundException($"Build config not found: {buildCfgPath}");
                    if (!File.Exists(cdnCfgPath))
                        throw new FileNotFoundException($"CDN config not found: {cdnCfgPath}");

                    // Store version name for later
                    if (headerMap.TryGetValue("Version", out int versionIdx))
                        VersionName = values[versionIdx];

                    return (buildCfgPath, cdnCfgPath);
                }
            }

            throw new InvalidDataException($"Product '{product}' not found in .build.info");
        }

        private string ExtractVersionName(BuildInstance build)
        {
            // If already set by DiscoverConfigFiles, use that
            if (!string.IsNullOrEmpty(VersionName))
                return VersionName;

            // Try to extract from BuildConfig
            if (build.BuildConfig?.Values != null &&
                build.BuildConfig.Values.TryGetValue("build-name", out var buildName) &&
                buildName != null &&
                buildName.Length > 0)
            {
                return buildName[0];
            }

            return "Unknown";
        }

        /// <summary>
        /// Checks if a file exists by FileDataID.
        /// </summary>
        /// <param name="fileDataID">The file data ID.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        public bool FileExists(uint fileDataID)
        {
            return Build?.Root?.FileExists(fileDataID) ?? false;
        }

        /// <summary>
        /// Opens a file by FileDataID and returns a stream.
        /// </summary>
        /// <param name="fileDataID">The file data ID.</param>
        /// <returns>A stream containing the file data.</returns>
        public Stream OpenFile(uint fileDataID)
        {
            if (Build == null)
                throw new InvalidOperationException("Build not initialized");

            byte[] fileData = Build.OpenFileByFDID(fileDataID);
            return new MemoryStream(fileData);
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
