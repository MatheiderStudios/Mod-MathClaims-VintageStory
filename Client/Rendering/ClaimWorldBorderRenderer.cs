using MathClaims.Client.Map;
using MathClaims.Models;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace MathClaims.Client.Rendering;

public sealed class ClaimWorldBorderRenderer(ICoreClientAPI api) : IRenderer
{
    private const int RadiusChunks = 12;
    private const int GroundStep = 1;
    private readonly HashSet<(int X, int Z)> verticals = [];
    private bool enabled;
    private BlockPos? origin;

    public double RenderOrder => 0.65;
    public int RenderRange => RadiusChunks * ClaimGeometry.CellSize;

    public bool Toggle()
    {
        enabled = !enabled;
        api.ShowChatMessage(enabled ? "MathClaims: bordas de propriedades ativadas." : "MathClaims: bordas de propriedades desativadas.");
        return enabled;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!enabled || api.World.Player?.Entity is not { } player) return;
        var playerChunkX = ClaimGeometry.ToCell((int)Math.Floor(player.Pos.X));
        var playerChunkZ = ClaimGeometry.ToCell((int)Math.Floor(player.Pos.Z));
        var dimension = player.Pos.Dimension;
        origin = new BlockPos((int)Math.Floor(player.CameraPos.X), (int)Math.Floor(player.CameraPos.Y), (int)Math.Floor(player.CameraPos.Z), dimension);
        verticals.Clear();

        foreach (var (column, property) in ClaimMapLayer.NearbyWorldClaims(dimension, playerChunkX, playerChunkZ, RadiusChunks))
        {
            DrawClaimBoundary(column, property, ClaimVisualPalette.ForWorldLine(property.Color));
        }

        if (ClaimMapLayer.SpawnRegion is { } spawn && spawn.Dimension == dimension &&
            DistanceToRectangle(playerChunkX, playerChunkZ, spawn) <= RadiusChunks)
        {
            DrawRectangle(ClaimGeometry.ToWorld(spawn.MinChunkX), ClaimGeometry.ToWorld(spawn.MinChunkZ), ClaimGeometry.ToWorld(spawn.MaxChunkX + 1), ClaimGeometry.ToWorld(spawn.MaxChunkZ + 1), ClaimVisualPalette.Spawn);
        }
    }

    private void DrawClaimBoundary(ChunkColumn column, ClaimMapProperty property, int color)
    {
        var x = ClaimGeometry.ToWorld(column.X);
        var z = ClaimGeometry.ToWorld(column.Z);
        var size = ClaimGeometry.CellSize;
        if (!SameProperty(column with { X = column.X - 1 }, property)) DrawEdge(x, z, x, z + size, color);
        if (!SameProperty(column with { X = column.X + 1 }, property)) DrawEdge(x + size, z, x + size, z + size, color);
        if (!SameProperty(column with { Z = column.Z - 1 }, property)) DrawEdge(x, z, x + size, z, color);
        if (!SameProperty(column with { Z = column.Z + 1 }, property)) DrawEdge(x, z + size, x + size, z + size, color);
    }

    private static bool SameProperty(ChunkColumn column, ClaimMapProperty property) =>
        ClaimMapLayer.TryGetClaim(column, out var adjacent) && adjacent.Id == property.Id;

    private void DrawRectangle(int minX, int minZ, int maxX, int maxZ, int color)
    {
        DrawEdge(minX, minZ, minX, maxZ, color);
        DrawEdge(maxX, minZ, maxX, maxZ, color);
        DrawEdge(minX, minZ, maxX, minZ, color);
        DrawEdge(minX, maxZ, maxX, maxZ, color);
    }

    private void DrawEdge(int x1, int z1, int x2, int z2, int color)
    {
        DrawVertical(x1, z1, color);
        DrawVertical(x2, z2, color);
        var top = api.World.MapSizeY - 1.15f;
        Line(x1, top, z1, x2, top, z2, color);
        var steps = Math.Max(1, Math.Max(Math.Abs(x2 - x1), Math.Abs(z2 - z1)) / GroundStep);
        for (var i = 0; i < steps; i++)
        {
            var t1 = (float)i / steps;
            var t2 = (float)(i + 1) / steps;
            var ax = x1 + (x2 - x1) * t1;
            var az = z1 + (z2 - z1) * t1;
            var bx = x1 + (x2 - x1) * t2;
            var bz = z1 + (z2 - z1) * t2;
            var ay = Surface(ax, az);
            var by = Surface(bx, bz);
            Line(ax, ay, az, bx, ay, bz, color);
            if (Math.Abs(by - ay) > 0.001f) Line(bx, ay, bz, bx, by, bz, color);
        }
    }

    private void DrawVertical(int x, int z, int color)
    {
        if (!verticals.Add((x, z))) return;
        Line(x, Surface(x, z), z, x, api.World.MapSizeY - 1.15f, z, color);
    }

    private float Surface(float x, float z) => api.World.BlockAccessor.GetRainMapHeightAt((int)Math.Floor(x), (int)Math.Floor(z)) + 1.06f;

    private void Line(float x1, float y1, float z1, float x2, float y2, float z2, int color)
    {
        if (origin is null) return;
        api.Render.RenderLine(origin, x1 - origin.X, y1 - origin.Y, z1 - origin.Z, x2 - origin.X, y2 - origin.Y, z2 - origin.Z, color);
    }

    private static int DistanceToRectangle(int x, int z, SpawnVisualRegion region)
    {
        var dx = x < region.MinChunkX ? region.MinChunkX - x : x > region.MaxChunkX ? x - region.MaxChunkX : 0;
        var dz = z < region.MinChunkZ ? region.MinChunkZ - z : z > region.MaxChunkZ ? z - region.MaxChunkZ : 0;
        return Math.Max(dx, dz);
    }
    public void Dispose() { }
}
