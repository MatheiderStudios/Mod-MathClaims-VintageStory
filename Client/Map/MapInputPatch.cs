using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace MathClaims.Client.Map;

[HarmonyPatch(typeof(GuiDialogWorldMap))]
public static class MapInputPatch
{
    [HarmonyPrefix, HarmonyPatch(nameof(GuiDialogWorldMap.OnMouseDown))]
    public static bool Down(GuiDialogWorldMap __instance, MouseEvent args)
        => ClaimMapLayer.Instance?.MouseDown(__instance, args) != true;
    [HarmonyPrefix, HarmonyPatch(nameof(GuiDialogWorldMap.OnMouseUp))]
    public static bool Up(GuiDialogWorldMap __instance, MouseEvent args)
        => ClaimMapLayer.Instance?.MouseUp(__instance, args) != true;
}
