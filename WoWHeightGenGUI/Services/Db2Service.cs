using DBCD;
using DBCD.Providers;
using WoWHeightGenLib.Services;

namespace WoWHeightGenGUI.Services;

public record MapEntry(int Id, string Name, uint WdtFileDataId, int InstanceType)
{
    public string InstanceTypeName => InstanceType switch
    {
        0 => "World",
        1 => "Dungeon",
        2 => "Raid",
        3 => "Battleground",
        4 => "Arena",
        5 => "Scenario",
        _ => "Unknown"
    };
}

public class Db2Service : IDisposable
{
    private DBCD.DBCD? _dbcd;
    private GithubDBDProvider? _dbdProvider;
    private CascDbcProvider? _dbcProvider;
    private string? _build;

    public bool IsInitialized => _dbcd != null;

    public void Initialize(MapGenerationContext context)
    {
        _dbcProvider = new CascDbcProvider(context);
        _dbdProvider = new GithubDBDProvider();
        _build = context.VersionName;
        _dbcd = new DBCD.DBCD(_dbcProvider, _dbdProvider);
    }

    public List<MapEntry> GetMaps()
    {
        if (_dbcd == null || _build == null)
            throw new InvalidOperationException("Db2Service not initialized");

        var maps = new List<MapEntry>();

        try
        {
            var mapStorage = _dbcd.Load("Map", _build);

            foreach (var row in mapStorage.Values)
            {
                try
                {
                    var id = Convert.ToInt32(row["ID"]);
                    var name = row["MapName_lang"]?.ToString() ?? "";
                    var wdtFileDataId = Convert.ToUInt32(row["WdtFileDataID"]);
                    var instanceType = Convert.ToInt32(row["InstanceType"]);

                    // Skip maps without a WDT file
                    if (wdtFileDataId == 0)
                        continue;

                    maps.Add(new MapEntry(id, name, wdtFileDataId, instanceType));
                }
                catch
                {
                    // Skip rows that fail to parse
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load Map.db2: {ex.Message}");
        }

        return maps.OrderBy(m => m.Name).ToList();
    }

    public void Dispose()
    {
        _dbdProvider = null;
        _dbcProvider = null;
        _dbcd = null;
    }
}

/// <summary>
/// DBCD provider that reads DB2 files from CASC via MapGenerationContext
/// </summary>
public class CascDbcProvider : IDBCProvider
{
    private readonly MapGenerationContext _context;

    // Known FileDataIDs for common DB2 files
    private static readonly Dictionary<string, uint> KnownFileDataIds = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Map", 1349477 },
        { "AreaTable", 1349478 },
        { "MapDifficulty", 1367868 },
    };

    public CascDbcProvider(MapGenerationContext context)
    {
        _context = context;
    }

    public Stream StreamForTableName(string tableName, string build)
    {
        // Try to find the FileDataID for this table
        if (!KnownFileDataIds.TryGetValue(tableName, out var fileDataId))
        {
            throw new FileNotFoundException($"Unknown DB2 table: {tableName}. FileDataID not configured.");
        }

        if (!_context.FileExists(fileDataId))
        {
            throw new FileNotFoundException($"DB2 file not found in CASC: {tableName} (FileDataID: {fileDataId})");
        }

        return _context.OpenFile(fileDataId);
    }
}
