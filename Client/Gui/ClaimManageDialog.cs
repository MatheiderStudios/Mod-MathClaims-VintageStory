using MathClaims.Client.Map;
using MathClaims.Models;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimManageDialog : MapModalDialog
{
    private string name;

    public ClaimManageDialog(ICoreClientAPI api, ClaimMapProperty property, Action<string> rename, Action manageMembers, Action<bool> setPvp, Action transfer, Action abandon, Action removeSelectedChunk) : base(api)
    {
        name = property.Name;
        var owner = property.Color == OverlayColor.Green;
        var pvpInitialized = false;
        var height = owner ? 438 : 280;
        var content = ElementBounds.Fixed(0, 0, 420, height).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-property-manage", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Gerenciar propriedade", () => TryClose())
            .BeginChildElements(content)
            .AddStaticText("Nome da propriedade", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 18, 420, 22))
            .AddTextInput(ElementBounds.Fixed(0, 44, 420, 30), value =>
            {
                name = value;
                SingleComposer?.GetButton("save-name").Enabled = ClaimName.TryNormalize(name, out _);
            }, CairoFont.TextInput(), "name")
            .AddButton("Salvar nome", () =>
            {
                if (ClaimName.TryNormalize(name, out var clean)) { rename(clean); TryClose(); }
                return true;
            }, ElementBounds.Fixed(0, 84, 420, 28), key: "save-name")
            .AddStaticText("Membros", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 128, 420, 22))
            .AddButton("Gerenciar membros", () => { manageMembers(); return true; }, ElementBounds.Fixed(0, 154, 420, 30), key: "manage-members")
            .AddStaticText("Terreno selecionado", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 200, 420, 22))
            .AddButton("Remover terreno selecionado", () => { removeSelectedChunk(); return true; }, ElementBounds.Fixed(0, 226, 420, 30), key: "remove-chunk");

        if (owner)
        {
            SingleComposer
                .AddStaticText("PvP", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 272, 420, 22))
                .AddSwitch(enabled => { if (pvpInitialized) setPvp(enabled); }, ElementBounds.Fixed(0, 300, 28, 28), "pvp")
                .AddStaticText("Permitir dano entre jogadores nesta propriedade", CairoFont.WhiteSmallText(), ElementBounds.Fixed(38, 303, 382, 24))
                .AddButton("Transferir propriedade", () => { transfer(); return true; }, ElementBounds.Fixed(0, 342, 380, 30), key: "transfer")
                .AddButton("Abandonar propriedade", () => { abandon(); return true; }, ElementBounds.Fixed(0, 378, 380, 30), key: "abandon");
        }

        SingleComposer.EndChildElements().Compose();
        SingleComposer.GetTextInput("name").SetValue(name);
        SingleComposer.GetButton("save-name").Enabled = ClaimName.TryNormalize(name, out _);
        if (owner)
        {
            SingleComposer.GetSwitch("pvp").SetValue(property.PvpEnabled);
            pvpInitialized = true;
        }
    }
}
