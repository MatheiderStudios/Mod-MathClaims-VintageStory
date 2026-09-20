using MathClaims.Client.Map;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimInfoDialog : MapModalDialog
{
    public ClaimInfoDialog(ICoreClientAPI api, ClaimMapProperty property, Action? manage, Action? administer) : base(api)
    {
        var height = manage is null && administer is null ? 174 : manage is not null && administer is not null ? 266 : 220;
        var content = ElementBounds.Fixed(0, 0, 390, height).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-property-info", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Informações da propriedade", () => TryClose())
            .BeginChildElements(content)
            .AddStaticText(property.Name, CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 18, 390, 28))
            .AddStaticText($"Proprietário: {property.OwnerName}", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 58, 390, 22))
            .AddStaticText($"Terrenos: {property.ChunkCount}", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 84, 390, 22))
            .AddStaticText(property.AccessLabel, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 110, 390, 26));
        if (manage is not null)
        {
            SingleComposer.AddButton("Gerenciar propriedade", () => { manage(); return true; }, ElementBounds.Fixed(0, 154, 390, 30));
        }
        if (administer is not null)
        {
            SingleComposer.AddButton("Ações administrativas", () => { administer(); return true; }, ElementBounds.Fixed(0, manage is null ? 154 : 200, 390, 30));
        }
        SingleComposer.EndChildElements().Compose();
    }
}
