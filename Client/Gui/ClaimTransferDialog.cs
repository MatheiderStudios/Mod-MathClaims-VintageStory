using MathClaims.Client.Map;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimTransferDialog : MapModalDialog
{
    public ClaimTransferDialog(ICoreClientAPI api, ClaimMapProperty property, Action<string> transfer, Action back) : base(api)
    {
        var content = ElementBounds.Fixed(0, 0, 420, 240).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-transfer", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Transferir propriedade", () => back())
            .BeginChildElements(content)
            .AddStaticText($"{property.Name} • {property.ChunkCount} terreno(s)", CairoFont.WhiteMediumText(), ElementBounds.Fixed(0, 18, 420, 28))
            .AddStaticText("Jogador que já entrou no servidor", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 58, 420, 22))
            .AddTextInput(ElementBounds.Fixed(0, 86, 420, 30), _ => { }, CairoFont.TextInput(), "recipient")
            .AddStaticText("A propriedade inteira será transferida após a confirmação.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 130, 420, 30))
            .AddButton("Continuar", () =>
            {
                var recipient = SingleComposer?.GetTextInput("recipient").GetText()?.Trim() ?? "";
                if (recipient.Length > 0) transfer(recipient);
                return true;
            }, ElementBounds.Fixed(0, 184, 205, 30), key: "continue")
            .AddButton("Voltar", () => { back(); return true; }, ElementBounds.Fixed(215, 184, 205, 30), key: "back")
            .EndChildElements().Compose();
    }
}
