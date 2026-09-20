using Wysicraft.Models;
namespace Wysicraft.Core;

/// <summary>Reusable display layouts. Row buttons route to the owning list's trusted events.</summary>
public static class RowTemplates
{
    public static readonly string[] Types = ["panel", "label", "image", "texture_region", "item", "button", "progress"];
    public static readonly string[] Actions = ["", "item_click", "item_primary", "item_secondary"];
    public static List<Element> Resolve(Project project, Element list) => list.RowTemplate.Length == 0
        ? list.RowElements : project.Screens.FirstOrDefault(s => s.Id == list.RowTemplate)?.Elements
            ?? throw new InvalidDataException("Missing row template screen: " + list.RowTemplate);
    public static void Check(IEnumerable<Element> elements)
    {
        var ui = new UiDefinition { Elements = elements.ToList() };
        if (ui.Elements.Count > 64) throw new InvalidDataException("Row templates support up to 64 elements");
        foreach (var e in ui.Elements) {
            if (!Types.Contains(e.Type) || e.RowTemplate.Length > 0 || e.RowElements.Count > 0)
                throw new InvalidDataException("Row templates support panels, labels, images, items, buttons and progress bars; recursive lists are not supported");
            if (!Actions.Contains(e.RowAction) || e.RowAction.Length > 0 && e.Type != "button")
                throw new InvalidDataException("Row action must be an Item List event on a button");
            if (e.Events.Values.Any(v => new[] { v.Client, v.Server }.Any(h => h.Actions.Count > 0 || h.Script.Length > 0)))
                throw new InvalidDataException("Assign row button actions through Row action and the owning Item List events");
            ContainerTree.Ancestors(ui, e).ToArray();
        }
    }
    public static Element Bind(Element source, ItemRow row, int index)
    {
        var e = Json.Clone(source);
        string BindText(string s) => s.Replace("${row.item}", row.Item).Replace("${row.name}", row.Name)
            .Replace("${row.count}", row.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("${row.index}", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        e.Text = BindText(e.Text); e.Item = BindText(e.Item); e.Value = BindText(e.Value); e.Tooltip = BindText(e.Tooltip);
        return e;
    }
}
