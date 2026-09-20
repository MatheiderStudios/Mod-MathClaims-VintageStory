using MathClaims.Client.Gui;
using MathClaims.Client.Map;
using MathClaims.Client.Rendering;
using MathClaims.Logging;
using MathClaims.Models;
using MathClaims.Networking;
using MathClaims.Persistence;
using MathClaims.Server.Claims;
using MathClaims.Server.Protection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace MathClaims;

public sealed class MathClaimsModSystem : ModSystem
{
    private ICoreServerAPI? sapi;
    private Vintagestory.API.Client.ICoreClientAPI? capi;
    private ClaimConfig config = new();
    private ClaimEngine? engine;
    private JsonClaimStore? store;
    private AuditLog? audit;
    private IServerNetworkChannel? serverChannel;
    private long tickListener;
    private ClaimDialogController? dialogs;
    private ClaimWorldBorderRenderer? worldBorders;
    private long clientExplorationListener;

    public override double ExecuteOrder() => -0.01;

    public override void Start(ICoreAPI api)
    {
        api.ModLoader.GetModSystem<WorldMapManager>(true).RegisterMapLayer<ClaimMapLayer>("mathclaims", 1.25);
    }

    public override void StartClientSide(Vintagestory.API.Client.ICoreClientAPI api)
    {
        capi = api;
        ClaimMapLayer.LoadClientExploration(api);
        var channel = api.Network.RegisterChannel("mathclaims")
            .RegisterMessageType(typeof(CreateClaimRequest))
            .RegisterMessageType(typeof(ClaimChunkRequest))
            .RegisterMessageType(typeof(ClaimPreflightRequest))
            .RegisterMessageType(typeof(ClaimPreflightResult))
            .RegisterMessageType(typeof(ClaimVisualSnapshot))
            .RegisterMessageType(typeof(ClaimVisualChunk))
            .RegisterMessageType(typeof(RenameClaimRequest))
            .RegisterMessageType(typeof(SetPermissionsRequest))
            .RegisterMessageType(typeof(TransferClaimRequest))
            .RegisterMessageType(typeof(AbandonClaimRequest))
            .RegisterMessageType(typeof(ClaimSnapshotRequest))
            .RegisterMessageType(typeof(AdminConfigSnapshot))
            .RegisterMessageType(typeof(UpdateAdminConfigRequest))
            .RegisterMessageType(typeof(ReleaseNpcStructureClaimsRequest))
            .RegisterMessageType(typeof(ForceTransferClaimRequest))
            .RegisterMessageType(typeof(DeleteClaimRequest))
            .RegisterMessageType(typeof(RemoveClaimChunkRequest))
            .RegisterMessageType(typeof(RemoveClaimChunksRequest))
            .RegisterMessageType(typeof(ClaimMemberDirectoryRequest))
            .RegisterMessageType(typeof(ClaimMemberDirectorySnapshot))
            .RegisterMessageType(typeof(ClaimMemberDirectoryEntry))
            .RegisterMessageType(typeof(SetClaimPvpRequest))
            .SetMessageHandler<ClaimVisualSnapshot>(ClaimMapLayer.ReplaceClaimVisuals);
        channel.SetMessageHandler<AdminConfigSnapshot>(snapshot => api.Event.EnqueueMainThreadTask(() =>
        {
            var dialog = new MathClaimsAdminDialog(api, snapshot, request => channel.SendPacket(request), () => channel.SendPacket(new ReleaseNpcStructureClaimsRequest()));
            dialog.OnClosed += () => api.Event.EnqueueMainThreadTask(dialog.Dispose, "mathclaims-dispose-admin");
            dialog.TryOpen();
            api.Gui.RequestFocus(dialog);
        }, "mathclaims-open-admin"));
        dialogs = new ClaimDialogController(api, channel);
        dialogs.Start();
        channel.SetMessageHandler<ClaimPreflightResult>(dialogs.OnPreflightResult);
        channel.SetMessageHandler<ClaimMemberDirectorySnapshot>(dialogs.OnMemberDirectorySnapshot);
        worldBorders = new ClaimWorldBorderRenderer(api);
        api.Event.RegisterRenderer(worldBorders, Vintagestory.API.Client.EnumRenderStage.Opaque, "mathclaims-world-borders");
        api.Input.RegisterHotKey("mathclaims-toggle-world-borders", "Alternar bordas de propriedades", Vintagestory.API.Client.GlKeys.P, Vintagestory.API.Client.HotkeyType.CharacterControls, ctrlPressed: true);
        api.Input.SetHotKeyHandler("mathclaims-toggle-world-borders", _ => { worldBorders.Toggle(); return true; });
        long snapshotListener = 0;
        snapshotListener = api.Event.RegisterGameTickListener(_ =>
        {
            if (!channel.Connected) return;
            channel.SendPacket(new ClaimSnapshotRequest());
            api.Event.UnregisterGameTickListener(snapshotListener);
        }, 250);
        clientExplorationListener = api.Event.RegisterGameTickListener(_ => ClaimMapLayer.RecordClientExploration(api), 1000);
        api.Logger.Notification("[MathClaims] Map UI revision 2 loaded (single dialog ownership).");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        config = api.LoadModConfig<ClaimConfig>("mathclaims.json") ?? new ClaimConfig();
        api.StoreModConfig(config, "mathclaims.json");
        var dataDir = api.GetOrCreateDataPath("MathClaims");
        store = new JsonClaimStore(dataDir);
        audit = new AuditLog(Path.Combine(dataDir, "logs"));
        var state = store.Load();
        var migratedSchema = ClaimStateMigration.Upgrade(state);
        engine = new ClaimEngine(state, config);
        if (migratedSchema)
        {
            store.Save(state);
            audit.Write("schema-migration", "from=1;to=2;geometry=16x16");
            api.Logger.Notification("[MathClaims] Migrated persisted claims from 32x32 to 16x16 cells.");
        }
        serverChannel = api.Network.RegisterChannel("mathclaims")
            .RegisterMessageType(typeof(CreateClaimRequest))
            .RegisterMessageType(typeof(ClaimChunkRequest))
            .RegisterMessageType(typeof(ClaimPreflightRequest))
            .RegisterMessageType(typeof(ClaimPreflightResult))
            .RegisterMessageType(typeof(ClaimVisualSnapshot))
            .RegisterMessageType(typeof(ClaimVisualChunk))
            .RegisterMessageType(typeof(RenameClaimRequest))
            .RegisterMessageType(typeof(SetPermissionsRequest))
            .RegisterMessageType(typeof(TransferClaimRequest))
            .RegisterMessageType(typeof(AbandonClaimRequest))
            .RegisterMessageType(typeof(ClaimSnapshotRequest))
            .RegisterMessageType(typeof(AdminConfigSnapshot))
            .RegisterMessageType(typeof(UpdateAdminConfigRequest))
            .RegisterMessageType(typeof(ReleaseNpcStructureClaimsRequest))
            .RegisterMessageType(typeof(ForceTransferClaimRequest))
            .RegisterMessageType(typeof(DeleteClaimRequest))
            .RegisterMessageType(typeof(RemoveClaimChunkRequest))
            .RegisterMessageType(typeof(RemoveClaimChunksRequest))
            .RegisterMessageType(typeof(ClaimMemberDirectoryRequest))
            .RegisterMessageType(typeof(ClaimMemberDirectorySnapshot))
            .RegisterMessageType(typeof(ClaimMemberDirectoryEntry))
            .RegisterMessageType(typeof(SetClaimPvpRequest))
            .SetMessageHandler<CreateClaimRequest>(OnCreateClaimRequested);
        serverChannel.SetMessageHandler<ClaimPreflightRequest>(OnClaimPreflightRequested);
        serverChannel.SetMessageHandler<RenameClaimRequest>(OnRenameClaimRequested);
        serverChannel.SetMessageHandler<SetPermissionsRequest>(OnSetPermissionsRequested);
        serverChannel.SetMessageHandler<ClaimMemberDirectoryRequest>(OnClaimMemberDirectoryRequested);
        serverChannel.SetMessageHandler<SetClaimPvpRequest>(OnSetClaimPvpRequested);
        serverChannel.SetMessageHandler<TransferClaimRequest>(OnTransferClaimRequested);
        serverChannel.SetMessageHandler<AbandonClaimRequest>(OnAbandonClaimRequested);
        serverChannel.SetMessageHandler<ClaimSnapshotRequest>((player, _) => SendVisualSnapshot(player));
        serverChannel.SetMessageHandler<UpdateAdminConfigRequest>(OnUpdateAdminConfigRequested);
        serverChannel.SetMessageHandler<ReleaseNpcStructureClaimsRequest>(OnReleaseNpcStructureClaimsRequested);
        serverChannel.SetMessageHandler<ForceTransferClaimRequest>(OnForceTransferRequested);
        serverChannel.SetMessageHandler<DeleteClaimRequest>(OnDeleteClaimRequested);
        serverChannel.SetMessageHandler<RemoveClaimChunkRequest>(OnRemoveClaimChunkRequested);
        serverChannel.SetMessageHandler<RemoveClaimChunksRequest>(OnRemoveClaimChunksRequested);
        api.Permissions.RegisterPrivilege("mathclaims.noexpire", "Protects a player's MathClaims from inactivity expiration.", false);
        api.Event.CanPlaceOrBreakBlock += CanPlaceOrBreak;
        api.Event.CanUseBlock += CanUse;
        api.Event.OnPlayerInteractEntity += OnPlayerInteractEntity;
        api.Event.PlayerNowPlaying += OnPlayerOnline;
        api.Event.PlayerRespawn += EnsurePvpGuard;
        api.Event.PlayerChat += (player, _, ref message, ref _, consumed) =>
        {
            if (config.Enabled && VanillaLandCommandGuard.IsVanillaLandCommand(message))
            {
                consumed.value = true;
                player.SendMessage(0, "O sistema vanilla de claims está desativado. Use o mapa do MathClaims.", EnumChatType.Notification);
            }
        };
        api.Event.PlayerDisconnect += OnPlayerOffline;
        api.Event.GameWorldSave += Persist;
        tickListener = api.Event.RegisterGameTickListener(OnUptimeTick, 60000);
        api.ChatCommands.Create("mathclaim")
            .WithDescription("Open the MathClaims administration interface.")
            .RequiresPrivilege(Privilege.controlserver)
            .RequiresPlayer()
            .HandleWith(args =>
            {
                if (args.Caller.Player is IServerPlayer player) OnMathClaimCommand(player, 0, new CmdArgs([]));
                return TextCommandResult.Success();
            });
        api.Logger.Notification("[MathClaims] Loaded {0} properties and {1} protected 16x16 cells.", engine.State.Properties.Count, engine.State.Properties.Values.Sum(p => p.Chunks.Count));
        audit.Write("startup", $"properties={engine.State.Properties.Count}");
    }

    private bool CanPlaceOrBreak(IServerPlayer player, BlockSelection selection, out string claimant)
    {
        return Check(player, selection.Position, ClaimPermission.BuildBreak, out claimant);
    }
    private void OnCreateClaimRequested(IServerPlayer player, CreateClaimRequest request)
    {
        if (engine is null) return;
        var columns = request.Columns.Count > 0
            ? request.Columns.Select(item => new ChunkColumn(item.Dimension, item.ChunkX, item.ChunkZ)).Distinct().ToArray()
            : [new ChunkColumn(request.Dimension, request.ChunkX, request.ChunkZ)];
        var result = engine.Create(player.PlayerUID, request.Name, columns, ValidateColumn);
        if (result.Success)
        {
            Persist();
            audit?.Write("create", $"owner={player.PlayerUID};property={result.PropertyId};chunks={string.Join(',', columns)};name={request.Name}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }
    private void OnClaimPreflightRequested(IServerPlayer player, ClaimPreflightRequest request)
    {
        if (engine is null || serverChannel is null) return;
        var columns = request.Columns.Select(item => new ChunkColumn(item.Dimension, item.ChunkX, item.ChunkZ)).Distinct().ToArray();
        var result = engine.ValidateCreate(player.PlayerUID, "Pré-validação", columns, ValidateColumn);
        var joins = result.Success && result.Message.Contains("adjacente", StringComparison.Ordinal);
        serverChannel.SendPacket(new ClaimPreflightResult { Allowed = result.Success, Message = result.Message, AddsToAdjacentProperty = joins }, player);
    }
    private string? ValidateColumn(ChunkColumn column)
    {
        if (!TryGetSpawnBounds(out var bounds) || column.Dimension != bounds.Dimension || config.SpawnNoClaimSize <= 0) return null;
        return column.X >= bounds.MinX && column.X <= bounds.MaxX && column.Z >= bounds.MinZ && column.Z <= bounds.MaxZ
            ? "Não é possível criar uma claim na região de spawn."
            : null;
    }
    private void OnRenameClaimRequested(IServerPlayer player, RenameClaimRequest request)
    {
        if (engine is null || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var property = engine.State.Properties.GetValueOrDefault(id);
        var canManage = property is not null && (property.OwnerUid == player.PlayerUID ||
            (property.Permissions.TryGetValue(player.PlayerUID, out var permissions) && (permissions & ClaimPermission.Management) != 0));
        var result = engine.Rename(player.PlayerUID, id, request.Name, canManage);
        if (result.Success)
        {
            Persist();
            audit?.Write("rename", $"actor={player.PlayerUID};property={id};name={request.Name}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }
    private void OnSetPermissionsRequested(IServerPlayer player, SetPermissionsRequest request)
    {
        if (engine is null || sapi is null || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var property = engine.State.Properties.GetValueOrDefault(id);
        var canManage = property is not null && (property.OwnerUid == player.PlayerUID ||
            (property.Permissions.TryGetValue(player.PlayerUID, out var actorPermissions) && (actorPermissions & ClaimPermission.Management) != 0));
        var requestedName = request.PlayerName.Trim();
        var target = sapi.PlayerData.PlayerDataByUid.Values.FirstOrDefault(data =>
            string.Equals(data.LastKnownPlayername, requestedName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            player.SendMessage(0, "Jogador não encontrado. Ele precisa já ter entrado neste servidor.", EnumChatType.Notification);
            return;
        }
        var result = engine.SetPermissions(player.PlayerUID, id, target.PlayerUID, (ClaimPermission)request.Permissions, canManage);
        if (result.Success)
        {
            Persist();
            audit?.Write("permissions", $"actor={player.PlayerUID};property={id};target={target.PlayerUID};flags={(ClaimPermission)request.Permissions}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }

    private void OnClaimMemberDirectoryRequested(IServerPlayer player, ClaimMemberDirectoryRequest request)
    {
        if (engine is null || sapi is null || serverChannel is null || !Guid.TryParse(request.PropertyId, out var id) || !engine.State.Properties.TryGetValue(id, out var property))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var canManage = property.OwnerUid == player.PlayerUID ||
            (property.Permissions.TryGetValue(player.PlayerUID, out var permissions) && (permissions & ClaimPermission.Management) != 0);
        if (!canManage)
        {
            player.SendMessage(0, "Você não tem permissão para gerenciar membros desta propriedade.", EnumChatType.Notification);
            return;
        }
        var members = sapi.PlayerData.PlayerDataByUid.Values
            .Where(data => !string.IsNullOrWhiteSpace(data.LastKnownPlayername))
            .OrderBy(data => data.LastKnownPlayername, StringComparer.OrdinalIgnoreCase)
            .Select(data => new ClaimMemberDirectoryEntry
            {
                PlayerUid = data.PlayerUID,
                PlayerName = data.LastKnownPlayername,
                Permissions = property.Permissions.TryGetValue(data.PlayerUID, out var flags) ? (int)flags : 0,
                IsOwner = string.Equals(data.PlayerUID, property.OwnerUid, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
        serverChannel.SendPacket(new ClaimMemberDirectorySnapshot { PropertyId = property.Id.ToString(), Members = members }, player);
    }

    private void OnSetClaimPvpRequested(IServerPlayer player, SetClaimPvpRequest request)
    {
        if (engine is null || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var result = engine.SetPvp(player.PlayerUID, id, request.Enabled);
        if (result.Success)
        {
            Persist();
            audit?.Write("pvp", $"actor={player.PlayerUID};property={id};enabled={request.Enabled}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }
    private void OnTransferClaimRequested(IServerPlayer player, TransferClaimRequest request)
    {
        if (engine is null || sapi is null || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var recipientName = request.RecipientName.Trim();
        var target = sapi.PlayerData.PlayerDataByUid.Values.FirstOrDefault(data =>
            string.Equals(data.LastKnownPlayername, recipientName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            player.SendMessage(0, "Jogador não encontrado. Ele precisa já ter entrado neste servidor.", EnumChatType.Notification);
            return;
        }
        var property = engine.State.Properties.GetValueOrDefault(id);
        var chunks = property?.Chunks.Count ?? 0;
        var result = engine.Transfer(player.PlayerUID, id, target.PlayerUID);
        if (result.Success)
        {
            Persist();
            audit?.Write("transfer", $"actor={player.PlayerUID};property={id};recipient={target.PlayerUID};chunks={chunks}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }
    private void OnAbandonClaimRequested(IServerPlayer player, AbandonClaimRequest request)
    {
        if (engine is null || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var property = engine.State.Properties.GetValueOrDefault(id);
        var chunks = property?.Chunks.Count ?? 0;
        var result = engine.Abandon(player.PlayerUID, id);
        if (result.Success)
        {
            Persist();
            audit?.Write("abandon", $"actor={player.PlayerUID};property={id};chunks={chunks}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }

    private void OnForceTransferRequested(IServerPlayer player, ForceTransferClaimRequest request)
    {
        if (engine is null || sapi is null || !player.HasPrivilege(Privilege.controlserver) || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Você não tem permissão para forçar essa transferência.", EnumChatType.Notification);
            return;
        }
        var recipient = sapi.PlayerData.PlayerDataByUid.Values.FirstOrDefault(data => string.Equals(data.LastKnownPlayername, request.RecipientName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (recipient is null)
        {
            player.SendMessage(0, "Jogador não encontrado. Ele precisa já ter entrado neste servidor.", EnumChatType.Notification);
            return;
        }
        var chunks = engine.State.Properties.GetValueOrDefault(id)?.Chunks.Count ?? 0;
        var result = engine.Transfer(player.PlayerUID, id, recipient.PlayerUID, force: true);
        if (result.Success)
        {
            Persist();
            audit?.Write("force-transfer", $"actor={player.PlayerUID};property={id};recipient={recipient.PlayerUID};chunks={chunks};ignoreLimits={config.AdminIgnoresLimits}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }

    private void OnRemoveClaimChunkRequested(IServerPlayer player, RemoveClaimChunkRequest request)
    {
        if (engine is null || !Guid.TryParse(request.PropertyId, out var id) || !engine.State.Properties.TryGetValue(id, out var property))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var canManage = property.OwnerUid == player.PlayerUID ||
            (property.Permissions.TryGetValue(player.PlayerUID, out var permissions) && (permissions & ClaimPermission.Management) != 0);
        var chunk = new ChunkColumn(request.Dimension, request.ChunkX, request.ChunkZ);
        if (!property.Chunks.Contains(chunk))
        {
            player.SendMessage(0, "Esse terreno não pertence à propriedade selecionada.", EnumChatType.Notification);
            return;
        }
        var result = engine.RemoveChunks(player.PlayerUID, id, [chunk], canManage);
        if (result.Success)
        {
            Persist();
            audit?.Write("remove-chunk", $"actor={player.PlayerUID};property={id};chunk={chunk}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }

    private void OnRemoveClaimChunksRequested(IServerPlayer player, RemoveClaimChunksRequest request)
    {
        if (engine is null || !Guid.TryParse(request.PropertyId, out var id) || !engine.State.Properties.TryGetValue(id, out var property))
        {
            player.SendMessage(0, "Propriedade inválida.", EnumChatType.Notification);
            return;
        }
        var canManage = property.OwnerUid == player.PlayerUID ||
            (property.Permissions.TryGetValue(player.PlayerUID, out var permissions) && (permissions & ClaimPermission.Management) != 0);
        var chunks = request.Columns.Select(item => new ChunkColumn(item.Dimension, item.ChunkX, item.ChunkZ)).Distinct().ToArray();
        if (chunks.Length == 0 || chunks.Any(chunk => !property.Chunks.Contains(chunk)))
        {
            player.SendMessage(0, "A seleção contém terrenos inválidos para esta propriedade.", EnumChatType.Notification);
            return;
        }
        var result = engine.RemoveChunks(player.PlayerUID, id, chunks, canManage);
        if (result.Success)
        {
            Persist();
            audit?.Write("remove-chunks", $"actor={player.PlayerUID};property={id};count={chunks.Length};chunks={string.Join(',', chunks)}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }

    private void OnDeleteClaimRequested(IServerPlayer player, DeleteClaimRequest request)
    {
        if (engine is null || !player.HasPrivilege(Privilege.controlserver) || !Guid.TryParse(request.PropertyId, out var id))
        {
            player.SendMessage(0, "Você não tem permissão para excluir essa propriedade.", EnumChatType.Notification);
            return;
        }
        var chunks = engine.State.Properties.GetValueOrDefault(id)?.Chunks.Count ?? 0;
        var result = engine.Delete(id);
        if (result.Success)
        {
            Persist();
            audit?.Write("admin-delete", $"actor={player.PlayerUID};property={id};chunks={chunks}");
            BroadcastVisualSnapshots();
        }
        player.SendMessage(0, result.Message, EnumChatType.Notification);
    }
    private bool CanUse(IServerPlayer player, BlockSelection selection)
    {
        var code = sapi?.World.BlockAccessor.GetBlock(selection.Position).Code?.Path;
        return Check(player, selection.Position, UsePermissionClassifier.RequiredForBlockCode(code), out _);
    }
    private void OnPlayerInteractEntity(Vintagestory.API.Common.Entities.Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
    {
        if (byPlayer is not IServerPlayer player || Check(player, entity.Pos.AsBlockPos, ClaimPermission.Animals, out _)) return;
        handling = EnumHandling.PreventDefault;
        player.SendMessage(0, "Você não tem permissão para interagir com animais nesta propriedade.", EnumChatType.Notification);
    }
    private bool Check(IServerPlayer player, BlockPos pos, ClaimPermission required, out string claimant)
    {
        claimant = "";
        if (!config.Enabled || engine is null || player.WorldData.CurrentGameMode == EnumGameMode.Creative) return true;
        if (config.SpawnProtectionEnabled && TryGetSpawnBounds(out var spawn) && pos.dimension == spawn.Dimension &&
            pos.X >= ClaimGeometry.ToWorld(spawn.MinX) && pos.X < ClaimGeometry.ToWorld(spawn.MaxX + 1) && pos.Z >= ClaimGeometry.ToWorld(spawn.MinZ) && pos.Z < ClaimGeometry.ToWorld(spawn.MaxZ + 1) &&
            (config.SpawnProtectionPermissions & required) != 0)
        {
            claimant = "custommessage-mathclaims-denied";
            return false;
        }
        var property = engine.At(ToColumn(pos));
        if (property is null) return true;
        if (ClaimEngine.HasPermission(player.PlayerUID, property, required)) return true;
        claimant = "custommessage-mathclaims-denied";
        return false;
    }
    private void OnPlayerOnline(IServerPlayer player)
    {
        engine?.GetActivity(player.PlayerUID).OfflineUptimeSeconds = 0;
        EnsurePvpGuard(player);
        Persist();
        SendVisualSnapshot(player);
    }

    private void EnsurePvpGuard(IServerPlayer player)
    {
        if (player.Entity is not EntityPlayer entity || entity.HasBehavior("mathclaims-own-claim-pvp")) return;
        entity.AddBehavior(new OwnClaimPvpGuard(entity, IsOwnClaimPvpDisabled));
    }

    private bool IsOwnClaimPvpDisabled(Entity entity)
    {
        if (!config.Enabled || engine is null || entity is not EntityPlayer player || string.IsNullOrWhiteSpace(player.PlayerUID)) return false;
        var property = engine.At(ToColumn(entity.Pos.AsBlockPos));
        return property is not null && property.OwnerUid == player.PlayerUID && !property.PvpEnabled;
    }

    private void SendVisualSnapshot(IServerPlayer player)
    {
        if (serverChannel is null || engine is null) return;
        var snapshot = new ClaimVisualSnapshot
        {
            ShowClaimsOnMinimap = config.ShowClaimsOnMinimap,
            ShowClaimsInUndiscoveredAreas = config.ShowClaimsInUndiscoveredAreas,
            Chunks = engine.State.Properties.Values
                .SelectMany(property => property.Chunks.Select(chunk => new ClaimVisualChunk
                {
                    Dimension = chunk.Dimension,
                    ChunkX = chunk.X,
                    ChunkZ = chunk.Z,
                    Color = (int)engine.GetOverlay(player.PlayerUID, property),
                    PropertyId = property.Id.ToString(),
                    PropertyName = property.Name,
                    OwnerName = property.OwnerUid == player.PlayerUID ? "Você" : sapi?.World.PlayerByUid(property.OwnerUid)?.PlayerName ?? "Outro jogador",
                    PropertyChunkCount = property.Chunks.Count,
                    CanManage = property.OwnerUid == player.PlayerUID ||
                        (property.Permissions.TryGetValue(player.PlayerUID, out var permissions) && (permissions & ClaimPermission.Management) != 0),
                    CanAdminister = player.HasPrivilege(Privilege.controlserver),
                    PvpEnabled = property.PvpEnabled
                }))
                .ToList()
        };
        if (config.SpawnProtectionEnabled && TryGetSpawnBounds(out var spawn))
        {
            snapshot.SpawnProtected = true;
            snapshot.SpawnDimension = spawn.Dimension;
            snapshot.SpawnMinChunkX = spawn.MinX;
            snapshot.SpawnMaxChunkX = spawn.MaxX;
            snapshot.SpawnMinChunkZ = spawn.MinZ;
            snapshot.SpawnMaxChunkZ = spawn.MaxZ;
        }
        serverChannel.SendPacket(snapshot, player);
    }

    private void BroadcastVisualSnapshots()
    {
        if (sapi is null) return;
        foreach (var player in sapi.World.AllOnlinePlayers.OfType<IServerPlayer>()) SendVisualSnapshot(player);
    }
    private void OnPlayerOffline(IServerPlayer player) => Persist();
    private void OnUptimeTick(float _) 
    {
        if (engine is null || sapi is null) return;
        engine.AdvanceUptime(TimeSpan.FromMinutes(1), sapi.World.AllOnlinePlayers.Select(p => p.PlayerUID));
        var expired = engine.Expire(HasNoExpirePrivilege);
        foreach (var property in expired) audit?.Write("inactivity-expiration", $"owner={property.OwnerUid};property={property.Id};chunks={property.Chunks.Count}");
        Persist();
    }
    private bool HasNoExpirePrivilege(string uid)
    {
        if (sapi is null) return false;
        var data = sapi.PlayerData.GetPlayerDataByUid(uid);
        return data is not null && (data.PermaPrivileges.Contains("mathclaims.noexpire") || sapi.Permissions.GetRole(data.RoleCode)?.Privileges.Contains("mathclaims.noexpire") == true);
    }
    private void OnMathClaimCommand(IServerPlayer player, int _, CmdArgs __)
    {
        var legacy = GetLegacyClaimCount();
        if (legacy > 0) player.SendMessage(0, $"ATENÇÃO: {legacy} claim(s) vanilla legada(s) foram encontradas. Nenhuma foi alterada.", EnumChatType.Notification);
        SendAdminConfig(player);
    }

    private void SendAdminConfig(IServerPlayer player)
    {
        if (serverChannel is null || engine is null) return;
        serverChannel.SendPacket(new AdminConfigSnapshot
        {
            Enabled = config.Enabled,
            ShowClaimsOnMinimap = config.ShowClaimsOnMinimap,
            ShowClaimsInUndiscoveredAreas = config.ShowClaimsInUndiscoveredAreas,
            AdminIgnoresLimits = config.AdminIgnoresLimits,
            MaxPropertiesPerPlayer = config.MaxPropertiesPerPlayer,
            MaxChunksPerPlayer = config.MaxChunksPerPlayer,
            MinimumDistanceEnabled = config.MinimumDistanceEnabled,
            MinimumDistanceChunks = config.MinimumDistanceChunks,
            SpawnNoClaimSize = config.SpawnNoClaimSize,
            SpawnProtectionEnabled = config.SpawnProtectionEnabled,
            SpawnProtectionPermissions = (int)config.SpawnProtectionPermissions,
            ExpirationEnabled = config.ExpirationEnabled,
            ExpirationUptimeSeconds = config.ExpirationUptimeSeconds,
            PropertyCount = engine.State.Properties.Count,
            LegacyClaimCount = GetLegacyClaimCount(),
            NpcStructureClaimCount = GetNpcStructureClaimCount()
        }, player);
    }

    private void OnUpdateAdminConfigRequested(IServerPlayer player, UpdateAdminConfigRequest request)
    {
        if (sapi is null || !player.HasPrivilege(Privilege.controlserver))
        {
            player.SendMessage(0, "Você não tem permissão para alterar a configuração do MathClaims.", EnumChatType.Notification);
            audit?.Write("admin-config-denied", $"actor={player.PlayerUID}");
            return;
        }

        config.Enabled = request.Enabled;
        config.ShowClaimsOnMinimap = request.ShowClaimsOnMinimap;
        config.ShowClaimsInUndiscoveredAreas = request.ShowClaimsInUndiscoveredAreas;
        config.AdminIgnoresLimits = request.AdminIgnoresLimits;
        config.MaxPropertiesPerPlayer = Math.Clamp(request.MaxPropertiesPerPlayer, 1, 100000);
        config.MaxChunksPerPlayer = Math.Clamp(request.MaxChunksPerPlayer, 1, 1000000);
        config.MinimumDistanceEnabled = request.MinimumDistanceEnabled;
        config.MinimumDistanceChunks = Math.Clamp(request.MinimumDistanceChunks, 0, 100000);
        config.SpawnNoClaimSize = Math.Clamp(request.SpawnNoClaimSize, 0, 999);
        config.SpawnProtectionEnabled = request.SpawnProtectionEnabled;
        config.SpawnProtectionPermissions = (ClaimPermission)request.SpawnProtectionPermissions & ClaimPermission.All;
        config.ExpirationEnabled = request.ExpirationEnabled;
        config.ExpirationUptimeSeconds = Math.Clamp(request.ExpirationUptimeSeconds, 0, TimeSpan.FromDays(36500).TotalSeconds);
        sapi.StoreModConfig(config, "mathclaims.json");
        audit?.Write("admin-config", $"actor={player.PlayerUID};enabled={config.Enabled};maxProperties={config.MaxPropertiesPerPlayer};maxChunks={config.MaxChunksPerPlayer};spawnProtection={config.SpawnProtectionEnabled};expiration={config.ExpirationEnabled}");
        BroadcastVisualSnapshots();
        player.SendMessage(0, "Configurações do MathClaims salvas.", EnumChatType.Notification);
    }
    private void OnReleaseNpcStructureClaimsRequested(IServerPlayer player, ReleaseNpcStructureClaimsRequest _)
    {
        if (sapi is null || !player.HasPrivilege(Privilege.controlserver))
        {
            player.SendMessage(0, "Você não tem permissão para liberar proteções de NPC.", EnumChatType.Notification);
            audit?.Write("npc-structure-release-denied", $"actor={player.PlayerUID}");
            return;
        }
        var candidates = sapi.WorldManager.LandClaims
            .Where(claim => NpcStructureClaimPolicy.IsNpcStructureOwner(claim.LastKnownOwnerName))
            .ToArray();
        foreach (var claim in candidates) sapi.World.Claims.Remove(claim);
        audit?.Write("npc-structure-release", $"actor={player.PlayerUID};count={candidates.Length};owners={string.Join(',', candidates.Select(claim => claim.LastKnownOwnerName))}");
        player.SendMessage(0, candidates.Length == 0
            ? "Nenhuma proteção vanilla de casa NPC/story reconhecida foi encontrada."
            : $"{candidates.Length} proteção(ões) de casa NPC/story liberada(s). Nenhum bloco foi apagado.", EnumChatType.Notification);
        SendAdminConfig(player);
    }
    private int GetLegacyClaimCount() => sapi?.WorldManager.LandClaims.Count ?? 0;
    private int GetNpcStructureClaimCount() => sapi?.WorldManager.LandClaims.Count(claim => NpcStructureClaimPolicy.IsNpcStructureOwner(claim.LastKnownOwnerName)) ?? 0;
    private void Persist() { if (engine is not null) store?.Save(engine.State); }
    private bool TryGetSpawnBounds(out SpawnBounds bounds)
    {
        bounds = default;
        if (sapi is null || config.SpawnNoClaimSize <= 0) return false;
        var spawn = sapi.World.DefaultSpawnPosition;
        var spawnX = ClaimGeometry.ToCell((int)Math.Floor(spawn.X));
        var spawnZ = ClaimGeometry.ToCell((int)Math.Floor(spawn.Z));
        var size = Math.Max(1, config.SpawnNoClaimSize);
        var minX = spawnX - (size - 1) / 2;
        var minZ = spawnZ - (size - 1) / 2;
        bounds = new SpawnBounds(0, minX, minX + size - 1, minZ, minZ + size - 1);
        return true;
    }
    private static ChunkColumn ToColumn(BlockPos pos) => new(pos.dimension, ClaimGeometry.ToCell(pos.X), ClaimGeometry.ToCell(pos.Z));
    public override void Dispose()
    {
        dialogs?.Dispose();
        if (worldBorders is not null && capi is not null)
        {
            capi.Event.UnregisterRenderer(worldBorders, Vintagestory.API.Client.EnumRenderStage.Opaque);
            worldBorders.Dispose();
        }
        Persist();
        if (sapi is not null && tickListener != 0) sapi.Event.UnregisterGameTickListener(tickListener);
        base.Dispose();
    }
}

internal readonly record struct SpawnBounds(int Dimension, int MinX, int MaxX, int MinZ, int MaxZ);
