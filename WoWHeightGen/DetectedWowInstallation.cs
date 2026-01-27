namespace WoWHeightGen
{
    /// <summary>
    /// Represents a detected WoW installation with its path and variant information.
    /// </summary>
    public record DetectedWowInstallation(
        string InstallPath,
        WowProductInfo ProductInfo,
        string FullPath)
    {
        /// <summary>
        /// Gets a formatted display name for console output.
        /// </summary>
        public string DisplayName =>
            $"{ProductInfo.UninstallName} ({ProductInfo.Product}) - {InstallPath}";

        /// <summary>
        /// Validates that the installation directory and executable exist.
        /// </summary>
        public bool IsValid()
        {
            return Directory.Exists(FullPath) &&
                   File.Exists(Path.Combine(FullPath, ProductInfo.ExecutableName));
        }
    }
}
