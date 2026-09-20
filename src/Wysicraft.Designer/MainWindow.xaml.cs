using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Wysicraft.Core;
using Wysicraft.Models;
using Wysicraft.Packaging;
using UiElement = Wysicraft.Models.Element;
using Action = System.Action;
using Registry = Wysicraft.Core.Registry;
using Validation = Wysicraft.Core.Validation;
namespace Wysicraft.Designer;

public partial class MainWindow : Window
{
    Project project = new(); UiDefinition ui; readonly HashSet<string> selected = [];
    readonly History<Project> history; string? folder; bool refreshing, dirty; bool grid = true;
    const double Zoom = 2; Point dragStart; Dictionary<string, Bounds>? dragBounds; string? editingScript;
    public MainWindow()
    {
        InitializeComponent(); ui = project.Screens[0];
        history = new(() => project, value => { project = value; ui = project.Screens.FirstOrDefault(s => s.Id == ui.Id) ?? project.Screens[0]; dirty = true; RefreshAll(); });
        BuildMenus(); InitializeDocking();
        ScriptEditor.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled=true; ShowScriptApi(); }};
        InitializeLayers();
        foreach (var spec in Registry.Controls.Values) Toolbox.Items.Add(new ListBoxItem { Content = spec.DisplayName, Tag = spec.Type, Padding = new Thickness(8) });
        Toolbox.PreviewMouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && Toolbox.SelectedItem is ListBoxItem item) DragDrop.DoDragDrop(Toolbox, new DataObject("control", item.Tag), DragDropEffects.Copy); };
        Toolbox.MouseDoubleClick += (_, _) => { if (Toolbox.SelectedItem is ListBoxItem item) AddControl((string)item.Tag, 16, 16); };
        Surface.Drop += (_, e) => { if (e.Data.GetData("control") is string type) { var p = e.GetPosition(Surface); AddControl(type, p.X / Zoom, p.Y / Zoom); } };
        Surface.MouseLeftButtonDown += (_, e) => { if (e.Source == Surface) { selected.Clear(); RefreshInspector(); Draw(); Surface.Focus(); } };
        Surface.MouseMove += DragMove; Surface.MouseLeftButtonUp += (_, _) => { if (dragBounds != null) { dragBounds = null; Surface.ReleaseMouseCapture(); RefreshInspector(); } };
        Screens.SelectionChanged += (_, _) => { if (!refreshing && Screens.SelectedItem is string id) { ui = project.Screens.First(s => s.Id == id); selected.Clear(); Draw(); RefreshInspector(); } };
        AddScreen.Click += (_, _) => Guard(() => { string? id = Prompt("New screen", "Screen ID", "screen_" + project.Screens.Count); if (id == null) return; if (!Validation.Id(id) || project.Screens.Any(s => s.Id == id)) throw new Exception("Use a unique lowercase ID"); Change(); ui = new() { Id = id, Title = id }; project.Screens.Add(ui); selected.Clear(); RefreshAll(); });
        ScreenSettings.Click += (_, _) => EditScreen();
        NewScript.Click += (_, _) => Guard(() => { var path = Prompt("New script", "Path (scripts/client/name.js or scripts/server/name.js)", "scripts/client/main.js"); if (path == null) return; ValidateScriptPath(path); if (project.Scripts.ContainsKey(path)) throw new Exception("Script already exists"); Change(); project.Scripts[path] = "function onClick(ctx) {\n  // Use only the approved WYSICRAFT API.\n}\n"; RefreshScripts(path); });
        OpenScript.Click += (_, _) => Guard(() => { var dialog = new OpenFileDialog { Filter = "JavaScript|*.js" }; if (dialog.ShowDialog() != true) return; string? path = Prompt("Import script", "Project path", "scripts/client/" + System.IO.Path.GetFileName(dialog.FileName)); if (path == null) return; ValidateScriptPath(path); Change(); project.Scripts[path] = File.ReadAllText(dialog.FileName); RefreshScripts(path); });
        SaveScript.Click += (_, _) => Guard(() => { SaveScriptText(); Save(false); });
        ScriptFiles.SelectionChanged += (_, _) => { if (refreshing) return; if (ScriptTemplate.All.FirstOrDefault(t=>t.Title == ScriptFiles.SelectedItem as string) is ScriptTemplate template) { Guard(()=>ChooseScriptTemplate(template)); return; } SaveScriptText(); editingScript = ScriptFiles.SelectedItem as string; ScriptEditor.Text = editingScript == null ? "" : project.Scripts[editingScript]; ScriptSide.Text = editingScript?.Contains("/server/") == true ? "SERVER • trusted host only" : "CLIENT"; };
        ScriptEditor.TextChanged += (_, _) => { LineNumbers.Text = string.Join("\n", Enumerable.Range(1, Math.Min(1000, ScriptEditor.LineCount > 0 ? ScriptEditor.LineCount : 1))); };
        PreviewKeyDown += Keys;
        Closing += (_, e) => { SaveScriptText(); if (dirty) { var result = MessageBox.Show(this, "Save changes before closing?", "WYSICRAFT", MessageBoxButton.YesNoCancel); if (result == MessageBoxResult.Cancel) e.Cancel = true; if (result == MessageBoxResult.Yes) { Save(false); e.Cancel = dirty; } } };
        RefreshAll();
    }
    void Guard(Action action) { try { action(); } catch (Exception ex) { Log(ex.Message); MessageBox.Show(this, ex.Message, "WYSICRAFT", MessageBoxButton.OK, MessageBoxImage.Error); } }
    void Change() { SaveScriptText(); history.Checkpoint(); dirty = true; }
    void Log(string text) { Output.Items.Add(text); Output.ScrollIntoView(Output.Items[^1]); Status.Text = text.Split('\n')[0]; }
    internal void LoadForSmoke(string path) { project = ProjectStore.Load(path); ui = project.Screens[0]; selected.Add(ui.Elements.First().Id); RefreshAll(); dirty = false; }
    internal void Capture(string path)
    {
        UpdateLayout(); VerifyCanvasSelection(); VerifyAppearanceTools(); UpdateLayout(); var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }
    void VerifyCanvasSelection()
    {
        // Exercise WPF hit testing at the control center, not just its outline.
        foreach (var element in ui.Elements.Where(e => e.Type == "button"))
        {
            var center = new Point((element.Bounds.X + element.Bounds.Width / 2) * Zoom, (element.Bounds.Y + element.Bounds.Height / 2) * Zoom);
            if (Surface.InputHitTest(center) is not Border border || !Equals(border.Tag, element.Id))
                throw new InvalidOperationException("Canvas did not hit the placed button: " + element.Id);
            border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
            if (!selected.Contains(element.Id)) throw new InvalidOperationException("Canvas did not select: " + element.Id);
            dragBounds = null; Surface.ReleaseMouseCapture(); UpdateLayout();
        }
        dirty = false; history.Clear();
    }
    void BuildMenus()
    {
        MenuItem Menu(string header, params (string, Action)[] entries) { var menu = new MenuItem { Header = header }; foreach (var (name, action) in entries) { var item = new MenuItem { Header = name }; item.Click += (_, _) => Guard(action); menu.Items.Add(item); } Menus.Items.Add(menu); return menu; }
        Menu("_File", ("New Project", NewProject), ("Open Project / Pack", OpenProject), ("Save", () => Save(false)), ("Save As", () => Save(true)), ("Export Pack", Export), ("Export for KubeJS", ExportKube), ("Exit", Close));
        Menu("_Edit", ("Undo", history.Undo), ("Redo", history.Redo), ("Cut", () => { Copy(); Delete(); }), ("Copy", Copy), ("Paste", Paste), ("Duplicate", Duplicate), ("Group", GroupSelected), ("Ungroup", UngroupSelected), ("Delete", Delete));
        var viewMenu = Menu("_View", ("Reset Layout", ResetDockLayout), ("Grid", () => { grid = !grid; Draw(); }), ("Snap to Grid", () => { Change(); project.Manifest.Snap = !project.Manifest.Snap; }));
        var panelsMenu = new MenuItem { Header = "_Panels" };
        foreach (var (title, id) in new[] { ("Toolbox", "toolbox"), ("Layers", "layers"), ("Properties", "properties"), ("Events", "events"), ("Scripts", "scripts"), ("Output", "output") }) {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => Guard(() => ShowDock(id));
            panelsMenu.Items.Add(item);
        }
        viewMenu.Items.Insert(0, panelsMenu);
        Menu("_Project", ("Validate", Validate), ("Export", Export), ("Export for KubeJS", ExportKube), ("Project Settings", Settings), ("Import Texture", ImportTexture), ("Preview", Preview), ("Test in Minecraft", TestMinecraft));
        Menu("_Help", ("Script API / snippets", ShowScriptApi), ("About", () => MessageBox.Show(this, "WYSICRAFT 1.3.2\nVisual GUI designer for Minecraft 1.21.1 / NeoForge\nClient and server JavaScript use the bundled engine.\nSee docs in the repository.", "About WYSICRAFT")));
        foreach (var (label, action) in new (string, Action)[] { ("▶ Preview", Preview), ("Minecraft test", TestMinecraft), ("Validate", Validate), ("Export…", Export), ("Export for KubeJS", ExportKube) }) { var button = new Button { Content = label }; button.Click += (_, _) => Guard(action); Toolbar.Children.Add(button); }
        AddMcpButton();
        Toolbar.Children.Add(new TextBlock { Text = "  200%  •  Minecraft GUI pixels", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.LightGray });
    }
    bool CanReplace() { SaveScriptText(); if (!dirty) return true; var result = MessageBox.Show(this, "Save current project first?", "WYSICRAFT", MessageBoxButton.YesNoCancel); if (result == MessageBoxResult.Cancel) return false; if (result == MessageBoxResult.Yes) { Save(false); return !dirty; } return true; }
    void NewProject() { if (!CanReplace()) return; string? name = Prompt("New project", "Project name", "Untitled"); if (name == null) return; project = new(); project.Manifest.Name = name; project.Manifest.Id = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9_]", "_"); if (!Validation.Id(project.Manifest.Id)) project.Manifest.Id = "new_project"; ui = project.Screens[0]; folder = null; selected.Clear(); history.Clear(); dirty = true; RefreshAll(); Settings(); }
    void OpenProject() {
        if (!CanReplace()) return;
        var dialog = new OpenFileDialog { Filter = "WYSICRAFT files|*.wysicraftproj;*.wysicraft;project.json|Editable project|*.wysicraftproj|Legacy project|project.json|Runtime pack|*.wysicraft" };
        if (dialog.ShowDialog() != true) return;
        string path = dialog.FileName;
        var loaded = ProjectStore.Load(System.IO.Path.GetFileName(path)=="project.json" ? System.IO.Path.GetDirectoryName(path)! : path);
        project=loaded; folder=path.EndsWith(".wysicraftproj",StringComparison.OrdinalIgnoreCase)?path:null;
        ui=project.Screens.FirstOrDefault(s=>s.Id==project.Manifest.DefaultUi) ?? project.Screens.First();
        selected.Clear(); history.Clear(); dirty=false; editingScript=null; RefreshAll(); Log("Loaded " + project.Manifest.Name);
    }
    void Save(bool saveAs) { Guard(() => {
        SaveScriptText(); string? destination=folder;
        if(destination==null || saveAs) {
            var dialog=new SaveFileDialog { Title="Save WYSICRAFT project", Filter="WYSICRAFT Project|*.wysicraftproj", DefaultExt=".wysicraftproj", AddExtension=true, FileName=folder==null?project.Manifest.Id+".wysicraftproj":System.IO.Path.GetFileName(folder) };
            if(dialog.ShowDialog()!=true) return; destination=dialog.FileName;
        }
        ProjectStore.SaveProject(project,destination); folder=destination; dirty=false; Log("Saved " + destination);
    }); }
    void ExportPack() { SaveScriptText(); Validate(); if (Validation.Check(project).Count > 0) return; var dialog = new SaveFileDialog { Filter = "WYSICRAFT Pack|*.wysicraft", FileName = project.Manifest.Id + ".wysicraft" }; if (dialog.ShowDialog() != true) return; ProjectStore.Export(project, dialog.FileName); Log("Exported " + dialog.FileName); }
    void ExportKube() { SaveScriptText(); var dialog = new SaveFileDialog { Filter = "Bundled project JAR|*.jar", FileName = project.Manifest.Id + ".jar" }; if (dialog.ShowDialog() != true) return; Distribution.Write(dialog.FileName,Distribution.BundledJar(project,RuntimeJar())); Log("Exported bundled project JAR: " + dialog.FileName + ". KubeJS handlers require KubeJS/Rhino installed."); }
    void Validate() { SaveScriptText(); Output.Items.Clear(); var errors = Validation.Check(project); foreach (var error in errors) Output.Items.Add(error); if (errors.Count == 0) Log("Validation passed."); else Log($"{errors.Count} validation errors"); ShowDock("output"); }
    void RefreshAll() { refreshing = true; Screens.ItemsSource = project.Screens.Select(s => s.Id).ToList(); Screens.SelectedItem = ui.Id; refreshing = false; RefreshScripts(); Draw(); RefreshInspector(); Title = project.Manifest.Name + " — WYSICRAFT"; }
    void AddControl(string type, double x, double y) { Change(); var e = new UiElement { Type = type, Id = Unique(type), Value = type=="item_list"?"[]":"0", Text = Registry.Controls[type].DisplayName, Bounds = new() { X = Snap(x), Y = Snap(y), Width = type is "panel" or "scroll_panel" or "item_list" ? 180 : 100, Height = type is "panel" or "scroll_panel" or "item_list" ? 100 : 20 } }; ui.Elements.Add(e); selected.Clear(); selected.Add(e.Id); Draw(); RefreshInspector(); }
    string Unique(string basis) { string id = basis; int n = 1; while (ui.Elements.Any(e => e.Id == id)) id = basis + "_" + n++; return id; }
    double Snap(double n) => project.Manifest.Snap ? Math.Round(n / Math.Max(1, project.Manifest.GridSize)) * Math.Max(1, project.Manifest.GridSize) : Math.Round(n);
    void Draw()
    {
        Surface.Children.Clear(); Surface.Width = ui.Size.Width * Zoom; Surface.Height = ui.Size.Height * Zoom;
        if (grid)
        {
            int step = Math.Max(2, project.Manifest.GridSize) * (int)Zoom;
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(52, 58, 67)), null, new RectangleGeometry(new Rect(0, 0, step, step))));
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(90, 98, 110)), null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            Surface.Background = new DrawingBrush(drawing) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, step, step) };
        }
        else Surface.Background = new SolidColorBrush(Color.FromRgb(52, 58, 67));
        foreach (var e in ui.Elements)
        {
            // The preview child ignores mouse input. A transparent background makes
            // the entire designer wrapper hittable, including the control interior.
            var border = new Border { Background = Brushes.Transparent, Tag = e.Id, Width = e.Bounds.Width * Zoom, Height = e.Bounds.Height * Zoom, BorderThickness = new Thickness(selected.Contains(e.Id) ? 2 : 1), BorderBrush = selected.Contains(e.Id) ? Brushes.DeepSkyBlue : Brushes.DimGray, Opacity = e.Visible ? Math.Clamp(e.Opacity, .15, 1) : .25, Child = RenderControl(e, false, null), ToolTip = e.Id + " • " + e.Type };
            Canvas.SetLeft(border, e.Bounds.X * Zoom); Canvas.SetTop(border, e.Bounds.Y * Zoom); Surface.Children.Add(border);
            border.ContextMenu = ElementMenu(e);
            border.MouseLeftButtonDown += (_, args) => { SelectCanvasElement(e.Id, Keyboard.Modifiers); Change(); dragStart = args.GetPosition(Surface); dragBounds = ui.Elements.Where(x => selected.Contains(x.Id)).ToDictionary(x => x.Id, x => Json.Clone(x.Bounds)); Surface.CaptureMouse(); Surface.Focus(); Draw(); RefreshInspector(); args.Handled = true; };
            if (selected.Contains(e.Id))
            {
                var handle = new Thumb { Width = 9, Height = 9, Background = Brushes.DeepSkyBlue, Cursor = Cursors.SizeNWSE }; Canvas.SetLeft(handle, (e.Bounds.X + e.Bounds.Width) * Zoom - 5); Canvas.SetTop(handle, (e.Bounds.Y + e.Bounds.Height) * Zoom - 5); Panel.SetZIndex(handle, 1000); Surface.Children.Add(handle);
                handle.DragStarted += (_, _) => Change(); handle.DragDelta += (_, args) => { e.Bounds.Width = Math.Max(8, e.Bounds.Width + args.HorizontalChange / Zoom); e.Bounds.Height = Math.Max(8, e.Bounds.Height + args.VerticalChange / Zoom); border.Width = e.Bounds.Width * Zoom; border.Height = e.Bounds.Height * Zoom; Canvas.SetLeft(handle, (e.Bounds.X + e.Bounds.Width) * Zoom - 5); Canvas.SetTop(handle, (e.Bounds.Y + e.Bounds.Height) * Zoom - 5); }; handle.DragCompleted += (_, _) => { e.Bounds.Width = Math.Max(8, Snap(e.Bounds.Width)); e.Bounds.Height = Math.Max(8, Snap(e.Bounds.Height)); Draw(); RefreshInspector(); };
            }
        }
        RefreshLayers();
        Status.Text = $"{ui.Id}  |  {ui.Size.Width} × {ui.Size.Height}  |  {selected.Count} selected  |  Grid {project.Manifest.GridSize}  |  Snap {(project.Manifest.Snap ? "on" : "off")}";
    }
    void DragMove(object sender, MouseEventArgs args) { if (dragBounds == null || args.LeftButton != MouseButtonState.Pressed) return; var p = args.GetPosition(Surface); foreach (var e in ui.Elements.Where(e => dragBounds.ContainsKey(e.Id))) { e.Bounds.X = Math.Max(0, Snap(dragBounds[e.Id].X + (p.X - dragStart.X) / Zoom)); e.Bounds.Y = Math.Max(0, Snap(dragBounds[e.Id].Y + (p.Y - dragStart.Y) / Zoom)); } Draw(); }
    void Delete() { if (selected.Count == 0) return; Change(); ui.Elements.RemoveAll(e => selected.Contains(e.Id)); selected.Clear(); Draw(); RefreshInspector(); }
    void Copy() { var elements = ui.Elements.Where(e => selected.Contains(e.Id)).ToList(); if (elements.Count > 0) Clipboard.SetData("Wysicraft.Elements", Json.Write(elements)); }
    void Paste() { if (Clipboard.GetData("Wysicraft.Elements") is string json) InsertCopies(Json.Read<List<UiElement>>(json)); }
    void SelectCanvasElement(string id, ModifierKeys modifiers)
    {
        if(modifiers.HasFlag(ModifierKeys.Alt)) { selected.Clear();selected.Add(id);return; }
        string group=ui.Elements.First(e=>e.Id==id).LayerGroup;
        if(group.Length>0 && !modifiers.HasFlag(ModifierKeys.Alt)) {var members=ui.Elements.Where(e=>e.LayerGroup==group).Select(e=>e.Id).ToArray(); bool toggle=(modifiers & (ModifierKeys.Control|ModifierKeys.Shift))!=0; bool remove=toggle && members.All(selected.Contains); if(!toggle)selected.Clear(); foreach(var member in members) {if(remove)selected.Remove(member);else selected.Add(member);}return;}
        bool extend = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        if (!extend && !selected.Contains(id)) selected.Clear();
        if (extend && selected.Contains(id)) selected.Remove(id); else selected.Add(id);
    }
    void Keys(object sender, KeyEventArgs e)
    {
        if(e.Handled) return;
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.N or Key.O or Key.S) { Guard(e.Key==Key.N?NewProject:e.Key==Key.O?OpenProject:()=>Save(false)); e.Handled=true; return; }
        if (e.OriginalSource is TextBox or ComboBox) return;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        Action? action = ctrl ? e.Key switch { Key.G => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?UngroupSelected:GroupSelected, Key.S => () => Save(false), Key.C => Copy, Key.V => Paste, Key.X => () => { Copy(); Delete(); }, Key.Z => history.Undo, Key.Y => history.Redo, _ => null } : e.Key == Key.Delete ? Delete : null;
        if (action != null) { Guard(action); e.Handled = true; return; }
        if (selected.Count > 0 && e.Key is Key.Left or Key.Right or Key.Up or Key.Down) { Change(); int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1; foreach (var el in ui.Elements.Where(x => selected.Contains(x.Id))) { el.Bounds.X += e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0; el.Bounds.Y += e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0; } Draw(); RefreshInspector(); e.Handled = true; }
    }
    void Heading(StackPanel panel, string text) => panel.Children.Add(new TextBlock { Text = text.ToUpperInvariant(), Foreground = Brushes.LightSkyBlue, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 12, 4, 5) });
    void Field(StackPanel panel, string label, object obj, string name)
    {
        if (name is "Foreground" or "Background" or "BorderColor" or "ShadowColor") { ColorField(panel, label, obj, name); return; }
        var prop = obj.GetType().GetProperty(name)!; var row = new DockPanel(); row.Children.Add(new TextBlock { Text = label, Width = 108, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) });
        if (prop.PropertyType == typeof(bool)) { var check = new CheckBox { IsChecked = (bool)prop.GetValue(obj)! }; check.Click += (_, _) => { Change(); prop.SetValue(obj, check.IsChecked == true); Draw(); }; row.Children.Add(check); }
        else
        {
            var value = prop.GetValue(obj);
            var box = new TextBox { Text = value is List<string> list ? string.Join("|", list) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" };
            string old = box.Text ?? ""; bool checkpoint = false;
            void Apply(bool report)
            {
                string text = box.Text ?? ""; if (text == old) return;
                try
                {
                    object converted = prop.PropertyType == typeof(List<string>) ? text.Split('|').ToList() : Convert.ChangeType(text, prop.PropertyType, CultureInfo.InvariantCulture)!;
                    if (name == "Id" && (!Validation.Id(text) || obj is UiElement && ui.Elements.Any(e => e != obj && e.Id == text))) throw new Exception("ID must be unique and lowercase");
                    if (converted is double number && (!double.IsFinite(number) || Math.Abs(number) > 4096 || name is "Width" or "Height" && number < 1 || name == "Opacity" && (number < 0 || number > 1) || name == "FontScale" && (number <= 0 || number > 8))) throw new Exception("Value outside supported range");
                    if (obj is Wysicraft.Models.Size && converted is int size && size is < 16 or > 4096) throw new Exception("Canvas size must be 16–4096");
                    if (!checkpoint) { Change(); checkpoint = true; }
                    if (name == "Id" && obj is UiElement element) { selected.Remove(element.Id); selected.Add(text); }
                    prop.SetValue(obj, converted); old = text; box.BorderBrush = new SolidColorBrush(Color.FromRgb(69,75,86)); Draw();
                }
                catch (Exception ex) { box.BorderBrush = Brushes.IndianRed; if (report) { Log(ex.Message); box.Text = old; } }
            }
            box.TextChanged += (_, _) => Apply(false);
            box.LostKeyboardFocus += (_, _) => { Apply(true); checkpoint = false; };
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Apply(true); checkpoint = false; e.Handled = true; } };
            row.Children.Add(box);
        }
        panel.Children.Add(row);
    }
    void RefreshInspector()
    {
        Properties.Children.Clear(); Events.Children.Clear(); var e = ui.Elements.FirstOrDefault(e => selected.Contains(e.Id));
        if (e == null) { Heading(Properties, "Screen"); Field(Properties, "Title", ui, "Title"); Field(Properties, "Width", ui.Size, "Width"); Field(Properties, "Height", ui.Size, "Height"); Field(Properties,"Show frame / title",ui,"ShowFrame"); Field(Properties,"Dim game behind UI",ui,"DimBackground"); Properties.Children.Add(new TextBlock { Text="For a transparent screen, turn both off. Add a Panel element wherever you want a background.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4) }); BuildEvents(ui.Events, ["open", "close"]); return; }
        Heading(Properties, "Identity"); foreach (string p in new[] { "Id", "Name" }) Field(Properties, p, e, p);
        Heading(Properties, "Layout"); foreach (string p in new[] { "X", "Y", "Width", "Height" }) Field(Properties, p, e.Bounds, p); Field(Properties, "Parent ID", e, "Parent");
        BuildAppearance(e);
        Heading(Properties, "Behavior"); foreach (string p in new[] { "Visible", "Enabled", "Tooltip", "VisibleIf", "EnabledIf" }) Field(Properties, p, e, p);
        if (Registry.Controls.TryGetValue(e.Type, out var spec)) { Heading(Properties, "Control / Minecraft"); foreach (string p in spec.Properties) Field(Properties, p, e, p); BuildEvents(e.Events, spec.Events); }
    }
    void BuildEvents(Dictionary<string, UiEvent> events, string[] names)
    {
        Heading(Events, "Event actions");
        var pick = new ComboBox { ItemsSource = names, SelectedIndex = 0 }; var side = new ComboBox { ItemsSource = new[] { "Client", "Server" }, SelectedIndex = 0 }; Events.Children.Add(pick); Events.Children.Add(side); var content = new StackPanel(); Events.Children.Add(content);
        void Populate()
        {
            content.Children.Clear(); string name = (string)pick.SelectedItem; bool server = side.SelectedIndex == 1;
            var h = events.TryGetValue(name, out var ev) ? server ? ev.Server : ev.Client : null;
            content.Children.Add(new TextBlock { Text = server ? "Trusted server actions. Preview simulates these." : "Runs locally for this screen.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
            Wysicraft.Models.EventHandler Ensure() { if (!events.TryGetValue(name, out var value)) events[name] = value = new(); return server ? value.Server : value.Client; }
            if(server) {
                var permission=new ComboBox { ItemsSource=new[]{"0 — Everyone","1 — Moderator","2 — Operator","3 — Administrator","4 — Owner"},SelectedIndex=h?.PermissionLevel ?? 0,Margin=new Thickness(4) };
                permission.SelectionChanged+=(_,_)=>{ Change(); Ensure().PermissionLevel=permission.SelectedIndex; }; content.Children.Add(permission);
                var cooldown=new TextBox { Text=(h?.CooldownTicks ?? 4).ToString(),Margin=new Thickness(4),ToolTip="Server cooldown in ticks (20 ticks = one second). Shared across reopening this screen. Continuous text/value changes are exempt." };
                content.Children.Add(new TextBlock {Text="Minimum interval (ticks)",Margin=new Thickness(4)}); content.Children.Add(cooldown);
                cooldown.LostKeyboardFocus+=(_,_)=>{ if(int.TryParse(cooldown.Text,out int ticks) && ticks>=0 && ticks<=1200) { Change(); Ensure().CooldownTicks=ticks; } else cooldown.Text=(h?.CooldownTicks ?? 4).ToString(); };
            }
            if (h != null) foreach (var action in h.Actions.ToList())
            {
                var block = new StackPanel { Margin = new Thickness(2, 8, 2, 4) }; var type = new ComboBox { ItemsSource = (server ? Registry.ServerActions : Registry.ClientActions).Order().ToArray(), SelectedItem = action.Type }; type.SelectionChanged += (_, _) => { if (type.SelectedItem is string t) { Change(); action.Type = t; } }; block.Children.Add(type); Field(block, "Target / Variable", action, "Target"); Field(block, "Value / Command", action, "Value"); var buttons = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var (label, delta) in new[] { ("↑", -1), ("↓", 1), ("Remove", 0) }) { var button = new Button { Content = label }; button.Click += (_, _) => { Change(); int index = h.Actions.IndexOf(action); if (delta == 0) h.Actions.Remove(action); else { int target = Math.Clamp(index + delta, 0, h.Actions.Count - 1); h.Actions.RemoveAt(index); h.Actions.Insert(target, action); } Populate(); }; buttons.Children.Add(button); } block.Children.Add(buttons); content.Children.Add(block);
            }
            var add = new Button { Content = "+ Add action" }; add.Click += (_, _) => { Change(); Ensure().Actions.Add(new() { Type = server ? "command" : "set_text" }); Populate(); }; content.Children.Add(add);
            BuildEventScripts(content, events, name, server, Populate);
        }
        pick.SelectionChanged += (_, _) => Populate(); side.SelectionChanged += (_, _) => Populate(); Populate();
    }
    public static string? Prompt(string title, string label, string initial)
    {
        var window = new Window { Title = title, Width = 480, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow, ResizeMode = ResizeMode.NoResize }; var panel = new StackPanel { Margin = new Thickness(12) }; var box = new TextBox { Text = initial }; var ok = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right }; ok.Click += (_, _) => window.DialogResult = true; panel.Children.Add(new TextBlock { Text = label }); panel.Children.Add(box); panel.Children.Add(ok); window.Content = panel; box.SelectAll(); box.Focus(); return window.ShowDialog() == true ? box.Text : null;
    }
    void Settings()
    {
        var clone = Json.Clone(project.Manifest); var panel = new StackPanel { Margin = new Thickness(12) }; foreach (string p in new[] { "Name", "Id", "Author", "Version", "RuntimeVersion", "GridSize", "Snap", "Dependencies" }) Field(panel, p, clone, p);
        var window = new Window { Title = "Project settings", Width = 490, Height = 470, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel }; var save = new Button { Content = "Apply", IsDefault = true }; save.Click += (_, _) => { if (!Validation.Id(clone.Id) || !Validation.Version(clone.Version) || !Validation.Version(clone.RuntimeVersion) || clone.GridSize is < 1 or > 128) { MessageBox.Show("Check ID, versions and grid size (1–128)."); return; } Change(); project.Manifest = clone; window.Close(); RefreshAll(); }; panel.Children.Add(save); window.ShowDialog();
    }
    void EditScreen()
    {
        var window = new Window { Title = "Screen settings", Width = 480, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(12) };
        var id = new TextBox { Text = ui.Id };
        var main = new CheckBox { Content = "Main screen", IsChecked = project.Manifest.DefaultUi == ui.Id, Margin = new Thickness(4, 12, 4, 12) };
        // A project always has one main screen; select another screen to replace it.
        if (main.IsChecked == true) main.IsEnabled = false;
        var variables = new TextBox { Text = string.Join(";", ui.Variables.Select(v => v.Key + "=" + v.Value)) };
        var frame = new CheckBox { Content="Show Minecraft screen frame",IsChecked=ui.ShowFrame };
        var dim = new CheckBox { Content="Dim game behind UI",IsChecked=ui.DimBackground };
        var fit = new CheckBox { Content="Fit to Minecraft viewport",IsChecked=ui.FitToScreen };
        panel.Children.Add(new TextBlock { Text = "Screen ID" }); panel.Children.Add(id); panel.Children.Add(main);
        panel.Children.Add(new TextBlock { Text = $"/{project.Manifest.Id}.open opens the Main screen. Selecting Main here replaces the previous Main screen.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
        panel.Children.Add(new TextBlock { Text = "Local variables (name=value;name=value)" }); panel.Children.Add(variables);
        panel.Children.Add(frame); panel.Children.Add(dim); panel.Children.Add(fit);
        var save = new Button { Content = "Save", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => Guard(() => {
            if (!Validation.Id(id.Text) || project.Screens.Any(s => s != ui && s.Id == id.Text)) throw new Exception("Use a unique lowercase screen ID");
            var parsed = variables.Text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Split('=', 2)).ToDictionary(v => v[0], v => v.Length > 1 ? v[1] : "");
            if (parsed.Keys.Any(k => !Validation.Variable(k))) throw new Exception("Invalid variable name");
            Change(); string oldId = ui.Id;
            if (main.IsChecked == true) project.Manifest.DefaultUi = id.Text;
            foreach (var screen in project.Screens)
                foreach (var ev in screen.Events.Values.Concat(screen.Elements.SelectMany(e => e.Events.Values)))
                    foreach (var action in ev.Client.Actions.Concat(ev.Server.Actions))
                        if (action.Type == "open_ui" && action.Value == oldId) action.Value = id.Text;
            ui.Id = id.Text; ui.Variables = parsed; ui.ShowFrame=frame.IsChecked==true; ui.DimBackground=dim.IsChecked==true; ui.FitToScreen=fit.IsChecked==true; RefreshAll(); window.DialogResult = true;
        });
        panel.Children.Add(save); window.Content = panel; window.ShowDialog();
    }
    void ImportTexture() { var dialog = new OpenFileDialog { Filter = "PNG textures|*.png" }; if (dialog.ShowDialog() != true) return; string name = System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(dialog.FileName).ToLowerInvariant(), "[^a-z0-9_-]", "_") + ".png"; var bytes = File.ReadAllBytes(dialog.FileName); ProjectStore.TextureSize(bytes); Change(); project.Assets[TextureAssets.Path(project.Manifest.Id, name)] = bytes; Log("Imported texture. Resource: " + TextureAssets.Resource(project.Manifest.Id, name)); }
    void ValidateScriptPath(string path) { Validation.SafePath(path); if (!(path.StartsWith("scripts/client/") || path.StartsWith("scripts/server/")) || !path.EndsWith(".js")) throw new Exception("Use scripts/client/name.js or scripts/server/name.js"); }
    void SaveScriptText() { if (editingScript != null && project.Scripts.ContainsKey(editingScript) && project.Scripts[editingScript] != ScriptEditor.Text) { history.Checkpoint(); project.Scripts[editingScript] = ScriptEditor.Text; dirty = true; } }
    void RefreshScripts(string? pick = null) { refreshing = true; ScriptFiles.ItemsSource = project.Scripts.Keys.Concat(ScriptTemplate.All.Select(t=>t.Title)).ToList(); ScriptFiles.SelectedItem = pick ?? editingScript; refreshing = false; editingScript = ScriptFiles.SelectedItem as string; ScriptEditor.Text = editingScript != null ? project.Scripts[editingScript] : ""; }
    static Brush Brush(string value) { try { return (Brush)new BrushConverter().ConvertFromString(value)!; } catch { return Brushes.Magenta; } }
    FrameworkElement RenderControl(UiElement e, bool interactive, Action<string>? fire)
    {
        FrameworkElement widget;
        switch (e.Type)
        {
            case "button": var button = new Button { Content = StyledText(e), Template = SkinTemplate(e), Background = SkinBrush(e), Margin = new Thickness(0), Padding = new Thickness(2) }; button.Click += (_, _) => fire?.Invoke("click"); widget = button; break;
            case "textbox": var input = new TextBox { Text = e.Value, Margin = new Thickness(0) }; input.TextChanged += (_, _) => { e.Value = input.Text; fire?.Invoke("text_changed"); }; input.KeyDown += (_, args) => { if (args.Key == Key.Enter) fire?.Invoke("submit"); }; widget = input; break;
            case "checkbox": var check = new CheckBox { Content = e.Text, IsChecked = e.Value == "true", VerticalAlignment = VerticalAlignment.Center }; check.Click += (_, _) => { e.Value = check.IsChecked == true ? "true" : "false"; fire?.Invoke(check.IsChecked == true ? "checked" : "unchecked"); }; widget = check; break;
            case "slider": var slider = new Slider { Minimum = e.Minimum, Maximum = Math.Max(e.Minimum + 1, e.Maximum), Value = double.TryParse(e.Value, out var v) ? v : 0 }; slider.ValueChanged += (_, _) => { e.Value = slider.Value.ToString(CultureInfo.InvariantCulture); fire?.Invoke("value_changed"); }; widget = slider; break;
            case "progress": widget = new ProgressBar { Minimum = e.Minimum, Maximum = Math.Max(e.Minimum + 1, e.Maximum), Value = double.TryParse(e.Value, out var p) ? p : 0 }; break;
            case "dropdown": var combo = new ComboBox { ItemsSource = e.Options, SelectedIndex = int.TryParse(e.Value, out var i) ? Math.Clamp(i, 0, Math.Max(0, e.Options.Count - 1)) : 0 }; combo.SelectionChanged += (_, _) => { e.Value = combo.SelectedIndex.ToString(); fire?.Invoke("value_changed"); }; widget = combo; break;
            case "image": case "texture_region":
                if (TextureAssets.TryGet(project, e.Texture, out var bytes)) { var bitmap = DecodeTexture(bytes); widget = new Image { Source = bitmap, Stretch = Stretch.Fill }; }
                else widget = new TextBlock { Text = "▧ " + e.Texture, Foreground = Brushes.LightGray }; break;
            case "panel": case "scroll_panel": widget = new Grid(); break;
            case "item_list":
                var list=new ListBox { Background=Brush(e.Background),BorderThickness=new Thickness(0) };
                FillItemList(list,e.Value,e,fire);
                list.SelectionChanged+=(_,_)=>{ if(list.SelectedIndex>=0) { e.Text=list.SelectedIndex.ToString(); fire?.Invoke("item_click"); } }; widget=list; break;
            default: widget = new TextBlock { Text = e.Type == "item" ? "◇ " + e.Item : e.Text, Foreground = Brush(e.Foreground), Background = e.Type == "label" ? Brushes.Transparent : Brush(e.Background), VerticalAlignment = VerticalAlignment.Center, FontSize = Math.Clamp(e.FontScale * 14, 6, 96), TextAlignment = e.Alignment == "center" ? TextAlignment.Center : e.Alignment == "right" ? TextAlignment.Right : TextAlignment.Left }; break;
        }
        ApplyFont(widget, e);
        if (e.Type != "button") widget = DecorateControl(widget, e);
        widget.IsHitTestVisible = interactive; widget.IsEnabled = e.Enabled; widget.ToolTip = e.Tooltip; return widget;
    }
}
