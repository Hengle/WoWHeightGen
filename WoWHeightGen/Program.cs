using WoWHeightGenLib.Configuration;
using WoWHeightGenLib.Models;
using WoWHeightGenLib.Services;

namespace WoWHeightGen
{
    internal class Program
    {
        static MapGenerationContext? _context;
        static HeightMapGenerator? _heightMapGenerator;
        static MinimapGenerator? _minimapGenerator;
        static AreaMapGenerator? _areaMapGenerator;
        static List<int>? wdtFileIDs;

        static void Main(string[] args)
        {
            GetWowInstallInfo();

            while (true)
            {
                if (!GetWDTInfo()) return;
                if (!GetTaskInfo()) return;
            }
        }

        static void GetWowInstallInfo()
        {
            string? installPath = null;
            string? product = null;

            while (true)
            {
                Console.WriteLine("Type \"exit\" to quit.");
                Console.WriteLine();

                // Try to detect installations from registry
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Scanning for WoW installations...");
                Console.ResetColor();

                var detectedInstalls = WowInstallationDetector.DetectInstallations();

                if (detectedInstalls.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Found {detectedInstalls.Count} WoW installation(s).");
                    Console.ResetColor();
                    Console.WriteLine();

                    var selected = InteractiveConsoleSelector.SelectFromList(
                        detectedInstalls,
                        install => install.DisplayName,
                        "Select a WoW installation (↑/↓ to navigate, Enter to select, Esc for manual):"
                    );

                    if (selected != null)
                    {
                        installPath = selected.InstallPath;
                        product = selected.ProductInfo.Product;

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Selected: {selected.DisplayName}");
                        Console.ResetColor();

                        // Try to initialize with the selected installation
                        if (TryInitializeContext(installPath, product))
                            return;

                        // If initialization failed, loop back to try again
                        continue;
                    }
                    // If user pressed Escape, fall through to manual input
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("No WoW installations detected via registry.");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                // Manual input
                PrintInfo("Enter WoW install path. Eg: ", "D:/Games/World of Warcraft");
                Console.WriteLine();
                if (GetConsoleString(out installPath)) continue;

                PrintInfo("Enter product. Eg: ", "wow, wowt, wow_beta");
                if (GetConsoleString(out product)) continue;

                if (TryInitializeContext(installPath, product))
                    return;
            }
        }

        static bool TryInitializeContext(string? installPath, string? product)
        {
            if (string.IsNullOrEmpty(installPath) || string.IsNullOrEmpty(product))
                return false;

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Initializing map generation context...");
                Console.ResetColor();

                _context = new MapGenerationContext(installPath, product);
                _context.Initialize();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Initialized successfully! Version: {_context.VersionName}");
                Console.ResetColor();

                // Instantiate generators
                _heightMapGenerator = new HeightMapGenerator(_context);
                _minimapGenerator = new MinimapGenerator(_context);
                _areaMapGenerator = new AreaMapGenerator(_context);

                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        static bool GetWDTInfo()
        {
            while (true)
            {
                PrintInfo("Enter WDT fileID. Eg: ", "782779");
                PrintInfo("Or enter WDT fileIDs separated by comma. Eg: ", "782779,790112,790796");
                Console.WriteLine();
                if (GetConsoleString(out string? inputString)) continue;
                if (inputString == null) continue;

                try
                {
                    inputString = inputString.Replace(" ", "");
                    string[] split = inputString.Split(',');

                    wdtFileIDs = new List<int>();

                    for (int i = 0; i < split.Length; i++)
                    {
                        if (int.TryParse(split[i], out int fileID))
                        {
                            wdtFileIDs.Add(fileID);
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                    continue;
                }
            }
        }

        static bool GetTaskInfo()
        {
            if (wdtFileIDs == null) return true;
            if (_heightMapGenerator == null || _minimapGenerator == null || _areaMapGenerator == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Generators not initialized.");
                Console.ResetColor();
                return false;
            }

            while (true)
            {
                PrintInfo("Pick a task: 1 - Export Height Map, 2 - Export Minimaps, 3 - Export Area Maps Eg: ", "1");
                Console.WriteLine();
                if (GetConsoleString(out string? inputString)) continue;

                try
                {
                    if (int.TryParse(inputString, out int taskType))
                    {
                        if (taskType == 1)
                        {
                            foreach (var fileID in wdtFileIDs)
                            {
                                Console.WriteLine($"Processing height map for WDT: {fileID}");
                                _heightMapGenerator.Generate(fileID, clampToAboveSea: false, clampToBelowSea: false);
                            }
                            return true;
                        }
                        else if (taskType == 2)
                        {
                            foreach (var fileID in wdtFileIDs)
                            {
                                Console.WriteLine($"Processing minimap for WDT: {fileID}");
                                _minimapGenerator.Generate(fileID);
                            }
                            return true;
                        }
                        else if (taskType == 3)
                        {
                            foreach (var fileID in wdtFileIDs)
                            {
                                Console.WriteLine($"Processing area map for WDT: {fileID}");
                                _areaMapGenerator.Generate(fileID);
                            }
                            return true;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Invalid task type.");
                            Console.ResetColor();
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                    continue;
                }
            }
        }

        static bool GetConsoleString(out string? value)
        {
            value = Console.ReadLine();

            if (value != null)
                if (value.Equals("exit")) return true;

            return false;
        }

        static void PrintInfo(string a, string b)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(a);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(b);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine();
        }
    }
}
