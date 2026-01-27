namespace WoWHeightGen
{
    /// <summary>
    /// Represents a World of Warcraft variant configuration including registry information
    /// and CASC product identifier.
    /// </summary>
    public record WowProductInfo(
        string UninstallName,
        string FolderName,
        string ExecutableName,
        string Product)
    {
        /// <summary>
        /// Returns all known WoW variants with their registry and installation information.
        /// </summary>
        public static IReadOnlyList<WowProductInfo> GetKnownVariants()
        {
            return new List<WowProductInfo>
            {
                new("World of Warcraft", @"_retail_", "WoW.exe", "wow"),
                new("World of Warcraft Event", @"_event1_", "WoWB.exe", "wowe1"),
                new("World of Warcraft Public Test", @"_ptr_", "WoWT.exe", "wowt"),
                new("World of Warcraft Public Test 3", @"_xptr_", "WoWT.exe", "wowxptr"),
                new("World of Warcraft Classic", @"_classic_", "WoWClassic.exe", "wow_classic"),
                new("World of Warcraft Classic PTR", @"_classic_ptr_", "WoWClassicT.exe", "wow_classic_ptr"),
                new("World of Warcraft Classic Era", @"_classic_era_", "WoWClassic.exe", "wow_classic_era"),
                new("World of Warcraft Classic Era PTR", @"_classic_era_ptr_", "WoWClassicT.exe", "wow_classic_era_ptr"),
                new("World of Warcraft Classic Anniversary", @"_anniversary_", "WowClassic.exe", "wow_anniversary"),
                new("World of Warcraft Beta", @"_beta_", "WoWB.exe", "wow_beta"),
                new("World of Warcraft Classic Beta", @"_classic_beta_", "WoWClassicB.exe", "wow_classic_beta"),
                new("World of Warcraft Alpha", @"_alpha_", "WoWB.exe", "wowdev"),
                new("World of Warcraft Submission", @"_submission_", "WoWT.exe", "wowz"),
                new("World of Warcraft Submission", @"_submission_", "WoWClassicT.exe", "wowz")
            };
        }
    }
}
