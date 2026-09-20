using ProtoBuf;

namespace MathClaims.Networking;

[ProtoContract]
public sealed class CreateClaimRequest
{
    [ProtoMember(1)] public int Dimension { get; set; }
    [ProtoMember(2)] public int ChunkX { get; set; }
    [ProtoMember(3)] public int ChunkZ { get; set; }
    [ProtoMember(4)] public string Name { get; set; } = "";
    [ProtoMember(5)] public List<ClaimChunkRequest> Columns { get; set; } = [];
}

[ProtoContract]
public sealed class ClaimChunkRequest
{
    [ProtoMember(1)] public int Dimension { get; set; }
    [ProtoMember(2)] public int ChunkX { get; set; }
    [ProtoMember(3)] public int ChunkZ { get; set; }
}

[ProtoContract]
public sealed class ClaimPreflightRequest
{
    [ProtoMember(1)] public List<ClaimChunkRequest> Columns { get; set; } = [];
}

[ProtoContract]
public sealed class ClaimPreflightResult
{
    [ProtoMember(1)] public bool Allowed { get; set; }
    [ProtoMember(2)] public string Message { get; set; } = "";
    [ProtoMember(3)] public bool AddsToAdjacentProperty { get; set; }
}

[ProtoContract]
public sealed class ClaimVisualSnapshot
{
    [ProtoMember(1)] public List<ClaimVisualChunk> Chunks { get; set; } = [];
    [ProtoMember(2)] public bool SpawnProtected { get; set; }
    [ProtoMember(3)] public int SpawnDimension { get; set; }
    [ProtoMember(4)] public int SpawnMinChunkX { get; set; }
    [ProtoMember(5)] public int SpawnMaxChunkX { get; set; }
    [ProtoMember(6)] public int SpawnMinChunkZ { get; set; }
    [ProtoMember(7)] public int SpawnMaxChunkZ { get; set; }
    [ProtoMember(8)] public bool ShowClaimsOnMinimap { get; set; }
    [ProtoMember(9)] public bool ShowClaimsInUndiscoveredAreas { get; set; }
}

[ProtoContract]
public sealed class ClaimVisualChunk
{
    [ProtoMember(1)] public int Dimension { get; set; }
    [ProtoMember(2)] public int ChunkX { get; set; }
    [ProtoMember(3)] public int ChunkZ { get; set; }
    [ProtoMember(4)] public int Color { get; set; }
    [ProtoMember(5)] public string PropertyId { get; set; } = "";
    [ProtoMember(6)] public string PropertyName { get; set; } = "";
    [ProtoMember(7)] public string OwnerName { get; set; } = "";
    [ProtoMember(8)] public int PropertyChunkCount { get; set; }
    [ProtoMember(9)] public bool CanManage { get; set; }
    [ProtoMember(10)] public bool CanAdminister { get; set; }
    [ProtoMember(11)] public bool PvpEnabled { get; set; }
}

[ProtoContract]
public sealed class ClaimMemberDirectoryRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
}

[ProtoContract]
public sealed class ClaimMemberDirectorySnapshot
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public List<ClaimMemberDirectoryEntry> Members { get; set; } = [];
}

[ProtoContract]
public sealed class ClaimMemberDirectoryEntry
{
    [ProtoMember(1)] public string PlayerUid { get; set; } = "";
    [ProtoMember(2)] public string PlayerName { get; set; } = "";
    [ProtoMember(3)] public int Permissions { get; set; }
    [ProtoMember(4)] public bool IsOwner { get; set; }
}

[ProtoContract]
public sealed class SetClaimPvpRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public bool Enabled { get; set; }
}

[ProtoContract]
public sealed class RenameClaimRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public string Name { get; set; } = "";
}

[ProtoContract]
public sealed class SetPermissionsRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public string PlayerName { get; set; } = "";
    [ProtoMember(3)] public int Permissions { get; set; }
}

[ProtoContract]
public sealed class TransferClaimRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public string RecipientName { get; set; } = "";
}

[ProtoContract]
public sealed class AbandonClaimRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
}

[ProtoContract]
public sealed class ClaimSnapshotRequest
{
}

[ProtoContract]
public sealed class AdminConfigSnapshot
{
    [ProtoMember(1)] public bool Enabled { get; set; }
    [ProtoMember(2)] public bool ShowClaimsOnMinimap { get; set; }
    [ProtoMember(3)] public bool ShowClaimsInUndiscoveredAreas { get; set; }
    [ProtoMember(4)] public bool AdminIgnoresLimits { get; set; }
    [ProtoMember(5)] public int MaxPropertiesPerPlayer { get; set; }
    [ProtoMember(6)] public int MaxChunksPerPlayer { get; set; }
    [ProtoMember(7)] public bool MinimumDistanceEnabled { get; set; }
    [ProtoMember(8)] public int MinimumDistanceChunks { get; set; }
    [ProtoMember(9)] public int SpawnNoClaimSize { get; set; }
    [ProtoMember(10)] public bool SpawnProtectionEnabled { get; set; }
    [ProtoMember(11)] public int SpawnProtectionPermissions { get; set; }
    [ProtoMember(12)] public bool ExpirationEnabled { get; set; }
    [ProtoMember(13)] public double ExpirationUptimeSeconds { get; set; }
    [ProtoMember(14)] public int PropertyCount { get; set; }
    [ProtoMember(15)] public int LegacyClaimCount { get; set; }
    [ProtoMember(16)] public int NpcStructureClaimCount { get; set; }
}

[ProtoContract]
public sealed class UpdateAdminConfigRequest
{
    [ProtoMember(1)] public bool Enabled { get; set; }
    [ProtoMember(2)] public bool ShowClaimsOnMinimap { get; set; }
    [ProtoMember(3)] public bool ShowClaimsInUndiscoveredAreas { get; set; }
    [ProtoMember(4)] public bool AdminIgnoresLimits { get; set; }
    [ProtoMember(5)] public int MaxPropertiesPerPlayer { get; set; }
    [ProtoMember(6)] public int MaxChunksPerPlayer { get; set; }
    [ProtoMember(7)] public bool MinimumDistanceEnabled { get; set; }
    [ProtoMember(8)] public int MinimumDistanceChunks { get; set; }
    [ProtoMember(9)] public int SpawnNoClaimSize { get; set; }
    [ProtoMember(10)] public bool SpawnProtectionEnabled { get; set; }
    [ProtoMember(11)] public int SpawnProtectionPermissions { get; set; }
    [ProtoMember(12)] public bool ExpirationEnabled { get; set; }
    [ProtoMember(13)] public double ExpirationUptimeSeconds { get; set; }
}

[ProtoContract]
public sealed class ReleaseNpcStructureClaimsRequest { }

[ProtoContract]
public sealed class ForceTransferClaimRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public string RecipientName { get; set; } = "";
}

[ProtoContract]
public sealed class DeleteClaimRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
}

[ProtoContract]
public sealed class RemoveClaimChunkRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public int Dimension { get; set; }
    [ProtoMember(3)] public int ChunkX { get; set; }
    [ProtoMember(4)] public int ChunkZ { get; set; }
}

[ProtoContract]
public sealed class RemoveClaimChunksRequest
{
    [ProtoMember(1)] public string PropertyId { get; set; } = "";
    [ProtoMember(2)] public List<ClaimChunkRequest> Columns { get; set; } = [];
}
