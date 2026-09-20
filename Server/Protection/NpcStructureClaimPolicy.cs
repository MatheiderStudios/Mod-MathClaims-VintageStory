namespace MathClaims.Server.Protection;

public static class NpcStructureClaimPolicy
{
    private static readonly HashSet<string> KnownOwnerCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "custommessage-nadiya",
        "custommessage-tobias",
        "custommessage-treasurehunter"
    };

    public static bool IsNpcStructureOwner(string? ownerCode) => ownerCode is not null && KnownOwnerCodes.Contains(ownerCode);
}
