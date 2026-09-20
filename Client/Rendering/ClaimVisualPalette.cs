using MathClaims.Models;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace MathClaims.Client.Rendering;

public static class ClaimVisualPalette
{
    public static int ForClaim(OverlayColor color) => color switch
    {
        OverlayColor.Green => ColorUtil.ToRgba(255, 64, 220, 96),
        OverlayColor.Yellow => ColorUtil.ToRgba(255, 245, 205, 45),
        _ => ColorUtil.ToRgba(255, 235, 70, 60)
    };

    public static int ForWorldLine(OverlayColor color) => color switch
    {
        OverlayColor.Green => ColorUtil.ToRgba(255, 96, 220, 64),
        OverlayColor.Yellow => ColorUtil.ToRgba(255, 45, 205, 245),
        _ => ColorUtil.ToRgba(255, 60, 70, 235)
    };

    public static int Selection => ColorUtil.ToRgba(255, 165, 75, 245);
    public static int RemovalSelection => ColorUtil.ToRgba(255, 235, 70, 60);
    public static int Spawn => ColorUtil.ToRgba(255, 255, 255, 255);
}
