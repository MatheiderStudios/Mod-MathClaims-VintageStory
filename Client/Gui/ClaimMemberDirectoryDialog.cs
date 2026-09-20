using MathClaims.Networking;
using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public sealed class ClaimMemberDirectoryDialog : MapModalDialog
{
    private const int PageSize = 6;
    private readonly IReadOnlyList<ClaimMemberDirectoryEntry> members;
    private readonly Action<ClaimMemberDirectoryEntry> select;
    private readonly Action<string, int> pageChanged;
    private int page;
    private string search;
    private ClaimMemberDirectoryEntry[] visibleMembers = [];

    public ClaimMemberDirectoryDialog(ICoreClientAPI api, IReadOnlyList<ClaimMemberDirectoryEntry> members, string initialSearch, int requestedPage, Action<ClaimMemberDirectoryEntry> select, Action<string, int> pageChanged, Action back) : base(api)
    {
        this.members = members.OrderBy(member => member.PlayerName, StringComparer.OrdinalIgnoreCase).ToArray();
        this.select = select;
        this.pageChanged = pageChanged;
        search = initialSearch;
        page = requestedPage;
        var content = ElementBounds.Fixed(0, 0, 430, 438).WithFixedPadding(20);
        SingleComposer = api.Gui.CreateCompo("mathclaims-member-directory", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(content)
            .AddDialogTitleBar("Gerenciar membros", () => back())
            .BeginChildElements(content)
            .AddStaticText("Digite para filtrar jogadores", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 18, 430, 22))
            .AddTextInput(ElementBounds.Fixed(0, 48, 430, 30), FilterWhileTyping, CairoFont.TextInput(), "member-search");
        for (var index = 0; index < PageSize; index++)
        {
            var buttonIndex = index;
            SingleComposer.AddButton("", () => Choose(buttonIndex), ElementBounds.Fixed(0, 94 + index * 42, 430, 32), key: $"member-{index}");
            SingleComposer.AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(18, 101 + index * 42, 394, 20), $"member-name-{index}");
        }
        SingleComposer
            .AddButton("Anterior", () => { pageChanged(search, page - 1); return true; }, ElementBounds.Fixed(0, 374, 135, 30), key: "previous")
            .AddButton("Voltar", () => { back(); return true; }, ElementBounds.Fixed(145, 374, 140, 30), key: "back")
            .AddButton("Próxima", () => { pageChanged(search, page + 1); return true; }, ElementBounds.Fixed(295, 374, 135, 30), key: "next")
            .EndChildElements().Compose();
        SingleComposer.GetTextInput("member-search").SetValue(search);
        RefreshMembers(search, false);
    }

    private void FilterWhileTyping(string value) => RefreshMembers(value, true);

    private void RefreshMembers(string value, bool resetPage)
    {
        search = value;
        if (resetPage) page = 0;
        var filtered = members.Where(member => member.PlayerName.StartsWith(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)PageSize));
        page = Math.Clamp(page, 0, pages - 1);
        visibleMembers = filtered.Skip(page * PageSize).Take(PageSize).ToArray();
        for (var index = 0; index < PageSize; index++)
        {
            var button = SingleComposer?.GetButton($"member-{index}");
            if (button is null) continue;
            var visible = index < visibleMembers.Length;
            button.Visible = visible;
            var label = SingleComposer?.GetDynamicText($"member-name-{index}");
            if (!visible)
            {
                label?.SetNewText("", false, true, false);
                continue;
            }
            var member = visibleMembers[index];
            label?.SetNewText(member.PlayerName, false, true, false);
            button.Enabled = !member.IsOwner;
        }
        SingleComposer?.GetButton("previous").SetActive(page > 0);
        SingleComposer?.GetButton("next").SetActive(page + 1 < pages);
    }

    private bool Choose(int index)
    {
        if (index < visibleMembers.Length && !visibleMembers[index].IsOwner) select(visibleMembers[index]);
        return true;
    }
}
