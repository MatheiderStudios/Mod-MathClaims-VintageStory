using MathClaims.Client.Map;
using MathClaims.Models;
using MathClaims.Networking;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MathClaims.Client.Gui;

public sealed class ClaimDialogController(ICoreClientAPI api, IClientNetworkChannel channel) : IDisposable
{
    private GuiDialog? active;
    private bool switching;
    private PendingPreflight? pendingPreflight;
    private MemberDirectoryContext? memberDirectory;
    private readonly HarmonyLib.Harmony patches = new("mathclaims.map-input");

    public void Start()
    {
        patches.CreateClassProcessor(typeof(MapInputPatch)).Patch();
        ClaimMapLayer.OnContextRequested = Open;
        ClaimMapLayer.OnPropertyRequested = OpenProperty;
        ClaimMapLayer.OnReleaseRequested = ConfirmRelease;
    }

    public void OnPreflightResult(ClaimPreflightResult result)
    {
        api.Event.EnqueueMainThreadTask(() =>
        {
            var pending = pendingPreflight;
            if (pending is null) return;
            pendingPreflight = null;
            if (!result.Allowed)
            {
                api.ShowChatMessage($"MathClaims: {result.Message}");
                FinishPreflight();
                return;
            }
            if (result.AddsToAdjacentProperty)
            {
                channel.SendPacket(CreateRequest(pending, ""));
                FinishPreflight();
                return;
            }
            Show(new ClaimNameDialog(api, name => channel.SendPacket(CreateRequest(pending, name))));
        }, "mathclaims-claim-preflight-result");
    }

    public void OnMemberDirectorySnapshot(ClaimMemberDirectorySnapshot snapshot)
    {
        api.Event.EnqueueMainThreadTask(() =>
        {
            var context = memberDirectory;
            if (context is null || !string.Equals(context.Property.Id, snapshot.PropertyId, StringComparison.OrdinalIgnoreCase)) return;
            memberDirectory = context with { Members = snapshot.Members };
            ShowMemberDirectory(memberDirectory, "", 0);
        }, "mathclaims-member-directory");
    }

    private void OpenProperty(ChunkColumn clicked, ClaimMapProperty property)
    {
        if (active is { } existing) { api.Gui.RequestFocus(existing); return; }
        Action? manage = property.CanManage ? () => OpenManage(clicked, property) : null;
        Action? administer = property.CanAdminister
            ? () => Transition(() => new ClaimAdminActionsDialog(
                api,
                property,
                recipient => Transition(() => new ClaimConfirmationDialog(api, "Forçar transferência", $"Transferir todos os {property.ChunkCount} terrenos para {recipient}?", "Confirmar transferência", () => channel.SendPacket(new ForceTransferClaimRequest { PropertyId = property.Id, RecipientName = recipient }))),
                () => Transition(() => new ClaimConfirmationDialog(api, "Excluir claim", "A proteção inteira será removida. Nenhum bloco, item ou construção será apagado.", "Confirmar exclusão", () => channel.SendPacket(new DeleteClaimRequest { PropertyId = property.Id })))))
            : null;
        Show(new ClaimInfoDialog(api, property, manage, administer));
    }

    private void OpenManage(ChunkColumn clicked, ClaimMapProperty property) => Transition(() => CreateManageDialog(clicked, property));

    private ClaimManageDialog CreateManageDialog(ChunkColumn clicked, ClaimMapProperty property) => new(
        api,
        property,
        name => channel.SendPacket(new RenameClaimRequest { PropertyId = property.Id, Name = name }),
        () => RequestMemberDirectory(clicked, property),
        enabled => channel.SendPacket(new SetClaimPvpRequest { PropertyId = property.Id, Enabled = enabled }),
        () => Transition(() => new ClaimTransferDialog(api, property, recipient => Transition(() => new ClaimConfirmationDialog(api, "Transferir propriedade", $"Transferir todos os {property.ChunkCount} terrenos para {recipient}?", "Confirmar transferência", () => channel.SendPacket(new TransferClaimRequest { PropertyId = property.Id, RecipientName = recipient }))), () => Transition(() => CreateManageDialog(clicked, property)))),
        () => Transition(() => new ClaimConfirmationDialog(api, "Abandonar propriedade", "A proteção será removida. Nenhum bloco ou item do mundo será apagado.", "Confirmar abandono", () => channel.SendPacket(new AbandonClaimRequest { PropertyId = property.Id }))),
        () => Transition(() => new ClaimConfirmationDialog(api, "Remover terreno", "O terreno selecionado será liberado. A operação será recusada se o split ultrapassar o limite de propriedades.", "Confirmar remoção", () => channel.SendPacket(new RemoveClaimChunkRequest { PropertyId = property.Id, Dimension = clicked.Dimension, ChunkX = clicked.X, ChunkZ = clicked.Z }))));

    private void ConfirmRelease(ClaimMapProperty property, IReadOnlyCollection<ChunkColumn> columns)
    {
        if (active is { } existing) { api.Gui.RequestFocus(existing); return; }
        Show(new ClaimConfirmationDialog(
            api,
            "Desproteger terrenos",
            $"Deseja mesmo desproteger {columns.Count} terreno(s) de {property.Name}? Eles ficarão livres para qualquer jogador.",
            "Sim, desproteger",
            () => channel.SendPacket(new RemoveClaimChunksRequest
            {
                PropertyId = property.Id,
                Columns = columns.Select(column => new ClaimChunkRequest { Dimension = column.Dimension, ChunkX = column.X, ChunkZ = column.Z }).ToList()
            })));
    }

    private void RequestMemberDirectory(ChunkColumn clicked, ClaimMapProperty property)
    {
        memberDirectory = new MemberDirectoryContext(clicked, property, []);
        switching = true;
        active?.TryClose();
        switching = false;
        channel.SendPacket(new ClaimMemberDirectoryRequest { PropertyId = property.Id });
    }

    private void ShowMemberDirectory(MemberDirectoryContext context, string search, int page)
    {
        Show(new ClaimMemberDirectoryDialog(
            api,
            context.Members,
            search,
            page,
            member => OpenMemberPermissions(context, member, search, page),
            (nextSearch, nextPage) => Transition(() => CreateMemberDirectory(context, nextSearch, nextPage)),
            () => Transition(() => CreateManageDialog(context.Clicked, context.Property))));
    }

    private ClaimMemberDirectoryDialog CreateMemberDirectory(MemberDirectoryContext context, string search, int page) => new(
        api,
        context.Members,
        search,
        page,
        member => OpenMemberPermissions(context, member, search, page),
        (nextSearch, nextPage) => Transition(() => CreateMemberDirectory(context, nextSearch, nextPage)),
        () => Transition(() => CreateManageDialog(context.Clicked, context.Property)));

    private void OpenMemberPermissions(MemberDirectoryContext context, ClaimMemberDirectoryEntry member, string search, int page) => Transition(() => new ClaimMemberPermissionsDialog(
        api,
        member,
        permissions =>
        {
            channel.SendPacket(new SetPermissionsRequest { PropertyId = context.Property.Id, PlayerName = member.PlayerName, Permissions = (int)permissions });
            RequestMemberDirectory(context.Clicked, context.Property);
        },
        () => Transition(() => CreateMemberDirectory(context, search, page))));

    private void Open(ChunkColumn clicked, IReadOnlyCollection<ChunkColumn> selected, Vec3d world)
    {
        if (active is { } existing) { api.Gui.RequestFocus(existing); return; }
        Show(new ClaimMapContextDialog(api, selected.Count,
            () => Transition(() =>
            {
                ClaimMapLayer.ClearSelection();
                var layer = api.ModLoader.GetModSystem<WorldMapManager>().MapLayers.OfType<WaypointMapLayer>().First();
                return new GuiDialogAddWayPoint(api, layer) { WorldPos = world };
            }),
            () => BeginPreflight(clicked, selected), "Proteger"));
    }

    private void BeginPreflight(ChunkColumn clicked, IReadOnlyCollection<ChunkColumn> selected)
    {
        pendingPreflight = new PendingPreflight(clicked, selected.ToArray());
        switching = true;
        active?.TryClose();
        switching = false;
        channel.SendPacket(new ClaimPreflightRequest { Columns = selected.Select(c => new ClaimChunkRequest { Dimension = c.Dimension, ChunkX = c.X, ChunkZ = c.Z }).ToList() });
    }

    private static CreateClaimRequest CreateRequest(PendingPreflight pending, string name) => new()
    {
        Dimension = pending.Clicked.Dimension,
        ChunkX = pending.Clicked.X,
        ChunkZ = pending.Clicked.Z,
        Name = name,
        Columns = pending.Selected.Select(c => new ClaimChunkRequest { Dimension = c.Dimension, ChunkX = c.X, ChunkZ = c.Z }).ToList()
    };

    private void FinishPreflight()
    {
        ClaimMapLayer.ClearSelection();
        ClaimMapLayer.PopupBusy = false;
        var map = api.ModLoader.GetModSystem<WorldMapManager>().worldMapDlg;
        if (map?.IsOpened() == true) api.Gui.RequestFocus(map);
    }

    private void Transition(Func<GuiDialog> create)
    {
        if (switching) return;
        switching = true;
        api.Event.EnqueueMainThreadTask(() =>
        {
            active?.TryClose();
            Show(create());
            switching = false;
        }, "mathclaims-dialog-transition");
    }

    private void Show(GuiDialog dialog)
    {
        active = dialog;
        ClaimMapLayer.PopupBusy = true;
        dialog.OnClosed += () =>
        {
            if (ReferenceEquals(active, dialog)) active = null;
            if (!switching)
            {
                ClaimMapLayer.ClearSelection();
                ClaimMapLayer.PopupBusy = false;
                var map = api.ModLoader.GetModSystem<WorldMapManager>().worldMapDlg;
                if (map?.IsOpened() == true) api.Gui.RequestFocus(map);
            }
            api.Event.EnqueueMainThreadTask(dialog.Dispose, "mathclaims-dispose-dialog");
        };
        dialog.TryOpen();
        api.Gui.RequestFocus(dialog);
    }

    public void Dispose()
    {
        active?.TryClose();
        patches.UnpatchAll("mathclaims.map-input");
        ClaimMapLayer.OnContextRequested = null;
        ClaimMapLayer.OnPropertyRequested = null;
        ClaimMapLayer.OnReleaseRequested = null;
        ClaimMapLayer.PopupBusy = false;
    }

    private sealed record PendingPreflight(ChunkColumn Clicked, IReadOnlyCollection<ChunkColumn> Selected);
    private sealed record MemberDirectoryContext(ChunkColumn Clicked, ClaimMapProperty Property, IReadOnlyList<ClaimMemberDirectoryEntry> Members);
}
