using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MathClaims.Server.Protection;

public sealed class OwnClaimPvpGuard : EntityBehavior
{
    private readonly System.Func<Entity, bool> shouldBlock;

    public OwnClaimPvpGuard(Entity entity, System.Func<Entity, bool> shouldBlock) : base(entity)
    {
        this.shouldBlock = shouldBlock;
    }

    public override string PropertyName() => "mathclaims-own-claim-pvp";

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        if (damage <= 0 || damageSource.GetCauseEntity() is not EntityPlayer attacker) return;
        if (PvpSafetyPolicy.ShouldBlock(shouldBlock(entity), shouldBlock(attacker))) damage = 0;
    }
}
