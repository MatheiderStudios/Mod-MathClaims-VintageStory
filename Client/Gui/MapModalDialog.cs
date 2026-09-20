using Vintagestory.API.Client;

namespace MathClaims.Client.Gui;

public abstract class MapModalDialog(ICoreClientAPI api) : GuiDialogGeneric("", api)
{
    public override double DrawOrder => 0.2;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override bool CaptureAllInputs() => IsOpened();
    public override void OnMouseDown(MouseEvent e) { base.OnMouseDown(e); e.Handled = true; }
    public override void OnMouseUp(MouseEvent e) { base.OnMouseUp(e); e.Handled = true; }
    public override void OnMouseMove(MouseEvent e) { base.OnMouseMove(e); e.Handled = true; }
    public override void OnMouseWheel(MouseWheelEventArgs e) { base.OnMouseWheel(e); e.SetHandled(true); }
}
