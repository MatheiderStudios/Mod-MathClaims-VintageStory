using MathClaims.Models;
using MathClaims.Networking;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimMemberPermissionsDialog : MapModalDialog
{
    private ClaimPermission permissions;

    public ClaimMemberPermissionsDialog(ICoreClientAPI api, ClaimMemberDirectoryEntry member, Action<ClaimPermission> save, Action back) : base(api)
    {
        permissions = (ClaimPermission)member.Permissions;
        var content = ElementBounds.Fixed(0, 0, 420, 440).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-member-permissions", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Permissões do membro", () => back())
            .BeginChildElements(content)
            .AddStaticText(member.PlayerName, CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 18, 420, 28))
            .AddStaticText("Permissões", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 58, 420, 22))
            .AddSwitch(value => SetFlag(ClaimPermission.BuildBreak, value), ElementBounds.Fixed(0, 90, 28, 28), "build")
            .AddStaticText("Construir / quebrar", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 93, 170, 24))
            .AddSwitch(value => SetFlag(ClaimPermission.Doors, value), ElementBounds.Fixed(220, 90, 28, 28), "doors")
            .AddStaticText("Portas", CairoFont.WhiteSmallText(), ElementBounds.Fixed(258, 93, 162, 24))
            .AddSwitch(value => SetFlag(ClaimPermission.Containers, value), ElementBounds.Fixed(0, 123, 28, 28), "containers")
            .AddStaticText("Contêineres", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 126, 170, 24))
            .AddSwitch(value => SetFlag(ClaimPermission.Machines, value), ElementBounds.Fixed(220, 123, 28, 28), "machines")
            .AddStaticText("Máquinas", CairoFont.WhiteSmallText(), ElementBounds.Fixed(258, 126, 162, 24))
            .AddSwitch(value => SetFlag(ClaimPermission.Animals, value), ElementBounds.Fixed(0, 156, 28, 28), "animals")
            .AddStaticText("Animais", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 159, 170, 24))
            .AddSwitch(value => SetFlag(ClaimPermission.Liquids, value), ElementBounds.Fixed(220, 156, 28, 28), "liquids")
            .AddStaticText("Líquidos", CairoFont.WhiteSmallText(), ElementBounds.Fixed(258, 159, 162, 24))
            .AddSwitch(value => SetFlag(ClaimPermission.Management, value), ElementBounds.Fixed(0, 189, 28, 28), "management")
            .AddStaticText("Gerenciamento", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 192, 170, 24))
            .AddButton("Ativar todos", () => { SetAll(); return true; }, ElementBounds.Fixed(220, 189, 200, 28), key: "all-permissions")
            .AddButton("Salvar permissões", () => { save(permissions); return true; }, ElementBounds.Fixed(0, 252, 420, 32), key: "save-permissions")
            .AddButton("Voltar", () => { back(); return true; }, ElementBounds.Fixed(0, 296, 420, 30), key: "back")
            .AddStaticText("Para remover totalmente este jogador, desmarque todas as permissões e clique em Salvar permissões.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 350, 420, 52))
            .EndChildElements().Compose();
        SetSwitch("build", ClaimPermission.BuildBreak);
        SetSwitch("doors", ClaimPermission.Doors);
        SetSwitch("containers", ClaimPermission.Containers);
        SetSwitch("machines", ClaimPermission.Machines);
        SetSwitch("animals", ClaimPermission.Animals);
        SetSwitch("liquids", ClaimPermission.Liquids);
        SetSwitch("management", ClaimPermission.Management);
    }

    private void SetSwitch(string key, ClaimPermission flag) => SingleComposer?.GetSwitch(key).SetValue((permissions & flag) != 0);
    private void SetFlag(ClaimPermission flag, bool enabled)
    {
        if (enabled) permissions |= flag;
        else permissions &= ~flag;
    }
    private void SetAll()
    {
        permissions = ClaimPermission.All;
        foreach (var key in new[] { "build", "doors", "containers", "machines", "animals", "liquids", "management" }) SingleComposer?.GetSwitch(key).SetValue(true);
    }
}
