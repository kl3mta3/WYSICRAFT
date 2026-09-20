using Wysicraft.Models;

namespace Wysicraft.Core;

public enum ArrangeOperation { Left, HorizontalCenter, Right, Top, VerticalCenter, Bottom, HorizontalGaps, VerticalGaps, CanvasHorizontalCenter, CanvasVerticalCenter }
public sealed record Position(double X,double Y);
public sealed record ArrangementPlan(int Units,Dictionary<string,Position> Positions);

public static class Arrangement
{
    sealed class Unit(List<Element> anchors,HashSet<string> moving) {
        public List<Element> Anchors=anchors;
        public HashSet<string> Moving=moving;
        public double X,Y,Width,Height;
    }
    public static ArrangementPlan Plan(UiDefinition screen,IEnumerable<string> selection,ArrangeOperation operation,bool keepGroups=true) {
        var selected=selection.ToHashSet();
        var elements=screen.Elements.Where(e=>selected.Contains(e.Id)).ToList();
        var fullGroups=keepGroups?elements.SelectMany(e=>LayerGroups.Path(screen,e.LayerGroup)).Distinct()
            .Where(g=>screen.Elements.Where(e=>LayerGroups.Contains(screen,g,e.LayerGroup)).All(e=>selected.Contains(e.Id))).ToArray():[];
        var units=elements.Select(e=>new Unit([e],ContainerTree.Moving(screen,new[]{e.Id}).Select(v=>v.Id).ToHashSet())).ToList();
        // Merge groups and overlapping movement sets so descendants move exactly once.
        for(int i=0;i<units.Count;i++)for(int j=i+1;j<units.Count;) {
            var a=units[i];var b=units[j];
            bool grouped=fullGroups.Any(g=>a.Anchors.Any(e=>LayerGroups.Contains(screen,g,e.LayerGroup)) && b.Anchors.Any(e=>LayerGroups.Contains(screen,g,e.LayerGroup)));
            if(a.Moving.Overlaps(b.Moving) || grouped) {a.Anchors.AddRange(b.Anchors);a.Moving.UnionWith(b.Moving);units.RemoveAt(j);i=-1;break;}
            j++;
        }
        foreach(var unit in units) {
            var ids=unit.Anchors.Select(e=>e.Id).ToHashSet();
            var roots=unit.Anchors.Where(e=>!ContainerTree.Ancestors(screen,e).Any(p=>ids.Contains(p.Id))).ToArray();
            unit.X=roots.Min(e=>e.Bounds.X);unit.Y=roots.Min(e=>e.Bounds.Y);
            unit.Width=roots.Max(e=>e.Bounds.X+e.Bounds.Width)-unit.X;
            unit.Height=roots.Max(e=>e.Bounds.Y+e.Bounds.Height)-unit.Y;
        }
        bool canvas=operation is ArrangeOperation.CanvasHorizontalCenter or ArrangeOperation.CanvasVerticalCenter;
        bool gaps=operation is ArrangeOperation.HorizontalGaps or ArrangeOperation.VerticalGaps;
        int minimum=canvas?1:gaps?3:2;
        if(units.Count<minimum)throw new InvalidOperationException($"Select at least {minimum} independent controls or groups. A container and its children count as one object.");
        double left=units.Min(u=>u.X),top=units.Min(u=>u.Y),right=units.Max(u=>u.X+u.Width),bottom=units.Max(u=>u.Y+u.Height);
        var offsets=new Dictionary<Unit,Position>();
        if(gaps) {
            bool horizontal=operation==ArrangeOperation.HorizontalGaps;
            var ordered=units.OrderBy(u=>horizontal?u.X:u.Y).ToArray();
            double start=horizontal?ordered[0].X:ordered[0].Y;
            double end=horizontal?ordered[^1].X+ordered[^1].Width:ordered[^1].Y+ordered[^1].Height;
            double gap=(end-start-ordered.Sum(u=>horizontal?u.Width:u.Height))/(units.Count-1);
            if(gap<0)throw new InvalidOperationException("Not enough room for equal gaps. Move the two outside objects farther apart first.");
            double cursor=start;
            foreach(var unit in ordered) {offsets[unit]=horizontal?new(cursor-unit.X,0):new(0,cursor-unit.Y);cursor+=(horizontal?unit.Width:unit.Height)+gap;}
        } else foreach(var unit in units) {
            offsets[unit]=operation switch {
                ArrangeOperation.Left=>new(left-unit.X,0),
                ArrangeOperation.HorizontalCenter=>new((left+right-unit.Width)/2-unit.X,0),
                ArrangeOperation.Right=>new(right-unit.Width-unit.X,0),
                ArrangeOperation.Top=>new(0,top-unit.Y),
                ArrangeOperation.VerticalCenter=>new(0,(top+bottom-unit.Height)/2-unit.Y),
                ArrangeOperation.Bottom=>new(0,bottom-unit.Height-unit.Y),
                // Canvas centering preserves the arrangement of the complete selection.
                ArrangeOperation.CanvasHorizontalCenter=>new((screen.Size.Width-(right-left))/2-left,0),
                ArrangeOperation.CanvasVerticalCenter=>new(0,(screen.Size.Height-(bottom-top))/2-top),
                _=>throw new ArgumentOutOfRangeException(nameof(operation))
            };
        }
        var positions=new Dictionary<string,Position>();
        foreach(var (unit,delta) in offsets)foreach(var e in screen.Elements.Where(e=>unit.Moving.Contains(e.Id)))
            if(Math.Abs(delta.X)>0.000001 || Math.Abs(delta.Y)>0.000001)positions.Add(e.Id,new(e.Bounds.X+delta.X,e.Bounds.Y+delta.Y));
        return new(units.Count,positions);
    }
    public static void Apply(UiDefinition screen,ArrangementPlan plan) {
        foreach(var e in screen.Elements)if(plan.Positions.TryGetValue(e.Id,out var point)){e.Bounds.X=point.X;e.Bounds.Y=point.Y;}
    }
}

