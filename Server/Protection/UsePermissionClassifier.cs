using MathClaims.Models;

namespace MathClaims.Server.Protection;

public static class UsePermissionClassifier
{
    public static ClaimPermission RequiredForBlockCode(string? code)
    {
        var path = code?.ToLowerInvariant() ?? "";
        if (Contains(path, "water", "lava", "liquid", "brine", "milk")) return ClaimPermission.Liquids;
        if (Contains(path, "door", "gate", "trapdoor")) return ClaimPermission.Doors;
        if (Contains(path, "chest", "container", "barrel", "crate", "basket", "vessel", "storage", "shelf", "cabinet")) return ClaimPermission.Containers;
        return ClaimPermission.Machines;
    }

    private static bool Contains(string value, params string[] terms) => terms.Any(value.Contains);
}
