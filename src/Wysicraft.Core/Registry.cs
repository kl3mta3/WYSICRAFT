using Wysicraft.Models;
namespace Wysicraft.Core;

public record ControlSpec(string Type, string DisplayName, string[] Properties, string[] Events);
public static class Registry
{
    public static readonly Dictionary<string, ControlSpec> Controls = new();
    public static readonly HashSet<string> ClientActions = ["set_text", "set_visible", "set_enabled", "set_value", "open_ui", "close_ui", "play_sound", "set_variable", "toggle_variable", "message", "change_texture"];
    public static readonly HashSet<string> ServerActions = ["command", "message", "set_variable", "toggle_variable", "open_ui", "close_ui", "server_function", "player_inventory"];
    static Registry()
    {
        Register("button", "Button", [], ["click"]);
        Register("label", "Label", [], []);
        Register("image", "Image", ["Texture"], []);
        Register("textbox", "Text Box", ["Value"], ["text_changed", "submit"]);
        Register("checkbox", "Checkbox", ["Value"], ["checked", "unchecked"]);
        Register("slider", "Slider", ["Value", "Minimum", "Maximum"], ["value_changed"]);
        Register("progress", "Progress Bar", ["Value", "Minimum", "Maximum"], []);
        Register("dropdown", "Dropdown", ["Value", "Options"], ["value_changed"]);
        Register("panel", "Panel", [], []);
        Register("scroll_panel", "Scroll Panel", [], []);
        Register("item", "Item Icon", ["Item"], []);
        Register("item_list", "Item List", ["Value", "RowHeight", "PrimaryLabel", "SecondaryLabel", "ShowItemId"], ["item_click", "item_primary", "item_secondary"]);
        Register("texture_region", "Texture Region", ["Texture", "TextureX", "TextureY", "TextureWidth", "TextureHeight"], []);
    }
    public static void Register(string type, string name, string[] properties, string[] events) => Controls.Add(type, new(type, name, properties, [.. events, "hover", "mouse_enter", "mouse_leave"]));
}
public sealed class History<T>(Func<T> capture, Action<T> restore)
{
    readonly Stack<T> undo = new(), redo = new();
    public void Checkpoint() { undo.Push(Json.Clone(capture())); redo.Clear(); }
    public void Undo() { if (undo.Count == 0) return; redo.Push(Json.Clone(capture())); restore(undo.Pop()); }
    public void Redo() { if (redo.Count == 0) return; undo.Push(Json.Clone(capture())); restore(redo.Pop()); }
    public void Clear() { undo.Clear(); redo.Clear(); }
}
