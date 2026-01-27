using Microsoft.Win32;

namespace WoWHeightGen
{
    /// <summary>
    /// Detects installed World of Warcraft installations via Windows Registry.
    /// </summary>
    public static class WowInstallationDetector
    {
        private const string UninstallRegistryPath =
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        /// <summary>
        /// Scans the Windows Registry for installed WoW variants and returns valid installations.
        /// </summary>
        public static List<DetectedWowInstallation> DetectInstallations()
        {
            var installations = new List<DetectedWowInstallation>();
            var variants = WowProductInfo.GetKnownVariants();

            // Group variants by UninstallName to avoid duplicate registry lookups
            var variantGroups = variants.GroupBy(v => v.UninstallName);

            foreach (var group in variantGroups)
            {
                string uninstallName = group.Key;
                string? installPath = GetInstallPathFromRegistry(uninstallName);

                if (string.IsNullOrEmpty(installPath))
                    continue;

                // Check each variant for this uninstall name
                foreach (var variant in group)
                {
                    var detected = new DetectedWowInstallation(
                        installPath,
                        variant,
                        Path.Combine(installPath, variant.FolderName)
                    );

                    // Only add if the installation is valid
                    if (detected.IsValid())
                    {
                        installations.Add(detected);
                    }
                }
            }

            return installations;
        }

        /// <summary>
        /// Retrieves the installation path from the Windows Registry for a given uninstall name.
        /// </summary>
        private static string? GetInstallPathFromRegistry(string uninstallName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"{UninstallRegistryPath}\{uninstallName}");

                if (key == null)
                    return null;

                var location = key.GetValue("InstallLocation") as string;

                // Normalize path separators and remove trailing slashes
                if (!string.IsNullOrEmpty(location))
                {
                    return location.Replace('/', Path.DirectorySeparatorChar)
                                  .TrimEnd(Path.DirectorySeparatorChar);
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log but don't fail - just skip this registry entry
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Warning: Failed to read registry for {uninstallName}: {ex.Message}");
                Console.ResetColor();
                return null;
            }
        }
    }
}
