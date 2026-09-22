using System.Text.Json.Nodes;
using Wysicraft.Models;
namespace Wysicraft.Core;

public static class Components
{
    static string Unique(UiDefinition screen,string basis,IEnumerable<string>? reserved=null) {
        basis=basis[..Math.Min(48,basis.Length)];string id=basis;int n=1;
        while(screen.Elements.Any(e=>e.Id==id) || (reserved?.Contains(id)??false))id=basis+"_"+n++;return id;
    }
    public static UiDefinition Capture(Project project,UiDefinition screen,IEnumerable<string> selection,string id) {
        if(!Validation.Id(id)||project.Screens.Any(s=>s.Id==id))throw new InvalidOperationException("Use a unique screen/component ID.");
        var elements=Json.Clone(ContainerTree.Moving(screen,selection).ToList());if(elements.Count==0)throw new InvalidOperationException("Select controls first.");
        double x=elements.Min(e=>e.Bounds.X),y=elements.Min(e=>e.Bounds.Y);
        var result=new UiDefinition {Id=id,Title=id,IsComponent=true,Size=new(){Width=Math.Max(16,(int)Math.Ceiling(elements.Max(e=>e.Bounds.X+e.Bounds.Width)-x)),Height=Math.Max(16,(int)Math.Ceiling(elements.Max(e=>e.Bounds.Y+e.Bounds.Height)-y))},Elements=elements,Variables=Json.Clone(screen.Variables)};
        var ids=elements.Select(e=>e.Id).ToHashSet();
        foreach(var e in elements){e.Bounds.X-=x;e.Bounds.Y-=y;e.LayerGroup="";if(!ids.Contains(e.Parent))e.Parent="";}
        project.Screens.Add(result);return result;
    }
    public static ComponentInstance Place(Project project,UiDefinition screen,string sourceId,double x,double y) {
        if(screen.IsComponent)throw new InvalidOperationException("Linked components cannot be nested. Detach a placed instance before saving it as a new component.");
        var source=Source(project,sourceId);var root=new Element {Id=Unique(screen,"instance_"+sourceId),Type="panel",FillEnabled=false,BorderWidth=0,Bounds=new(){X=x,Y=y,Width=source.Size.Width,Height=source.Size.Height}};
        root.LayerGroup=root.Id;screen.Elements.Add(root);
        var instance=new ComponentInstance {Root=root.Id,Source=sourceId,SourceSize=Json.Clone(source.Size)};screen.ComponentInstances.Add(instance);Update(project,screen,instance,false);return instance;
    }
    static UiDefinition Source(Project project,string id)=>project.Screens.SingleOrDefault(s=>s.Id==id && s.IsComponent)??throw new InvalidOperationException("Component source not found: "+id);
    public static void Update(Project project,UiDefinition screen,ComponentInstance instance,bool keepOverrides=true) {
        var source=Source(project,instance.Source);var root=screen.Elements.SingleOrDefault(e=>e.Id==instance.Root)??throw new InvalidOperationException("Instance root was deleted. Detach its link or undo the deletion.");
        if(root.Type!="panel")throw new InvalidOperationException("Component root must remain a Panel.");
        if(root.Bounds.Width==instance.SourceSize.Width)root.Bounds.Width=source.Size.Width;
        if(root.Bounds.Height==instance.SourceSize.Height)root.Bounds.Height=source.Size.Height;
        foreach(var e in source.Elements)if(!instance.Ids.ContainsKey(e.Id)) {
            var id=Unique(screen,instance.Root[..Math.Min(24,instance.Root.Length)]+"_"+e.Id,instance.Ids.Values);instance.Ids[e.Id]=id;
        }
        foreach(var obsolete in instance.Ids.Keys.Except(source.Elements.Select(e=>e.Id)).ToArray()){screen.Elements.RemoveAll(e=>e.Id==instance.Ids[obsolete]);instance.Ids.Remove(obsolete);instance.Baseline.Remove(obsolete);}
        var layout=ResponsiveLayout.Resolve(source,root.Bounds.Width,root.Bounds.Height);
        foreach(var sourceElement in source.Elements) {
            var next=Json.Clone(sourceElement);next.Id=instance.Ids[sourceElement.Id];next.Parent=sourceElement.Parent.Length==0?root.Id:instance.Ids[sourceElement.Parent];next.LayerGroup=root.LayerGroup;next.Bounds=layout[sourceElement.Id];
            foreach(var handler in next.Events.Values.SelectMany(v=>new[]{v.Client,v.Server}))foreach(var action in handler.Actions)
                if(action.Type is "set_text" or "set_value" or "set_visible" or "set_enabled" or "change_texture" or "player_inventory" && instance.Ids.TryGetValue(action.Target,out var mapped))action.Target=mapped;
            var baseline=Json.Clone(next);var current=screen.Elements.FirstOrDefault(e=>e.Id==next.Id);
            if(current!=null && keepOverrides && instance.Baseline.TryGetValue(sourceElement.Id,out var old)) {
                var relative=Json.Clone(current);relative.Bounds.X-=root.Bounds.X;relative.Bounds.Y-=root.Bounds.Y;
                next=Json.Read<Element>(Merge(JsonNode.Parse(Json.Write(old)),JsonNode.Parse(Json.Write(relative)),JsonNode.Parse(Json.Write(next)))!.ToJsonString());
                next.Id=baseline.Id;next.Parent=baseline.Parent;next.LayerGroup=root.LayerGroup;
            }
            next.Bounds.X+=root.Bounds.X;next.Bounds.Y+=root.Bounds.Y;
            if(current==null)screen.Elements.Add(next);else screen.Elements[screen.Elements.IndexOf(current)]=next;
            instance.Baseline[sourceElement.Id]=baseline;
        }
        // Keep the component's internal stacking order, with its transparent wrapper first.
        var members=source.Elements.Select(e=>screen.Elements.Single(c=>c.Id==instance.Ids[e.Id])).ToList();screen.Elements.RemoveAll(e=>instance.Ids.Values.Contains(e.Id));screen.Elements.InsertRange(screen.Elements.IndexOf(root)+1,members);
        foreach(var variable in source.Variables)screen.Variables.TryAdd(variable.Key,variable.Value);
        instance.SourceSize=Json.Clone(source.Size);
    }
    static JsonNode? Merge(JsonNode? old,JsonNode? current,JsonNode? next) {
        if(JsonNode.DeepEquals(old,current))return next?.DeepClone();
        if(old is JsonObject a && current is JsonObject b && next is JsonObject c) {
            var result=(JsonObject)c.DeepClone();
            foreach(var key in a.Select(p=>p.Key).Union(b.Select(p=>p.Key))) {
                if(!b.ContainsKey(key)){result.Remove(key);continue;}
                if(!c.ContainsKey(key)) {if(!JsonNode.DeepEquals(a[key],b[key]))result[key]=b[key]?.DeepClone();continue;}
                result[key]=Merge(a[key],b[key],c[key]);
            }
            return result;
        }
        return current?.DeepClone();
    }
    public static void Detach(UiDefinition screen,string root)=>screen.ComponentInstances.RemoveAll(i=>i.Root==root);
}
