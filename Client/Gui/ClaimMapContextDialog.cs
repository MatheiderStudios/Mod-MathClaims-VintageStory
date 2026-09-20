using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimMapContextDialog : MapModalDialog
{
    public ClaimMapContextDialog(ICoreClientAPI api, int count, Action waypoint, Action claim, string claimLabel = "Criar terreno") : base(api)
    {
        var content = ElementBounds.Fixed(0, 0, 350, 150).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-map-context", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Terreno selecionado", () => TryClose())
            .BeginChildElements(content)
            .AddStaticText($"{count} terreno(s) selecionado(s)", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 20, 350, 24))
            .AddButton("Criar waypoint", () => { waypoint(); return true; }, ElementBounds.Fixed(0, 60, 350, 30))
            .AddButton(claimLabel, () => { claim(); return true; }, ElementBounds.Fixed(0, 104, 350, 30))
            .EndChildElements().Compose();
    }
}
