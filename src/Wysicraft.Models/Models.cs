using System.Text.Json;
namespace Wysicraft.Models;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = false };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidDataException("Empty JSON");
    public static T Clone<T>(T value) => Read<T>(Write(value));
}
public sealed class Manifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "untitled";
    public string Name { get; set; } = "Untitled";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string RuntimeVersion { get; set; } = "1.3.0";
    public string DefaultUi { get; set; } = "main";
    public List<string> Ui { get; set; } = ["main"];
    public List<string> Dependencies { get; set; } = [];
    public int GridSize { get; set; } = 8;
    public bool Snap { get; set; } = true;
}
public sealed class Project
{
    public Manifest Manifest { get; set; } = new();
    public List<UiDefinition> Screens { get; set; } = [new()];
    public Dictionary<string, string> Scripts { get; set; } = [];
    public Dictionary<string, byte[]> Assets { get; set; } = [];
}
public sealed class UiDefinition
{
    public bool ShowFrame { get; set; }
    public bool DimBackground { get; set; }
    public bool FitToScreen { get; set; } = true;
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "main";
    public string Title { get; set; } = "New screen";
    public Size Size { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = [];
    public Dictionary<string, UiEvent> Events { get; set; } = [];
    public List<Element> Elements { get; set; } = [];
}
public sealed class Size { public int Width { get; set; } = 320; public int Height { get; set; } = 200; }
public sealed class Bounds { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } = 100; public double Height { get; set; } = 20; }
public sealed class Element
{
    public int RowHeight { get; set; } = 30;
    public string PrimaryLabel { get; set; } = "";
    public string SecondaryLabel { get; set; } = "";
    public bool ShowItemId { get; set; } = true;
    public string Id { get; set; } = "element";
    public string Name { get; set; } = "";
    public string LayerGroup { get; set; } = "";
    public string Type { get; set; } = "button";
    public Bounds Bounds { get; set; } = new();
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public string Tooltip { get; set; } = "";
    public string Text { get; set; } = "Button";
    public string Foreground { get; set; } = "#FFFFFF";
    public string Background { get; set; } = "#40464F";
    public bool FillEnabled { get; set; } = true;
    public string BorderColor { get; set; } = "#697382";
    public double BorderWidth { get; set; }
    public double Opacity { get; set; } = 1;
    public double FontScale { get; set; } = 1;
    public string Font { get; set; } = "minecraft:default";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool TextShadow { get; set; }
    public string ShadowColor { get; set; } = "#000000";
    public double ShadowOpacity { get; set; } = 0.75;
    public double ShadowOffsetX { get; set; } = 1;
    public double ShadowOffsetY { get; set; } = 1;
    public double ShadowBlur { get; set; }
    public double CornerRadius { get; set; }
    public string Alignment { get; set; } = "left";
    public string Texture { get; set; } = "";
    public string Item { get; set; } = "minecraft:stone";
    public string Value { get; set; } = "0";
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 100;
    public List<string> Options { get; set; } = ["Option 1", "Option 2"];
    public string VisibleIf { get; set; } = "";
    public string EnabledIf { get; set; } = "";
    public string Parent { get; set; } = "";
    public int TextureX { get; set; }
    public int TextureY { get; set; }
    public int TextureWidth { get; set; } = 256;
    public int TextureHeight { get; set; } = 256;
    public Dictionary<string, UiEvent> Events { get; set; } = [];
}
public sealed class UiEvent { public EventHandler Client { get; set; } = new(); public EventHandler Server { get; set; } = new(); }
public sealed class EventHandler
{
    public int PermissionLevel { get; set; }
    public int CooldownTicks { get; set; } = 4;
    public string ScriptEngine { get; set; } = "standard";
    public List<VisualAction> Actions { get; set; } = [];
    public string Script { get; set; } = "";
    public string Function { get; set; } = "";
}
public sealed class VisualAction
{
    public string Type { get; set; } = "set_text";
    public string Target { get; set; } = "";
    public string Value { get; set; } = "";
}
