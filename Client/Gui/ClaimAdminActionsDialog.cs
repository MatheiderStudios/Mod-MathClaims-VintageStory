using MathClaims.Client.Map;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimAdminActionsDialog : MapModalDialog
{
    public ClaimAdminActionsDialog(ICoreClientAPI api, ClaimMapProperty property, Action<string> forceTransfer, Action delete) : base(api)
    {
        var content = ElementBounds.Fixed(0, 0, 410, 270).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-admin-actions", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Ações administrativas", () => TryClose())
            .BeginChildElements(content)
            .AddStaticText(property.Name, CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 18, 410, 26))
            .AddStaticText("Forçar transferência para jogador conhecido", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 58, 410, 22))
            .AddTextInput(ElementBounds.Fixed(0, 86, 410, 32), _ => { }, CairoFont.TextInput(), "recipient")
            .AddButton("Forçar transferência", () =>
            {
                var recipient = SingleComposer?.GetTextInput("recipient").GetText()?.Trim() ?? "";
                if (recipient.Length > 0) forceTransfer(recipient);
                return true;
            }, ElementBounds.Fixed(0, 128, 410, 30), key: "force-transfer")
            .AddStaticText("Excluir remove somente a proteção e os metadados; blocos, itens e construções permanecem no mundo.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 174, 410, 48))
            .AddButton("Excluir claim", () => { delete(); return true; }, ElementBounds.Fixed(0, 226, 410, 30), key: "delete")
            .EndChildElements().Compose();
    }
}
