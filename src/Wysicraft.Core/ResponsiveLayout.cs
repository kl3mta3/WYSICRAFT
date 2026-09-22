using Wysicraft.Models;
namespace Wysicraft.Core;

public static class ResponsiveLayout
{
    public static Dictionary<string,Bounds> Resolve(UiDefinition design,double width,double height)=>Layout(design,width,height,null,null);
    // Editor reflow: the bounds of every descendant of `id` after that element is resized from its design bounds to `bounds`.
    public static Dictionary<string,Bounds> Reflow(UiDefinition design,string id,Bounds bounds) {
        var all=Layout(design,design.Size.Width,design.Size.Height,id,bounds);
        return all.Where(p=>IsDescendant(design,p.Key,id)).ToDictionary(p=>p.Key,p=>p.Value);
    }
    static bool IsDescendant(UiDefinition design,string candidate,string ancestor) {
        var parent=design.Elements.FirstOrDefault(e=>e.Id==candidate)?.Parent;
        for(int depth=0;!string.IsNullOrEmpty(parent) && depth<=design.Elements.Count;depth++) {
            if(parent==ancestor)return true;
            parent=design.Elements.FirstOrDefault(e=>e.Id==parent)?.Parent;
        }
        return false;
    }
    static Dictionary<string,Bounds> Layout(UiDefinition design,double width,double height,string? fixedId,Bounds? fixedBounds) {
        var resolved=new Dictionary<string,Bounds>();var visiting=new HashSet<string>();
        Bounds Place(Element e) {
            if(resolved.TryGetValue(e.Id,out var found))return found;
            if(e.Id==fixedId && fixedBounds!=null)return resolved[e.Id]=Json.Clone(fixedBounds);
            if(!visiting.Add(e.Id))throw new InvalidDataException("Container cycle");
            var parent=design.Elements.FirstOrDefault(p=>p.Id==e.Parent);
            var old=parent?.Bounds ?? new Bounds {Width=design.Size.Width,Height=design.Size.Height};
            var next=parent==null?new Bounds {Width=width,Height=height}:Place(parent);
            double dw=next.Width-old.Width,dh=next.Height-old.Height;
            var b=Json.Clone(e.Bounds);b.X+=next.X-old.X;b.Y+=next.Y-old.Y;
            switch(e.HorizontalAnchor) {case "right":b.X+=dw;break;case "center":b.X+=dw/2;break;case "stretch":b.Width=Math.Max(e.MinWidth,b.Width+dw);break;}
            switch(e.VerticalAnchor) {case "bottom":b.Y+=dh;break;case "center":b.Y+=dh/2;break;case "stretch":b.Height=Math.Max(e.MinHeight,b.Height+dh);break;}
            visiting.Remove(e.Id);return resolved[e.Id]=b;
        }
        foreach(var e in design.Elements)Place(e);return resolved;
    }
    public static void Apply(UiDefinition target,UiDefinition design,int width,int height) {
        width=design.Responsive?Math.Clamp(width,16,4096):design.Size.Width;
        height=design.Responsive?Math.Clamp(height,16,4096):design.Size.Height;
        var bounds=Resolve(design,width,height);
        foreach(var e in target.Elements)e.Bounds=Json.Clone(bounds[e.Id]);
        target.Size=new(){Width=width,Height=height};
    }
}
