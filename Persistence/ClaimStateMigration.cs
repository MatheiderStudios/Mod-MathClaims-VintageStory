using MathClaims.Models;

namespace MathClaims.Persistence;

public static class ClaimStateMigration
{
    public static bool Upgrade(ClaimState state)
    {
        if (state.SchemaVersion >= ClaimGeometry.CurrentSchemaVersion) return false;

        foreach (var property in state.Properties.Values)
        {
            property.Chunks = property.Chunks
                .SelectMany(old => new[]
                {
                    new ChunkColumn(old.Dimension, old.X * 2, old.Z * 2),
                    new ChunkColumn(old.Dimension, old.X * 2 + 1, old.Z * 2),
                    new ChunkColumn(old.Dimension, old.X * 2, old.Z * 2 + 1),
                    new ChunkColumn(old.Dimension, old.X * 2 + 1, old.Z * 2 + 1)
                })
                .ToHashSet();
        }
        state.SchemaVersion = ClaimGeometry.CurrentSchemaVersion;
        return true;
    }
}
