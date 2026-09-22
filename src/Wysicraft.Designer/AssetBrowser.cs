using System.IO;
using AvalonDock.Layout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wysicraft.Core;
using Validation = Wysicraft.Core.Validation;
using Wysicraft.Models;
using Wysicraft.Packaging;
namespace Wysicraft.Designer;

public partial class MainWindow
{
    sealed class BrowserEntry {
        public string Id {get;init;}="";
        public string Name {get;init;}="";
        public string Detail {get;init;}="";
        public Func<ImageSource?>? Load {get;init;}
        ImageSource? image;bool loaded;
        public ImageSource? Image {get {if(!loaded){loaded=true;try{image=Load?.Invoke();}catch{}}return image;}}
    }
    readonly MinecraftAssets minecraftAssets=new();
    readonly Dictionary<string,ImageSource?> itemImages=[];
    readonly ListBox assetList=new(),itemList=new();
    readonly TextBox assetSearch=new(),itemSearch=new();
    readonly TextBlock assetInfo=new(){TextWrapping=TextWrapping.Wrap},itemInfo=new(){TextWrapping=TextWrapping.Wrap};
    List<(string Id,string Name)> registeredItems=[];
    bool browsersReady;
    void InitializeAssetBrowsers() {
        Setup(AssetsHost,assetList,assetSearch,assetInfo,"Search project images",[
            ("Import PNGs",ImportBrowserImages),("Assign selected",()=>AssignBrowserAsset()),("Add image",()=>CreateBrowserElement(false)),("Replace",ReplaceBrowserAsset),("Find uses",FindBrowserUses),("Delete",DeleteBrowserAsset)]);
        Setup(ItemsHost,itemList,itemSearch,itemInfo,"Search item name or namespace:id",[
            ("Load Minecraft / mod JAR",LoadItemJar),("Read test items",LoadTestItems),("Assign selected",AssignBrowserItem),("Add item",()=>CreateBrowserElement(true))]);
        itemInfo.Text="Load local Minecraft assets for texture previews. Read test items after launching Minecraft to include registered mod items. 3D models render fully in-game.";
        assetSearch.TextChanged+=(_,_)=>RefreshAssetBrowser();itemSearch.TextChanged+=(_,_)=>RefreshItemBrowser();
        assetList.SelectionChanged+=(_,_)=>{if(assetList.SelectedItem is BrowserEntry entry)assetInfo.Text=entry.Id+"\n"+AssetUses(entry.Id).Count+" known reference(s).";};
        assetList.MouseDoubleClick+=(_,_)=>Guard(()=>AssignBrowserAsset());itemList.MouseDoubleClick+=(_,_)=>Guard(AssignBrowserItem);
        Drag(assetList,"wysicraft.asset");Drag(itemList,"wysicraft.item");
        Surface.Drop+=(_,args)=>Guard(()=>{var point=args.GetPosition(Surface);if(args.Data.GetData("wysicraft.asset") is string path){CreateBrowserElement(false,point.X/Zoom,point.Y/Zoom,path);args.Handled=true;}else if(args.Data.GetData("wysicraft.item") is string id){CreateBrowserElement(true,point.X/Zoom,point.Y/Zoom,id);args.Handled=true;}});
        browsersReady=true;
        Loaded+=(_,_)=>{if(DockSmoke)return;var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);var jar=Path.Combine(home,".gradle","caches","neoformruntime","artifacts","minecraft_1.21.1_client.jar");if(!File.Exists(jar))jar=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft","versions","1.21.1","1.21.1.jar");if(File.Exists(jar))try{minecraftAssets.LoadJar(jar);itemImages.Clear();RefreshItemBrowser();Draw();}catch(Exception ex){itemInfo.Text=ex.Message;}};
        void Setup(DockPanel host,ListBox list,TextBox search,TextBlock info,string hint,(string,System.Action)[] actions) {
            var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);host.Children.Add(top);top.Children.Add(new TextBlock {Text=hint,Margin=new Thickness(4)});top.Children.Add(search);
            var buttons=new WrapPanel();top.Children.Add(buttons);foreach(var (label,action) in actions){var button=new Button{Content=label};button.Click+=(_,_)=>Guard(action);buttons.Children.Add(button);}
            info.Margin=new Thickness(4);DockPanel.SetDock(info,Dock.Bottom);host.Children.Add(info);
            list.ItemTemplate=BrowserTemplate();ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);VirtualizingPanel.SetIsVirtualizing(list,true);VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);host.Children.Add(list);
        }
        void Drag(ListBox list,string format) {
            Point start=default;list.PreviewMouseLeftButtonDown+=(_,e)=>start=e.GetPosition(list);
            list.PreviewMouseMove+=(_,e)=>{var p=e.GetPosition(list);if(e.LeftButton==MouseButtonState.Pressed && (Math.Abs(p.X-start.X)>SystemParameters.MinimumHorizontalDragDistance || Math.Abs(p.Y-start.Y)>SystemParameters.MinimumVerticalDragDistance) && list.SelectedItem is BrowserEntry entry)DragDrop.DoDragDrop(list,new DataObject(format,entry.Id),DragDropEffects.Copy);};
        }
    }
    static DataTemplate BrowserTemplate() {
        var root=new FrameworkElementFactory(typeof(StackPanel));root.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);root.SetValue(FrameworkElement.MarginProperty,new Thickness(3));
        var image=new FrameworkElementFactory(typeof(Image));image.SetValue(FrameworkElement.WidthProperty,40d);image.SetValue(FrameworkElement.HeightProperty,40d);image.SetValue(RenderOptions.BitmapScalingModeProperty,BitmapScalingMode.NearestNeighbor);image.SetBinding(Image.SourceProperty,new Binding("Image"));root.AppendChild(image);
        var labels=new FrameworkElementFactory(typeof(StackPanel));labels.SetValue(FrameworkElement.MarginProperty,new Thickness(8,0,0,0));root.AppendChild(labels);
        foreach(var field in new[]{"Name","Detail"}) {var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(field));if(field=="Detail")text.SetValue(TextBlock.FontSizeProperty,10d);labels.AppendChild(text);}
        return new DataTemplate {VisualTree=root};
    }
    string AssetResource(string path) {
        if(path.StartsWith("assets/textures/"))return TextureAssets.Resource(project.Manifest.Id,path[16..]);
        var parts=path.Split('/',3);return parts[1]+":"+parts[2];
    }
    void RefreshAssetBrowser() {
        if(!browsersReady)return;var id=(assetList.SelectedItem as BrowserEntry)?.Id;var query=assetSearch.Text.Trim();
        assetList.ItemsSource=project.Assets.Where(p=>p.Key.EndsWith(".png",StringComparison.OrdinalIgnoreCase)&&p.Key.Contains(query,StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Key).Select(p=>new BrowserEntry {Id=p.Key,Name=Path.GetFileName(p.Key),Detail=AssetResource(p.Key),Load=()=>DecodeTexture(p.Value)}).ToArray();
        assetList.SelectedItem=assetList.Items.Cast<BrowserEntry>().FirstOrDefault(e=>e.Id==id);
    }
    void RefreshItemBrowser() {
        var query=itemSearch.Text.Trim();var items=registeredItems.Count>0?registeredItems:minecraftAssets.Items.ToList();
        itemList.ItemsSource=items.Where(p=>p.Id.Contains(query,StringComparison.OrdinalIgnoreCase)||p.Name.Contains(query,StringComparison.OrdinalIgnoreCase)).Select(p=>new BrowserEntry {Id=p.Id,Name=p.Name,Detail=p.Id,Load=()=>ItemImage(p.Id)}).ToArray();
        itemInfo.Text=$"{itemList.Items.Count} items • "+(registeredItems.Count>0?"Registered test items":"Asset-derived IDs; use Read test items for the exact registry")+". Texture previews; full models render in Minecraft.";
    }
    ImageSource? ItemImage(string id) {
        if(itemImages.TryGetValue(id,out var cached))return cached;
        try {var bytes=minecraftAssets.Texture(id);ImageSource? image=bytes==null?null:DecodeTexture(bytes);if(image is BitmapSource b && b.PixelHeight>b.PixelWidth)image=new CroppedBitmap(b,new Int32Rect(0,0,b.PixelWidth,b.PixelWidth));return itemImages[id]=image;}catch{return itemImages[id]=null;}
    }
    void ImportBrowserImages() {
        var dialog=new OpenFileDialog {Filter="PNG images|*.png",Multiselect=true};if(dialog.ShowDialog()!=true)return;
        var files=dialog.FileNames.Select(p=>(Path:p,Bytes:File.ReadAllBytes(p))).ToArray();foreach(var file in files){ProjectStore.TextureSize(file.Bytes);DecodeTexture(file.Bytes);}
        Change();foreach(var file in files){string stem=System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(file.Path).ToLowerInvariant(),"[^a-z0-9_-]","_");string path=TextureAssets.Path(project.Manifest.Id,stem+".png");int n=1;while(project.Assets.ContainsKey(path))path=TextureAssets.Path(project.Manifest.Id,stem+"_"+(n++)+".png");project.Assets[path]=file.Bytes;}RefreshAssetBrowser();
    }
    BrowserEntry ChosenAsset()=>assetList.SelectedItem as BrowserEntry??throw new InvalidOperationException("Select an image in Assets first.");
    void AssignBrowserAsset() {var entry=ChosenAsset();var targets=ui.Elements.Where(e=>selected.Contains(e.Id) && e.Type is "button" or "panel" or "scroll_panel" or "image" or "texture_region" or "label" or "progress").ToArray();if(targets.Length==0)throw new InvalidOperationException("Select a button, image, label or panel on the designer first.");Change();foreach(var e in targets){e.Texture=AssetResource(entry.Id);e.FillEnabled=true;}Draw();RefreshInspector();}
    void AssignBrowserItem() {var item=itemList.SelectedItem as BrowserEntry??throw new InvalidOperationException("Select a Minecraft item first.");var targets=ui.Elements.Where(e=>selected.Contains(e.Id)&&e.Type=="item").ToArray();if(targets.Length==0)throw new InvalidOperationException("Select an Item control, or use Add item.");Change();foreach(var e in targets)e.Item=item.Id;Draw();RefreshInspector();}
    void CreateBrowserElement(bool item,double x=16,double y=16,string? id=null) {
        id??=item?(itemList.SelectedItem as BrowserEntry)?.Id:ChosenAsset().Id;if(id==null)throw new InvalidOperationException("Select an item first.");
        var element=new Element {Id=Unique(item?"item":"image"),Type=item?"item":"image",FillEnabled=false,Bounds=new(){X=Snap(x),Y=Snap(y),Width=item?32:100,Height=item?32:100}};
        if(item)element.Item=id;else {element.Texture=AssetResource(id);var size=ProjectStore.TextureSize(project.Assets[id]);element.Bounds.Height=Math.Max(1,100d*size.Height/size.Width);element.Bounds.Height=Math.Min(4096,element.Bounds.Height);}
        Change();element.LayerGroup=isolatedGroup;ui.Elements.Add(element);selected.Clear();selected.Add(element.Id);Draw();RefreshInspector();
    }
    List<string> AssetUses(string path) {
        var uses=new List<string>();if(!project.Assets.TryGetValue(path,out var bytes))return uses;
        bool Matches(string value)=>TextureAssets.TryGet(project,value,out var found)&&ReferenceEquals(found,bytes);
        IEnumerable<Element> All(IEnumerable<Element> elements)=>elements.SelectMany(e=>new[]{e}.Concat(All(e.RowElements)));
        foreach(var screen in project.Screens) {
            foreach(var e in All(screen.Elements)){if(Matches(e.Texture))uses.Add(screen.Id+" / "+e.Id);foreach(var h in e.Events.Values.SelectMany(v=>new[]{v.Client,v.Server}))foreach(var a in h.Actions)if(a.Type=="change_texture"&&Matches(a.Value))uses.Add(screen.Id+" / "+e.Id+" event");}
            foreach(var h in screen.Events.Values.SelectMany(v=>new[]{v.Client,v.Server}))foreach(var a in h.Actions)if(a.Type=="change_texture"&&Matches(a.Value))uses.Add(screen.Id+" screen event");
        }
        foreach(var script in project.Scripts)if(script.Value.Contains(AssetResource(path),StringComparison.Ordinal)||script.Value.Contains(Path.GetFileName(path),StringComparison.Ordinal))uses.Add(script.Key);
        return uses.Distinct().ToList();
    }
    void FindBrowserUses(){var entry=ChosenAsset();var uses=AssetUses(entry.Id);assetInfo.Text=uses.Count==0?"No known references. Dynamically constructed script paths cannot be detected.":string.Join("\n",uses);}
    void ReplaceBrowserAsset(){var entry=ChosenAsset();var dialog=new OpenFileDialog{Filter="PNG image|*.png"};if(dialog.ShowDialog()!=true)return;var bytes=File.ReadAllBytes(dialog.FileName);ProjectStore.TextureSize(bytes);DecodeTexture(bytes);Change();project.Assets[entry.Id]=bytes;RefreshAssetBrowser();Draw();}
    void DeleteBrowserAsset(){var entry=ChosenAsset();var uses=AssetUses(entry.Id);if(uses.Count>0)throw new InvalidOperationException("Image is still used by:\n"+string.Join("\n",uses));Change();project.Assets.Remove(entry.Id);RefreshAssetBrowser();Draw();}
    void LoadItemJar(){var dialog=new OpenFileDialog{Filter="Minecraft client or mod JAR|*.jar",Multiselect=true};if(dialog.ShowDialog()!=true)return;foreach(var path in dialog.FileNames)minecraftAssets.LoadJar(path);itemImages.Clear();RefreshItemBrowser();Draw();}
    void LoadTestItems(){
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WYSICRAFT","MinecraftTest","1.21.1");var settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WYSICRAFT","minecraft-test.json");
        if(File.Exists(settings)){using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(settings));if(doc.RootElement.TryGetProperty("instance",out var path))root=path.GetString()??root;}
        string catalog=Path.Combine(root,".wysicraft-test","items.json");if(!File.Exists(catalog))throw new InvalidOperationException("Start an editor Minecraft test with this version first, then choose Read test items.");
        if(new FileInfo(catalog).Length>16*1024*1024)throw new InvalidDataException("Item catalog exceeds 16 MB.");
        using var data=System.Text.Json.JsonDocument.Parse(File.ReadAllText(catalog));registeredItems=data.RootElement.EnumerateArray().Select(e=>(Id:e.GetProperty("id").GetString()!,Name:e.GetProperty("name").GetString()!)).Where(e=>Validation.Resource(e.Id)).DistinctBy(e=>e.Id).OrderBy(e=>e.Name).ToList();
        foreach(var jar in Directory.Exists(Path.Combine(root,"mods"))?Directory.EnumerateFiles(Path.Combine(root,"mods"),"*.jar"):[])try{minecraftAssets.LoadJar(jar);}catch(InvalidDataException){}
        itemImages.Clear();RefreshItemBrowser();Draw();
    }
    internal void VerifyAssetBrowser(string output) {
        var pixels=new byte[]{255,120,40,255};var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,pixels,4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);
        var path=TextureAssets.Path(project.Manifest.Id,"browser_test.png");project.Assets[path]=stream.ToArray();RefreshAssetBrowser();assetList.SelectedItem=assetList.Items.Cast<BrowserEntry>().Single(e=>e.Id==path);
        CreateBrowserElement(false);var created=ui.Elements.Single(e=>selected.Contains(e.Id));if(created.Texture!=AssetResource(path)||AssetUses(path).Count!=1)throw new Exception("Asset assignment/reference detection failed");
        bool blocked=false;try{DeleteBrowserAsset();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Used asset was deletable");
        history.Undo();if(ui.Elements.Any(e=>e.Id==created.Id))throw new Exception("Undo did not remove created image");history.Redo();
        var jar=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".gradle","caches","neoformruntime","artifacts","minecraft_1.21.1_client.jar");minecraftAssets.LoadJar(jar);itemImages.Clear();itemSearch.Text="diamond sword";RefreshItemBrowser();
        itemList.SelectedItem=itemList.Items.Cast<BrowserEntry>().Single(e=>e.Id=="minecraft:diamond_sword");if(((BrowserEntry)itemList.SelectedItem).Image==null)throw new Exception("Minecraft texture preview missing");CreateBrowserElement(true);if(ui.Elements.Single(e=>selected.Contains(e.Id)).Item!="minecraft:diamond_sword")throw new Exception("Item assignment failed");
        foreach(string id in new[]{"assets","items"}){ShowDock(id);var pane=Workspace.Layout.Descendents().OfType<AvalonDock.Layout.LayoutAnchorable>().First(p=>p.ContentId==id);pane.Float();Flush();pane.Dock();Flush();}
        // AvalonDock shows floating windows asynchronously; let that finish before the test moves on or closes the app.
        void Flush()=>Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        // The image and item created above can sit over sample buttons; remove them before the button hit test.
        ui.Elements.RemoveAll(e=>e.Id==created.Id || e.Type=="item" && e.Item=="minecraft:diamond_sword");selected.Clear();Draw();UpdateLayout();
        VerifyCanvasSelection();ShowDock("items");UpdateLayout();dirty=false;File.WriteAllText(output,"PASS: PNG thumbnail/imported bytes, create image, reference guard, undo/redo, real Minecraft item lookup/texture, create item, panel docking and canvas selection");
    }
}
