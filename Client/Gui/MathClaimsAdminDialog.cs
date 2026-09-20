using MathClaims.Models;
using MathClaims.Networking;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class MathClaimsAdminDialog : GuiDialogGeneric
{
    private readonly AdminConfigSnapshot initial;
    private readonly Action<UpdateAdminConfigRequest> save;
    private readonly Action releaseNpcStructureClaims;
    private readonly ICoreClientAPI clientApi;

    public MathClaimsAdminDialog(ICoreClientAPI api, AdminConfigSnapshot snapshot, Action<UpdateAdminConfigRequest> save, Action releaseNpcStructureClaims)
        : base("MathClaims — administração", api)
    {
        initial = snapshot;
        this.save = save;
        this.releaseNpcStructureClaims = releaseNpcStructureClaims;
        clientApi = api;
        var content = ElementBounds.Fixed(0, 0, 560, 700).WithFixedPadding(18);
        SingleComposer = api.Gui.CreateCompo("mathclaims-admin", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("MathClaims — administração", () => TryClose())
            .BeginChildElements(content)
            .AddStaticText("Geral", CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 14, 250, 24))
            .AddSwitch(_ => { }, ElementBounds.Fixed(0, 44, 28, 26), "enabled")
            .AddStaticText("MathClaims habilitado", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 47, 220, 22))
            .AddHoverText("ⓘ", CairoFont.WhiteSmallText(), 220, ElementBounds.Fixed(245, 47, 24, 22), "info-enabled")
            .AddSwitch(_ => { }, ElementBounds.Fixed(285, 44, 28, 26), "minimap")
            .AddStaticText("Mostrar no minimapa", CairoFont.WhiteSmallText(), ElementBounds.Fixed(323, 47, 220, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(0, 76, 28, 26), "undiscovered")
            .AddStaticText("Mostrar em área não descoberta", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 79, 240, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(285, 76, 28, 26), "adminlimits")
            .AddStaticText("Admin ignora limites", CairoFont.WhiteSmallText(), ElementBounds.Fixed(323, 79, 220, 22))

            .AddStaticText("Limites", CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 116, 250, 24))
            .AddStaticText("Máx. propriedades", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 147, 160, 22))
            .AddNumberInput(ElementBounds.Fixed(170, 143, 90, 30), _ => { }, CairoFont.TextInput(), "max-properties")
            .AddStaticText("Máx. terrenos", CairoFont.WhiteSmallText(), ElementBounds.Fixed(285, 147, 145, 22))
            .AddNumberInput(ElementBounds.Fixed(440, 143, 90, 30), _ => { }, CairoFont.TextInput(), "max-chunks")
            .AddSwitch(_ => { }, ElementBounds.Fixed(0, 181, 28, 26), "distance-enabled")
            .AddStaticText("Distância mínima entre donos", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 184, 230, 22))
            .AddNumberInput(ElementBounds.Fixed(440, 177, 90, 30), _ => { }, CairoFont.TextInput(), "distance")
            .AddHoverText("Quando ligada, impede novos terrenos próximos a propriedades de outro dono.", CairoFont.WhiteSmallText(), 300, ElementBounds.Fixed(270, 184, 150, 22), "info-distance")

            .AddStaticText("Spawn", CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 222, 250, 24))
            .AddStaticText("Tamanho (células 16×16)", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 253, 160, 22))
            .AddNumberInput(ElementBounds.Fixed(170, 249, 90, 30), _ => { }, CairoFont.TextInput(), "spawn-size")
            .AddSwitch(_ => { }, ElementBounds.Fixed(285, 249, 28, 26), "spawn-protected")
            .AddStaticText("Proteção ativa (borda branca)", CairoFont.WhiteSmallText(), ElementBounds.Fixed(323, 252, 230, 22))
            .AddStaticText("Restrições do spawn", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 290, 180, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(0, 319, 28, 26), "spawn-build")
            .AddStaticText("Construir/quebrar", CairoFont.WhiteSmallText(), ElementBounds.Fixed(36, 322, 128, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(170, 319, 28, 26), "spawn-doors")
            .AddStaticText("Portas", CairoFont.WhiteSmallText(), ElementBounds.Fixed(206, 322, 72, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(285, 319, 28, 26), "spawn-containers")
            .AddStaticText("Contêineres", CairoFont.WhiteSmallText(), ElementBounds.Fixed(321, 322, 100, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(430, 319, 28, 26), "spawn-machines")
            .AddStaticText("Máquinas", CairoFont.WhiteSmallText(), ElementBounds.Fixed(466, 322, 86, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(0, 350, 28, 26), "spawn-animals")
            .AddStaticText("Animais", CairoFont.WhiteSmallText(), ElementBounds.Fixed(36, 353, 90, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(170, 350, 28, 26), "spawn-liquids")
            .AddStaticText("Líquidos", CairoFont.WhiteSmallText(), ElementBounds.Fixed(206, 353, 90, 22))
            .AddSwitch(_ => { }, ElementBounds.Fixed(285, 350, 28, 26), "spawn-management")
            .AddStaticText("Gerenciamento", CairoFont.WhiteSmallText(), ElementBounds.Fixed(321, 353, 130, 22))

            .AddStaticText("Expiração por inatividade (somente uptime)", CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 393, 440, 24))
            .AddSwitch(_ => { }, ElementBounds.Fixed(0, 424, 28, 26), "expiration")
            .AddStaticText("Ativada", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 427, 88, 22))
            .AddStaticText("Dias", CairoFont.WhiteSmallText(), ElementBounds.Fixed(145, 427, 40, 22))
            .AddNumberInput(ElementBounds.Fixed(188, 423, 65, 30), _ => { }, CairoFont.TextInput(), "expire-days")
            .AddStaticText("Horas", CairoFont.WhiteSmallText(), ElementBounds.Fixed(264, 427, 48, 22))
            .AddNumberInput(ElementBounds.Fixed(314, 423, 65, 30), _ => { }, CairoFont.TextInput(), "expire-hours")
            .AddStaticText("Min", CairoFont.WhiteSmallText(), ElementBounds.Fixed(389, 427, 38, 22))
            .AddNumberInput(ElementBounds.Fixed(428, 423, 48, 30), _ => { }, CairoFont.TextInput(), "expire-minutes")
            .AddStaticText("Seg", CairoFont.WhiteSmallText(), ElementBounds.Fixed(484, 427, 36, 22))
            .AddNumberInput(ElementBounds.Fixed(520, 423, 35, 30), _ => { }, CairoFont.TextInput(), "expire-seconds")
            .AddHoverText("O contador aumenta apenas enquanto o servidor está ligado e o jogador permanece offline.", CairoFont.WhiteSmallText(), 350, ElementBounds.Fixed(0, 457, 550, 22), "info-expiration")

            .AddStaticText($"Legado: claims vanilla encontradas: {snapshot.LegacyClaimCount}.  Propriedades MathClaims: {snapshot.PropertyCount}.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 501, 550, 38))
            .AddStaticText($"Casas NPC/story reconhecidas: {snapshot.NpcStructureClaimCount}. Esta ação libera apenas os códigos vanilla conhecidos (Nadiya, Tobias e treasure hunter), nunca claims de jogadores.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 535, 550, 50))
            .AddButton("Liberar casas NPC/story", ReleaseNpcStructureClaims, ElementBounds.Fixed(0, 584, 270, 28), key: "release-npc-claims")
            .AddButton("Salvar configurações", Save, ElementBounds.Fixed(0, 616, 560, 32), key: "save")
            .EndChildElements().Compose();
        SetInitialValues();
    }

    public override double DrawOrder => 0.2;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    private void SetInitialValues()
    {
        var c = SingleComposer!;
        SetSwitch("enabled", initial.Enabled); SetSwitch("minimap", initial.ShowClaimsOnMinimap);
        SetSwitch("undiscovered", initial.ShowClaimsInUndiscoveredAreas); SetSwitch("adminlimits", initial.AdminIgnoresLimits);
        SetSwitch("distance-enabled", initial.MinimumDistanceEnabled); SetSwitch("spawn-protected", initial.SpawnProtectionEnabled);
        SetSwitch("expiration", initial.ExpirationEnabled);
        SetNumber("max-properties", initial.MaxPropertiesPerPlayer); SetNumber("max-chunks", initial.MaxChunksPerPlayer);
        SetNumber("distance", initial.MinimumDistanceChunks); SetNumber("spawn-size", initial.SpawnNoClaimSize);
        var p = (ClaimPermission)initial.SpawnProtectionPermissions;
        SetSwitch("spawn-build", p.HasFlag(ClaimPermission.BuildBreak)); SetSwitch("spawn-doors", p.HasFlag(ClaimPermission.Doors));
        SetSwitch("spawn-containers", p.HasFlag(ClaimPermission.Containers)); SetSwitch("spawn-machines", p.HasFlag(ClaimPermission.Machines));
        SetSwitch("spawn-animals", p.HasFlag(ClaimPermission.Animals)); SetSwitch("spawn-liquids", p.HasFlag(ClaimPermission.Liquids));
        SetSwitch("spawn-management", p.HasFlag(ClaimPermission.Management));
        var total = Math.Max(0, (long)initial.ExpirationUptimeSeconds);
        SetNumber("expire-days", total / 86400); total %= 86400;
        SetNumber("expire-hours", total / 3600); total %= 3600;
        SetNumber("expire-minutes", total / 60); SetNumber("expire-seconds", total % 60);
    }

    private bool Save()
    {
        var days = Number("expire-days", 0, 36500); var hours = Number("expire-hours", 0, 23);
        var minutes = Number("expire-minutes", 0, 59); var seconds = Number("expire-seconds", 0, 59);
        var permissions = Flag("spawn-build", ClaimPermission.BuildBreak) | Flag("spawn-doors", ClaimPermission.Doors) |
            Flag("spawn-containers", ClaimPermission.Containers) | Flag("spawn-machines", ClaimPermission.Machines) |
            Flag("spawn-animals", ClaimPermission.Animals) | Flag("spawn-liquids", ClaimPermission.Liquids) |
            Flag("spawn-management", ClaimPermission.Management);
        save(new UpdateAdminConfigRequest
        {
            Enabled = Switch("enabled"), ShowClaimsOnMinimap = Switch("minimap"), ShowClaimsInUndiscoveredAreas = Switch("undiscovered"),
            AdminIgnoresLimits = Switch("adminlimits"), MaxPropertiesPerPlayer = Number("max-properties", 1, 100000),
            MaxChunksPerPlayer = Number("max-chunks", 1, 1000000), MinimumDistanceEnabled = Switch("distance-enabled"),
            MinimumDistanceChunks = Number("distance", 0, 100000), SpawnNoClaimSize = Number("spawn-size", 0, 999),
            SpawnProtectionEnabled = Switch("spawn-protected"), SpawnProtectionPermissions = (int)permissions,
            ExpirationEnabled = Switch("expiration"), ExpirationUptimeSeconds = days * 86400d + hours * 3600d + minutes * 60d + seconds
        });
        TryClose();
        return true;
    }

    private bool ReleaseNpcStructureClaims()
    {
        var confirm = new ClaimConfirmationDialog(clientApi, "Liberar casas NPC/story",
            "As proteções vanilla reconhecidas de Nadiya, Tobias e treasure hunter serão removidas. Blocos e estruturas não serão apagados.",
            "Confirmar liberação", releaseNpcStructureClaims);
        confirm.OnClosed += () => clientApi.Event.EnqueueMainThreadTask(confirm.Dispose, "mathclaims-dispose-npc-release-confirm");
        confirm.TryOpen();
        clientApi.Gui.RequestFocus(confirm);
        return true;
    }

    private void SetSwitch(string key, bool value) => SingleComposer!.GetSwitch(key).SetValue(value);
    private bool Switch(string key) => SingleComposer!.GetSwitch(key).On;
    private void SetNumber(string key, long value) => SingleComposer!.GetNumberInput(key).SetValue(value.ToString());
    private int Number(string key, int min, int max) => Math.Clamp((int)Math.Round(SingleComposer!.GetNumberInput(key).GetValue()), min, max);
    private ClaimPermission Flag(string key, ClaimPermission permission) => Switch(key) ? permission : ClaimPermission.None;
}
