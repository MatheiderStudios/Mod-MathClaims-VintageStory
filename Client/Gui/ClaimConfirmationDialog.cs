using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimConfirmationDialog : MapModalDialog
{
    public ClaimConfirmationDialog(ICoreClientAPI api, string title, string message, string confirmText, Action confirm) : base(api)
    {
        var content = ElementBounds.Fixed(0, 0, 420, 170).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-confirm", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar(title, () => TryClose())
            .BeginChildElements(content)
            .AddStaticText(message, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 22, 420, 64))
            .AddButton(confirmText, () => { confirm(); TryClose(); return true; }, ElementBounds.Fixed(0, 116, 205, 30), key: "confirm")
            .AddButton("Não", () => { TryClose(); return true; }, ElementBounds.Fixed(215, 116, 205, 30), key: "cancel")
            .EndChildElements().Compose();
    }
}
