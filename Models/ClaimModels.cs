using System.Text.RegularExpressions;

namespace MathClaims.Models;

[Flags]
public enum ClaimPermission
{
    None = 0, BuildBreak = 1, Doors = 2, Containers = 4, Machines = 8,
    Animals = 16, Liquids = 32, Management = 64,
    All = BuildBreak | Doors | Containers | Machines | Animals | Liquids | Management
}

public readonly record struct ChunkColumn(int Dimension, int X, int Z)
{
    public IEnumerable<ChunkColumn> Neighbors()
    {
        yield return this with { X = X + 1 }; yield return this with { X = X - 1 };
        yield return this with { Z = Z + 1 }; yield return this with { Z = Z - 1 };
    }
}

public static class ClaimGeometry
{
    public const int CellSize = 16;
    public const int LegacyCellSize = 32;
    public const int CurrentSchemaVersion = 2;
    public static int ToCell(int blockCoordinate) => FloorDiv(blockCoordinate, CellSize);
    public static int ToWorld(int cellCoordinate) => cellCoordinate * CellSize;
    public static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
}

public readonly record struct ExplorationSector(int Dimension, int X, int Z)
{
    public const int CellsPerSector = 2;
    public static ExplorationSector FromCell(ChunkColumn cell) => new(cell.Dimension,
        ClaimGeometry.FloorDiv(cell.X, CellsPerSector), ClaimGeometry.FloorDiv(cell.Z, CellsPerSector));
}

public sealed class ClientExplorationState
{
    public List<ExplorationSector> Sectors { get; set; } = [];
}

public sealed class ClaimProperty
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string OwnerUid { get; set; } = "";
    public HashSet<ChunkColumn> Chunks { get; set; } = [];
    public Dictionary<string, ClaimPermission> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool PvpEnabled { get; set; }
}

public sealed class PlayerActivity { public double OfflineUptimeSeconds { get; set; } }

public sealed class ClaimState
{
    public int SchemaVersion { get; set; } = ClaimGeometry.CurrentSchemaVersion;
    public Dictionary<Guid, ClaimProperty> Properties { get; set; } = [];
    public Dictionary<string, PlayerActivity> PlayerActivity { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ClaimConfig
{
    public bool Enabled { get; set; } = true;
    public int MaxPropertiesPerPlayer { get; set; } = 3;
    public int MaxChunksPerPlayer { get; set; } = 64;
    public int SpawnNoClaimSize { get; set; } = 3;
    public bool SpawnProtectionEnabled { get; set; }
    public ClaimPermission SpawnProtectionPermissions { get; set; } = ClaimPermission.All;
    public bool MinimumDistanceEnabled { get; set; }
    public int MinimumDistanceChunks { get; set; }
    public bool ExpirationEnabled { get; set; }
    public double ExpirationUptimeSeconds { get; set; } = TimeSpan.FromDays(30).TotalSeconds;
    public bool AdminIgnoresLimits { get; set; }
    public bool ShowClaimsOnMinimap { get; set; } = true;
    public bool ShowClaimsInUndiscoveredAreas { get; set; }
}

public static class ClaimName
{
    private static readonly Regex Safe = new("^[\\p{L}\\p{N}][\\p{L}\\p{N} _.'-]{0,47}$", RegexOptions.CultureInvariant);
    public static bool TryNormalize(string? value, out string name)
    {
        name = (value ?? "").Trim();
        return Safe.IsMatch(name);
    }
}

public enum OverlayColor { Green, Yellow, Red }
