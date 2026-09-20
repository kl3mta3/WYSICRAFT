using Wysicraft.Models;
namespace Wysicraft.Core;
public static class ContainerTree
{
    public static IEnumerable<Element> Ancestors(UiDefinition screen,Element child) {
        var seen=new HashSet<string>{child.Id}; string id=child.Parent;
        while(id.Length>0) {
            if(!seen.Add(id) || seen.Count>33)throw new InvalidDataException("Container cycle or nesting exceeds 32 levels");
            var parent=screen.Elements.FirstOrDefault(e=>e.Id==id);
            if(parent==null || parent.Type is not ("panel" or "scroll_panel"))throw new InvalidDataException("Parent must identify a panel or scroll panel");
            yield return parent;id=parent.Parent;
        }
    }
    public static IEnumerable<Element> Moving(UiDefinition screen,IEnumerable<string> selected) {
        var ids=selected.ToHashSet();return screen.Elements.Where(e=>ids.Contains(e.Id) || Ancestors(screen,e).Any(p=>ids.Contains(p.Id)));
    }
    public static double Top(UiDefinition screen,Element e,IReadOnlyDictionary<string,double> scroll) => e.Bounds.Y-Ancestors(screen,e).Where(p=>p.Type=="scroll_panel").Sum(p=>scroll.GetValueOrDefault(p.Id));
}
