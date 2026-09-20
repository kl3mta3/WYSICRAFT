using System.Text.RegularExpressions;
using Wysicraft.Models;
namespace Wysicraft.Core;

public record Issue(string Ui, string Element, string Message) { public override string ToString() => $"{Ui}/{Element}: {Message}"; }
public static class Validation
{
    public static bool Id(string s) => s != null && Regex.IsMatch(s, "^[a-z][a-z0-9_]{0,63}$");
    public static bool Variable(string s) => s != null && Regex.IsMatch(s, "^[a-zA-Z_][a-zA-Z0-9_]{0,63}$");
    public static bool Resource(string s) => Regex.IsMatch(s, "^[a-z0-9_.-]+:[a-z0-9_./-]+$") && !s.Contains("..");
    public static bool Version(string s) => Regex.IsMatch(s, @"^\d+\.\d+\.\d+$") && System.Version.TryParse(s, out _);
    public static string SafePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 240 || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.Split('/').Any(s => s is "" or "." or ".." || s.EndsWith('.') || s.EndsWith(' ') || s.Any(char.IsControl))) throw new InvalidDataException($"Unsafe path: {path}");
        return path;
    }
    public static List<Issue> Check(Project project)
    {
        List<Issue> errors = []; void Add(string ui, string id, string msg) => errors.Add(new(ui, id, msg));
        var m = project.Manifest;
        if (!Id(m.Id)) Add("manifest", "", "Invalid pack ID");
        if (m.SchemaVersion != 1) Add("manifest", "", "Unsupported schema version");
        if (!Version(m.Version) || !Version(m.RuntimeVersion)) Add("manifest", "", "Versions must be major.minor.patch");
        else if (System.Version.Parse(m.RuntimeVersion) > new System.Version(1,3,0)) Add("manifest", "", "Minimum runtime exceeds 1.3.0");
        if (!project.Screens.Any(s => s.Id == m.DefaultUi)) Add("manifest", "", "Default UI does not exist");
        HashSet<string> uis = [];
        foreach (var ui in project.Screens)
        {
            if (!Id(ui.Id) || !uis.Add(ui.Id)) Add(ui.Id, "", "Invalid or duplicate UI ID");
            if (ui.SchemaVersion != 1) Add(ui.Id, "", "Unsupported schema version");
            if (ui.Size.Width is < 16 or > 4096 || ui.Size.Height is < 16 or > 4096 || ui.Elements.Count > 512) Add(ui.Id, "", "Canvas or element limit exceeded");
            HashSet<string> ids = [];
            foreach (var e in ui.Elements)
            {
                if (!Id(e.Id) || !ids.Add(e.Id)) Add(ui.Id, e.Id, "Invalid or duplicate element ID");
                if (!Registry.Controls.ContainsKey(e.Type)) Add(ui.Id, e.Id, "Unknown control type: " + e.Type);
                if (!double.IsFinite(e.Bounds.X + e.Bounds.Y + e.Bounds.Width + e.Bounds.Height) || e.Bounds.Width < 1 || e.Bounds.Height < 1 || e.Bounds.Width > 4096 || e.Bounds.Height > 4096) Add(ui.Id, e.Id, "Invalid bounds");
                if (e.Opacity < 0 || e.Opacity > 1 || e.FontScale <= 0 || e.FontScale > 8 || e.Maximum <= e.Minimum) Add(ui.Id, e.Id, "Invalid appearance or value range");
                if (!Resource(e.Font) || !double.IsFinite(e.CornerRadius) || e.CornerRadius < 0 || e.CornerRadius > 128) Add(ui.Id, e.Id, "Invalid font resource or corner radius (0–128)");
                if (!double.IsFinite(e.BorderWidth + e.ShadowOpacity + e.ShadowOffsetX + e.ShadowOffsetY + e.ShadowBlur) || e.BorderWidth is < 0 or > 32 || e.ShadowOpacity is < 0 or > 1 || Math.Abs(e.ShadowOffsetX) > 64 || Math.Abs(e.ShadowOffsetY) > 64 || e.ShadowBlur is < 0 or > 16) Add(ui.Id, e.Id, "Invalid border/shadow settings");
                foreach (var color in new[] { e.Foreground, e.Background, e.BorderColor, e.ShadowColor }) if (!Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) Add(ui.Id, e.Id, "Color must be #RRGGBB or #AARRGGBB");
                if (e.Parent != "" && !ui.Elements.Any(p => p.Id == e.Parent && p.Id != e.Id && p.Parent == "" && p.Type is "panel" or "scroll_panel")) Add(ui.Id, e.Id, "Parent must be a root panel");
                foreach (string condition in new[] { e.VisibleIf, e.EnabledIf }) try { Expressions.Evaluate(condition, ui.Variables); } catch (FormatException ex) { Add(ui.Id, e.Id, ex.Message); }
                if (e.Texture != "") { if (!Resource(e.Texture)) Add(ui.Id, e.Id, "Invalid texture resource"); else if (e.Texture.StartsWith(m.Id + ":") && !TextureAssets.TryGet(project, e.Texture, out _)) Add(ui.Id, e.Id, "Missing texture asset"); }
                if (e.Type == "item" && !Resource(e.Item)) Add(ui.Id, e.Id, "Invalid item identifier");
                if(e.RowHeight<24 || e.RowHeight>128 || e.PrimaryLabel.Length>24 || e.SecondaryLabel.Length>24) Add(ui.Id,e.Id,"Row height must be 24–128; button labels at most 24 characters");
                if(e.Type=="item_list") try { ItemRows.Parse(e.Value); } catch(Exception ex) { Add(ui.Id,e.Id,ex.Message); }
                CheckEvents(ui, e.Id, e.Events, Registry.Controls.GetValueOrDefault(e.Type)?.Events ?? []);
            }
            CheckEvents(ui, "", ui.Events, ["open", "close"]);
        }
        foreach (var path in project.Scripts.Keys.Concat(project.Assets.Keys)) try { SafePath(path); } catch (Exception ex) { Add("files", "", ex.Message); }
        return errors;

        void CheckEvents(UiDefinition ui, string id, Dictionary<string, UiEvent> events, string[] allowed)
        {
            foreach (var (name, ev) in events)
            {
                if (!allowed.Contains(name)) Add(ui.Id, id, "Invalid event: " + name);
                foreach (bool server in new[] { false, true })
                {
                    var h = server ? ev.Server : ev.Client; string side = server ? "server" : "client";
                    if (h.ScriptEngine is not ("standard" or "kubejs") || (!server && h.ScriptEngine == "kubejs")) Add(ui.Id, id, "KubeJS scripts must use a Server event");
                    if(h.PermissionLevel<0 || h.PermissionLevel>4 || h.CooldownTicks<0 || h.CooldownTicks>1200) Add(ui.Id,id,"Permission must be 0–4 and cooldown 0–1200 ticks");
                    if (h.Actions.Count > 64) Add(ui.Id, id, "Too many actions");
                    foreach (var a in h.Actions)
                    {
                        if (!(server ? Registry.ServerActions : Registry.ClientActions).Contains(a.Type)) Add(ui.Id, id, "Unknown " + side + " action: " + a.Type);
                        if (new[] { "set_text", "set_visible", "set_enabled", "set_value", "change_texture" }.Contains(a.Type) && !ui.Elements.Any(e => e.Id == a.Target)) Add(ui.Id, id, "Missing action target: " + a.Target);
                        if (a.Type == "open_ui" && !project.Screens.Any(s => s.Id == a.Value)) Add(ui.Id, id, "Missing destination UI: " + a.Value);
                        if (a.Type is "set_variable" or "toggle_variable" && !Variable(a.Target)) Add(ui.Id, id, "Invalid variable name");
                        if(a.Type=="player_inventory" && !ui.Elements.Any(e=>e.Id==a.Target && e.Type=="item_list")) Add(ui.Id,id,"Player inventory action requires an Item List target");
                        if (a.Value.Length > 4096 || a.Target.Length > 256) Add(ui.Id, id, "Action exceeds string limit");
                    }
                    if (h.Script != "" || h.Function != "")
                    {
                        if (!h.Script.StartsWith("scripts/" + side + "/") || !project.Scripts.TryGetValue(h.Script, out var source)) Add(ui.Id, id, "Missing or wrong-side script: " + h.Script);
                        else if (source.Length > 65536) Add(ui.Id, id, "Script exceeds 64 KiB");
                        if (!Regex.IsMatch(h.Function, "^[a-zA-Z_][a-zA-Z0-9_]*$")) Add(ui.Id, id, "Invalid script function");
                    }
                }
            }
        }
    }
}
