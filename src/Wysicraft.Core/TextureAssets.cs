using Wysicraft.Models;
namespace Wysicraft.Core;

public static class TextureAssets
{
    public static string Path(string projectId, string name, string elementType = "image") => "assets/" + projectId + "/textures/gui/" + elementType + "/" + name;
    public static string Resource(string projectId, string name, string elementType = "image") => projectId + ":textures/gui/" + elementType + "/" + name;
    public static bool TryGet(Project project, string resource, out byte[] bytes)
    {
        bytes = [];
        if (!Validation.Resource(resource)) return false;
        var parts = resource.Split(':', 2);
        if (project.Assets.TryGetValue("assets/" + parts[0] + "/" + parts[1], out bytes!)) return true;
        if (parts[0] != project.Manifest.Id) return false;
        string name = parts[1].StartsWith("textures/gui/") ? parts[1][13..] : parts[1];
        if (name.StartsWith("image/")) name = name[6..];
        return project.Assets.TryGetValue(Path(parts[0], name), out bytes!) || project.Assets.TryGetValue("assets/" + parts[0] + "/textures/gui/" + name, out bytes!) || project.Assets.TryGetValue("assets/textures/" + name, out bytes!);
    }
    public static Dictionary<string, byte[]> CanonicalAssets(Project project)
    {
        var result = new Dictionary<string, byte[]>();
        foreach (var (path, bytes) in project.Assets) {
            string target = path.StartsWith("assets/textures/") ? Path(project.Manifest.Id, path[16..]) : path;
            string prefix = "assets/" + project.Manifest.Id + "/textures/gui/";
            if (target.StartsWith(prefix) && !target[prefix.Length..].Contains('/')) target = Path(project.Manifest.Id, target[prefix.Length..]);
            Validation.SafePath(target);
            if (!System.Text.RegularExpressions.Regex.IsMatch(target, @"^assets/[a-z0-9_.-]+/textures/.+\.png$")) throw new InvalidDataException("Invalid texture path: " + target);
            if (result.TryGetValue(target, out var existing) && !existing.SequenceEqual(bytes)) throw new InvalidDataException("Conflicting texture paths: " + target);
            result[target] = bytes;
        }
        return result;
    }
}
