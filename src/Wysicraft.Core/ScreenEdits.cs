using Wysicraft.Models;
namespace Wysicraft.Core;
public static class ScreenEdits
{
    public static void Delete(Project project,string id,string replacement) {
        var screen=project.Screens.Single(s=>s.Id==id);
        if(screen.IsComponent)throw new InvalidOperationException("Delete component sources from Components.");
        if(!project.Screens.Any(s=>s.Id==replacement && s.Id!=id && !s.IsComponent))throw new InvalidOperationException("Keep at least one screen and choose a replacement screen.");
        if(project.Screens.Any(s=>s.Id!=id && s.Elements.Any(e=>e.RowTemplate==id)))throw new InvalidOperationException("This screen is used as an Item List row template. Reassign those row templates before deleting it.");
        foreach(var s in project.Screens.Where(s=>s!=screen))foreach(var ev in s.Events.Values.Concat(s.Elements.SelectMany(e=>e.Events.Values)))
            foreach(var action in ev.Client.Actions.Concat(ev.Server.Actions))if(action.Type=="open_ui" && action.Value==id)action.Value=replacement;
        if(project.Manifest.DefaultUi==id)project.Manifest.DefaultUi=replacement;
        project.Screens.Remove(screen);project.Manifest.Ui=project.Screens.Select(s=>s.Id).ToList();
    }
}
