using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Wysicraft.Models;

namespace Wysicraft.Core;

public sealed class ProjectEdit
{
    public string Kind { get; set; } = "";
    public string Screen { get; set; } = "";
    public string Element { get; set; } = "";
    public string Key { get; set; } = "";
    public string Source { get; set; } = "";
    public JsonElement Data { get; set; }
}

public static class ProjectEdits
{
    static readonly JsonSerializerOptions Strict = new(Json.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static Project Apply(Project original, IReadOnlyList<ProjectEdit> edits)
    {
        if (edits.Count is < 1 or > 128) throw new InvalidDataException("Use 1–128 edits per batch.");
        var project = Json.Clone(original);
        foreach (var edit in edits)
        {
            UiDefinition Screen() => project.Screens.SingleOrDefault(s => s.Id == edit.Screen) ?? throw new InvalidDataException("Screen not found: " + edit.Screen);
            switch (edit.Kind)
            {
                case "add_component_template": ComponentStarters.Add(project,edit.Key);break;
                case "create_component": Components.Capture(project,Screen(),edit.Data.GetProperty("ids").Deserialize<string[]>(Json.Options)!,edit.Key);break;
                case "place_component": Components.Place(project,Screen(),edit.Key,edit.Data.GetProperty("x").GetDouble(),edit.Data.GetProperty("y").GetDouble());break;
                case "update_component":
                    var componentScreen=Screen();Components.Update(project,componentScreen,componentScreen.ComponentInstances.Single(i=>i.Root==edit.Element),!edit.Data.TryGetProperty("reset",out var reset)||!reset.GetBoolean());break;
                case "detach_component": Components.Detach(Screen(),edit.Element);break;
                case "arrange":
                    var layout=Screen();
                    var ids=edit.Data.GetProperty("ids").Deserialize<string[]>(Json.Options) ?? throw new InvalidDataException("Arrange requires selected element IDs");
                    if(ids.Any(id=>!layout.Elements.Any(e=>e.Id==id)))throw new InvalidDataException("Arrange selection contains a missing element");
                    if(!Enum.TryParse<ArrangeOperation>(edit.Data.GetProperty("operation").GetString(),true,out var operation) || !Enum.IsDefined(operation))throw new InvalidDataException("Unknown arrange operation");
                    Arrangement.Apply(layout,Arrangement.Plan(layout,ids,operation,!edit.Data.TryGetProperty("keepGroups",out var keepGroups) || keepGroups.GetBoolean()));break;
                case "set_project":
                    string oldId=project.Manifest.Id;
                    project.Manifest=Patch(project.Manifest,edit.Data);
                    if(oldId!=project.Manifest.Id) {
                        string Rename(string value)=>value.StartsWith(oldId+":")?project.Manifest.Id+value[oldId.Length..]:value;
                        project.Assets=project.Assets.ToDictionary(p=>p.Key.StartsWith("assets/"+oldId+"/")?"assets/"+project.Manifest.Id+"/"+p.Key[(8+oldId.Length)..]:p.Key,p=>p.Value);
                        foreach(var s in project.Screens) {
                            foreach(var e in s.Elements) { e.Texture=Rename(e.Texture); e.Font=Rename(e.Font); }
                            foreach(var ev in s.Events.Values.Concat(s.Elements.SelectMany(e=>e.Events.Values))) foreach(var h in new[]{ev.Client,ev.Server}) foreach(var a in h.Actions) if(a.Type=="change_texture") a.Value=Rename(a.Value);
                        }
                    }
                    break;
                case "upsert_screen":
                    var old = project.Screens.SingleOrDefault(s => s.Id == edit.Screen);
                    var screen = Patch(old ?? new UiDefinition { Id = edit.Screen },edit.Data);
                    if (screen.Id != edit.Screen) throw new InvalidDataException("Screen ID must match screen; renaming IDs is not supported here.");
                    if (old == null) project.Screens.Add(screen); else project.Screens[project.Screens.IndexOf(old)] = screen;
                    break;
                case "delete_screen": project.Screens.Remove(Screen()); break;
                case "upsert_element":
                    var target = Screen(); var before = target.Elements.SingleOrDefault(e => e.Id == edit.Element);
                    var element = Patch(before ?? new Element { Id = edit.Element },edit.Data);
                    if (element.Id != edit.Element) throw new InvalidDataException("Element ID must match element.");
                    if (before == null) target.Elements.Add(element); else target.Elements[target.Elements.IndexOf(before)] = element;
                    break;
                case "delete_element":
                    var owner = Screen();
                    if (owner.Elements.RemoveAll(e => e.Id == edit.Element) == 0) throw new InvalidDataException("Element not found: " + edit.Element);
                    foreach (var child in owner.Elements.Where(e => e.Parent == edit.Element)) child.Parent = "";
                    break;
                case "put_script":
                    Validation.SafePath(edit.Key);
                    if (!(edit.Key.StartsWith("scripts/client/") || edit.Key.StartsWith("scripts/server/")) || !edit.Key.EndsWith(".js") || System.Text.Encoding.UTF8.GetByteCount(edit.Source)>65536)
                        throw new InvalidDataException("Use scripts/client/name.js or scripts/server/name.js, at most 64 KiB.");
                    project.Scripts[edit.Key] = edit.Source; break;
                case "delete_script":
                    if (!project.Scripts.Remove(edit.Key)) throw new InvalidDataException("Script not found: " + edit.Key);
                    break;
                case "delete_asset":
                    if (!project.Assets.Remove(edit.Key)) throw new InvalidDataException("Asset not found: " + edit.Key);
                    break;
                case "set_event":
                    var events = edit.Element.Length == 0 ? Screen().Events : Screen().Elements.Single(e => e.Id == edit.Element).Events;
                    events[edit.Key] = Patch(events.GetValueOrDefault(edit.Key) ?? new UiEvent(),edit.Data); break;
                case "set_main": project.Manifest.DefaultUi = Screen().Id; break;
                default: throw new InvalidDataException("Unknown edit kind: " + edit.Kind);
            }
        }
        project.Manifest.Ui = project.Screens.Select(s => s.Id).ToList();
        var errors = Validation.Check(project);
        if (errors.Count > 0) throw new InvalidDataException(string.Join("\n",errors));
        return project;
    }
    static T Patch<T>(T original,JsonElement changes)
    {
        if (changes.ValueKind != JsonValueKind.Object) throw new InvalidDataException("data must be a JSON object.");
        var node = JsonNode.Parse(Json.Write(original))!.AsObject();
        Merge(node,JsonNode.Parse(changes.GetRawText())!.AsObject());
        return JsonSerializer.Deserialize<T>(node.ToJsonString(),Strict) ?? throw new InvalidDataException("Empty edit.");
    }
    static void Merge(JsonObject target,JsonObject changes)
    {
        foreach (var pair in changes)
            if (pair.Value is JsonObject child && target[pair.Key] is JsonObject existing) Merge(existing,child);
            else target[pair.Key] = pair.Value?.DeepClone();
    }
}

