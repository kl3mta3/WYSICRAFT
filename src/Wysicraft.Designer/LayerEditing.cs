using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wysicraft.Models;

namespace Wysicraft.Designer;
public partial class MainWindow
{
    const string GroupPrefix="@group:";
    readonly HashSet<string> collapsedGroups=new();
    Point layerDragStart;
    string? layerDragRow;
    bool layersDragging;
    ListBoxItem? layerDropMarker;
    DateTime nextLayerScroll;
    void SetLayerOrder(List<Element> front) {
        var ordered=new List<Element>();var groups=new HashSet<string>();
        foreach(var e in front) {if(e.LayerGroup.Length==0)ordered.Add(e);else if(groups.Add(e.LayerGroup))ordered.AddRange(front.Where(x=>x.LayerGroup==e.LayerGroup));}
        ui.Elements=ordered.AsEnumerable().Reverse().ToList();
    }
    IEnumerable<string> LayerRowIds(string key) => key.StartsWith(GroupPrefix)
        ? ui.Elements.Where(e=>e.LayerGroup==key[GroupPrefix.Length..]).Select(e=>e.Id)
        : new[]{key};
    string UniqueLayerGroup(string name) { string result=name; int number=2; while(ui.Elements.Any(e=>e.LayerGroup==result)) result=name+" "+number++; return result; }
    void GroupSelected() {
        if(selected.Count<2) {Log("Select at least two layers to group.");return;}
        var name=Prompt("Group layers","Group name",UniqueLayerGroup("Group"));
        if(name!=null) SetLayerGroup(name);
    }
    void SetLayerGroup(string name) {
        name=name.Trim(); if(name.Length is <1 or >64 || name.Any(char.IsControl)) throw new InvalidOperationException("Use a group name of 1–64 characters.");
        if(ui.Elements.Any(e=>e.LayerGroup==name && !selected.Contains(e.Id))) throw new InvalidOperationException("That group already exists. Drag items into it instead.");
        Change(); var front=ui.Elements.AsEnumerable().Reverse().ToList(); var moved=front.Where(e=>selected.Contains(e.Id)).ToList();
        int index=front.FindIndex(e=>selected.Contains(e.Id)); if(index<0)return;
        front.RemoveAll(e=>selected.Contains(e.Id)); foreach(var e in moved)e.LayerGroup=name;
        front.InsertRange(Math.Min(index,front.Count),moved); SetLayerOrder(front); collapsedGroups.Remove(name);
        Draw(); RefreshInspector();
    }
    void UngroupSelected() {
        var names=ui.Elements.Where(e=>selected.Contains(e.Id) && e.LayerGroup.Length>0).Select(e=>e.LayerGroup).ToHashSet();
        if(names.Count==0)return; Change(); foreach(var e in ui.Elements.Where(e=>names.Contains(e.LayerGroup))) e.LayerGroup="";
        Draw(); RefreshInspector();
    }
    void RefreshLayerRows() {
        syncingLayers=true;
        try {
            var keys=new List<string>(); var seen=new HashSet<string>();
            foreach(var e in ui.Elements.AsEnumerable().Reverse()) {
                if(e.LayerGroup.Length==0) {keys.Add(e.Id);continue;}
                if(!seen.Add(e.LayerGroup))continue;
                keys.Add(GroupPrefix+e.LayerGroup);
                if(!collapsedGroups.Contains(e.LayerGroup)) keys.AddRange(ui.Elements.AsEnumerable().Reverse().Where(x=>x.LayerGroup==e.LayerGroup).Select(x=>x.Id));
            }
            if(!Layers.Items.Cast<ListBoxItem>().Select(i=>(string)i.Tag).SequenceEqual(keys)) {
                Layers.Items.Clear(); foreach(string key in keys) Layers.Items.Add(new ListBoxItem {Tag=key,Padding=new Thickness(6,4,2,4)});
            }
            foreach(ListBoxItem item in Layers.Items) {
                string key=(string)item.Tag;
                if(key.StartsWith(GroupPrefix)) {
                    string group=key[GroupPrefix.Length..]; var ids=LayerRowIds(key).ToList();
                    var row=new StackPanel {Orientation=Orientation.Horizontal};
                    var toggle=new Button {Content=collapsedGroups.Contains(group)?"▸":"▾",Padding=new Thickness(2,0,2,0),Margin=new Thickness(0),ToolTip="Expand / collapse group"};
                    toggle.Click+=(_,args)=> {if(!collapsedGroups.Add(group))collapsedGroups.Remove(group);RefreshLayers();args.Handled=true;};
                    row.Children.Add(toggle); row.Children.Add(new TextBlock {Text=group+" ("+ids.Count+")",FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center});
                    item.Content=row; item.IsSelected=false; row.Opacity=ids.All(selected.Contains)?1:0.8; item.ToolTip="Drag to reorder the group. Right-click for group actions.";
                    var menu=new ContextMenu();
                    void Add(string title,Action action) {var entry=new MenuItem {Header=title};entry.Click+=(_,_)=>Guard(()=>{selected.Clear();selected.UnionWith(ids);action();});menu.Items.Add(entry);}
                    Add("Rename group",()=>{var name=Prompt("Rename group","Group name",group);if(name!=null)SetLayerGroup(name);});
                    Add("Ungroup",UngroupSelected);Add("Duplicate",Duplicate);Add("Delete group and elements",Delete); item.ContextMenu=menu;
                } else {
                    var e=ui.Elements.First(e=>e.Id==key); item.Content=(e.LayerGroup.Length>0?"    ":"")+(e.Visible?"":"[hidden] ")+e.Id+" · "+e.Type;
                    item.ToolTip=e.Name.Length>0?e.Name:e.Text; item.IsSelected=selected.Contains(e.Id); item.ContextMenu=ElementMenu(e);
                }
            }
        } finally {syncingLayers=false;}
    }
    void InitializeLayerDragging() {
        Layers.AllowDrop=true;
        Layers.PreviewMouseLeftButtonDown+=(_,e)=> {
            layerDragStart=e.GetPosition(Layers); var row=ItemsControl.ContainerFromElement(Layers,e.OriginalSource as DependencyObject) as ListBoxItem;
            layerDragRow=row?.Tag as string;
            if(row?.IsSelected==true && Keyboard.Modifiers==ModifierKeys.None && e.OriginalSource is not Button) e.Handled=true;
        };
        Layers.PreviewMouseMove+=(_,e)=> {
            if(e.LeftButton!=MouseButtonState.Pressed || layerDragRow==null || layersDragging)return;
            var delta=e.GetPosition(Layers)-layerDragStart;
            if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            var ids=LayerRowIds(layerDragRow).ToArray(); if(!ids.All(selected.Contains)){selected.Clear();selected.UnionWith(ids);}
            layersDragging=true;
            try {DragDrop.DoDragDrop(Layers,new DataObject("Wysicraft.LayerOrder",selected.ToArray()),DragDropEffects.Move);}
            finally {layersDragging=false;layerDragRow=null;ClearLayerDropMarker();Draw();RefreshInspector();}
        };
        Layers.DragOver+=(_,e)=> {
            e.Effects=e.Data.GetDataPresent("Wysicraft.LayerOrder")?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;
            ClearLayerDropMarker(); if(e.Effects==DragDropEffects.None)return;
            if(DateTime.UtcNow>=nextLayerScroll) {
                var queue=new Queue<DependencyObject>();queue.Enqueue(Layers);
                while(queue.Count>0) {var node=queue.Dequeue();if(node is ScrollViewer scroll) {double y=e.GetPosition(Layers).Y;if(y<24)scroll.LineUp();else if(y>Layers.ActualHeight-24)scroll.LineDown();break;}for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)queue.Enqueue(VisualTreeHelper.GetChild(node,i));}
                nextLayerScroll=DateTime.UtcNow.AddMilliseconds(80);
            }
            layerDropMarker=ItemsControl.ContainerFromElement(Layers,e.OriginalSource as DependencyObject) as ListBoxItem;
            if(layerDropMarker!=null) {layerDropMarker.BorderBrush=Brushes.DeepSkyBlue;layerDropMarker.BorderThickness=e.GetPosition(layerDropMarker).Y<layerDropMarker.ActualHeight/2?new Thickness(0,2,0,0):new Thickness(0,0,0,2);}
        };
        Layers.DragLeave+=(_,_)=>ClearLayerDropMarker();
        Layers.Drop+=(_,e)=> {
            if(e.Data.GetData("Wysicraft.LayerOrder") is not string[] ids)return;
            var row=ItemsControl.ContainerFromElement(Layers,e.OriginalSource as DependencyObject) as ListBoxItem;
            string? key=row?.Tag as string; bool after=row!=null && e.GetPosition(row).Y>=row.ActualHeight/2;
            Guard(()=>ReorderLayerDrop(ids,key,after));ClearLayerDropMarker();e.Handled=true;
        };
        var buttons=(Panel)LayerDuplicate.Parent; foreach(var (label,action) in new (string,Action)[]{("Group",GroupSelected),("Ungroup",UngroupSelected)}) {var button=new Button {Content=label};button.Click+=(_,_)=>Guard(action);buttons.Children.Add(button);}
        if(buttons.Parent is DockPanel host) { int index=host.Children.IndexOf(buttons); var wrap=new WrapPanel(); while(buttons.Children.Count>0) {var child=buttons.Children[0];buttons.Children.RemoveAt(0);wrap.Children.Add(child);} host.Children.Remove(buttons);DockPanel.SetDock(wrap,Dock.Bottom);host.Children.Insert(index,wrap); }
    }
    void ClearLayerDropMarker() { if(layerDropMarker!=null) {layerDropMarker.ClearValue(Control.BorderThicknessProperty);layerDropMarker.ClearValue(Control.BorderBrushProperty);layerDropMarker=null;} }
    void ReorderLayerDrop(string[] ids,string? target,bool after) {
        var moving=ids.ToHashSet(); if(target!=null && LayerRowIds(target).All(moving.Contains))return;
        var front=ui.Elements.AsEnumerable().Reverse().ToList(); var moved=front.Where(e=>moving.Contains(e.Id)).ToList(); if(moved.Count==0)return;
        bool wholeGroup=moved.Any(e=>e.LayerGroup.Length>0 && ui.Elements.Where(x=>x.LayerGroup==e.LayerGroup).All(x=>moving.Contains(x.Id)));
        string destination=target==null?"":target.StartsWith(GroupPrefix)?target[GroupPrefix.Length..]:ui.Elements.First(e=>e.Id==target).LayerGroup;
        Change(); front.RemoveAll(e=>moving.Contains(e.Id));
        int index=front.Count;
        if(target!=null) {
            if(target.StartsWith(GroupPrefix) || wholeGroup && destination.Length>0) {int first=front.FindIndex(e=>e.LayerGroup==destination);int last=front.FindLastIndex(e=>e.LayerGroup==destination);index=after?last+1:first;}
            else {index=front.FindIndex(e=>e.Id==target)+(after?1:0);}
        }
        if(!wholeGroup) foreach(var e in moved)e.LayerGroup=destination;
        front.InsertRange(Math.Clamp(index,0,front.Count),moved);SetLayerOrder(front); selected.Clear();selected.UnionWith(ids);
        Draw();RefreshInspector();
    }
    internal void VerifyLayerEditing(string output) {
        project=new Project();ui=project.Screens[0];ui.Elements=[new Element {Id="one"},new Element {Id="two"},new Element {Id="three"}];history.Clear();selected.Clear();selected.UnionWith(new[]{"one","two"});
        SetLayerGroup("Controls");
        if(ui.Elements.Count(e=>e.LayerGroup=="Controls")!=2)throw new Exception("Grouping failed");
        ReorderLayerDrop(new[]{"one","two"},"three",false);
        if(ui.Elements.Last().Id!="two" || ui.Elements.First().Id!="three")throw new Exception("Group ordering failed");
        SelectCanvasElement("one",ModifierKeys.None);if(selected.Count!=2)throw new Exception("Canvas did not select group");
        SelectCanvasElement("one",ModifierKeys.Alt);if(selected.Count!=1)throw new Exception("Alt selection did not isolate item");
        ReorderLayerDrop(new[]{"three"},GroupPrefix+"Controls",true);
        if(ui.Elements.First(e=>e.Id=="three").LayerGroup!="Controls")throw new Exception("Drop into group failed");
        history.Undo();if(ui.Elements.First(e=>e.Id=="three").LayerGroup!="")throw new Exception("Undo failed");
        selected.Clear();selected.UnionWith(new[]{"one","two"});Duplicate();
        if(ui.Elements.Where(e=>selected.Contains(e.Id)).Any(e=>e.LayerGroup=="Controls" || e.LayerGroup.Length==0))throw new Exception("Duplicate did not isolate copied group");
        Wysicraft.Packaging.ProjectStore.SaveProject(project,output+".wysicraftproj");
        if(Wysicraft.Packaging.ProjectStore.Load(output+".wysicraftproj").Screens[0].Elements.Count(e=>e.LayerGroup.Length>0)!=4)throw new Exception("Groups were not saved");
        UngroupSelected();if(ui.Elements.Where(e=>selected.Contains(e.Id)).Any(e=>e.LayerGroup.Length>0))throw new Exception("Ungroup failed");
        dirty=false;System.IO.File.WriteAllText(output,"PASS: grouping, order, canvas/Alt selection, drop into group, undo, independent duplication, save/load and ungroup.");
    }
}
