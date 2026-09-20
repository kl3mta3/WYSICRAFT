using Wysicraft.Models;
namespace Wysicraft.Core;
public sealed record ItemRow(string Item, int Count, string Name);
public static class ItemRows
{
    public static List<ItemRow> Parse(string json) {
        if(json.Length>4096) throw new InvalidDataException("Item list exceeds 4096 characters");
        var rows=Json.Read<List<ItemRow>>(json);
        if(rows.Count>128 || rows.Any(r=>r==null || !Validation.Resource(r.Item) || r.Count<0 || r.Name==null || r.Name.Length>80)) throw new InvalidDataException("Expected up to 128 rows with item ID, nonnegative count and name (up to 80 characters)");
        return rows;
    }
}
