using Wysicraft.Models;
namespace Wysicraft.Core;
public static class LayerGroups
{
    public static IEnumerable<string> Path(UiDefinition ui,string group) {
        var seen=new HashSet<string>();
        while(group.Length>0) {if(!seen.Add(group) || seen.Count>32)throw new InvalidDataException("Group cycle or nesting exceeds 32 levels");yield return group;group=ui.GroupParents.GetValueOrDefault(group,"");}
    }
    public static bool Contains(UiDefinition ui,string group,string candidate) => Path(ui,candidate).Contains(group);
    public static string Root(UiDefinition ui,string group) => Path(ui,group).LastOrDefault() ?? "";
    public static List<Element> Ordered(UiDefinition ui,List<Element> front) {
        var output=new List<Element>();var visited=new HashSet<string>();
        void Walk(string group) {
            if(!visited.Add(group))return;
            foreach(var e in front.Where(e=>Contains(ui,group,e.LayerGroup))) {
                if(e.LayerGroup==group)output.Add(e);
                else {var path=Path(ui,e.LayerGroup).Reverse().ToList();Walk(path[path.IndexOf(group)+1]);}
            }
        }
        foreach(var e in front) {if(e.LayerGroup.Length==0)output.Add(e);else Walk(Root(ui,e.LayerGroup));}
        return output;
    }
}
