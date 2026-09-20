using MathClaims.Models;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimNameDialog : MapModalDialog
{
    private string name = "";
    public ClaimNameDialog(ICoreClientAPI api, Action<string> submit) : base(api)
    {
        var content = ElementBounds.Fixed(0, 0, 390, 192).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-create", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Proteger nova área", () => TryClose())
            .BeginChildElements(content)
            .AddStaticText("Nome da propriedade", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 20, 390, 24))
            .AddTextInput(ElementBounds.Fixed(0, 53, 390, 32), text =>
            {
                name = text;
                if (SingleComposer is not null) SingleComposer.GetButton("create").Enabled = ClaimName.TryNormalize(name, out _);
            }, CairoFont.TextInput(), "name")
            .AddStaticText("De 1 a 48 caracteres. Use letras, números e espaços.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 98, 390, 42))
            .AddButton("Proteger", () =>
            {
                if (ClaimName.TryNormalize(name, out var clean)) { submit(clean); TryClose(); }
                return true;
            }, ElementBounds.Fixed(0, 146, 390, 30), key: "create")
            .EndChildElements().Compose();
        SingleComposer.GetButton("create").Enabled = false;
    }
}
