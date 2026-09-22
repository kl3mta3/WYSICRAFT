using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace Wysicraft.Designer;

// View zoom scales the whole canvas with a LayoutTransform. Editing math stays in canvas coordinates
// (Zoom = 2 canvas pixels per GUI pixel), because GetPosition(Surface) and Thumb deltas are reported untransformed.
public partial class MainWindow
{
    double viewScale=1;
    const double MinViewScale=.25,MaxViewScale=4;
    readonly ScaleTransform viewTransform=new(1,1);
    void InitializeZoom() {
        Surface.LayoutTransform=viewTransform;
        CanvasScroll.PreviewMouseWheel+=(_,e)=>{
            if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))return;
            SetViewScale(viewScale*(e.Delta>0?1.15:1/1.15),e.GetPosition(CanvasScroll));e.Handled=true;
        };
    }
    void ZoomIn()=>SetViewScale(viewScale*1.25);
    void ZoomOut()=>SetViewScale(viewScale/1.25);
    void ZoomActual()=>SetViewScale(1);
    void ZoomFit() {
        CanvasScroll.UpdateLayout();
        double width=CanvasScroll.ViewportWidth-Surface.Margin.Left-Surface.Margin.Right,height=CanvasScroll.ViewportHeight-Surface.Margin.Top-Surface.Margin.Bottom;
        if(width<=0 || height<=0 || Surface.Width<=0 || Surface.Height<=0)return;
        SetViewScale(Math.Min(width/Surface.Width,height/Surface.Height));
    }
    // Keeps the canvas point under `anchor` (a viewport position) in place; defaults to the viewport center.
    void SetViewScale(double scale,Point? anchor=null) {
        scale=Math.Clamp(Math.Round(scale,3),MinViewScale,MaxViewScale);if(scale==viewScale)return;
        var point=anchor??new Point(CanvasScroll.ViewportWidth/2,CanvasScroll.ViewportHeight/2);
        double contentX=(CanvasScroll.HorizontalOffset+point.X)/viewScale,contentY=(CanvasScroll.VerticalOffset+point.Y)/viewScale;
        viewScale=scale;viewTransform.ScaleX=viewTransform.ScaleY=scale;
        CanvasScroll.UpdateLayout();
        CanvasScroll.ScrollToHorizontalOffset(contentX*scale-point.X);CanvasScroll.ScrollToVerticalOffset(contentY*scale-point.Y);
        Status.Text=StatusLine();UpdateZoomLabel();
    }
    string StatusLine()=>$"{ui.Id}  |  {ui.Size.Width} × {ui.Size.Height}  |  {selected.Count} selected  |  Grid {project.Manifest.GridSize}  |  Snap {(project.Manifest.Snap ? "on" : "off")}  |  Zoom {viewScale:P0}";
}
