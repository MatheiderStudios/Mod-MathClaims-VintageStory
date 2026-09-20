namespace MathClaims.Server.Protection;

public static class PvpSafetyPolicy
{
    public static bool ShouldBlock(bool victimOwnPvpDisabled, bool attackerOwnPvpDisabled) => victimOwnPvpDisabled || attackerOwnPvpDisabled;
}
