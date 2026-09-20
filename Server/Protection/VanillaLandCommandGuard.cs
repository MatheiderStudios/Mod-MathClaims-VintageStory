namespace MathClaims.Server.Protection;

public static class VanillaLandCommandGuard
{
    public static bool IsVanillaLandCommand(string? message)
    {
        var text = message?.TrimStart() ?? "";
        if (!text.StartsWith("/land", StringComparison.OrdinalIgnoreCase)) return false;
        return text.Length == 5 || char.IsWhiteSpace(text[5]);
    }
}
