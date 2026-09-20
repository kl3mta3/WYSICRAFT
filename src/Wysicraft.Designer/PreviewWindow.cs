using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wysicraft.Models;
using Wysicraft.Core;
using Element = Wysicraft.Models.Element;
using Handler = Wysicraft.Models.EventHandler;
using Validation = Wysicraft.Core.Validation;
namespace Wysicraft.Designer;

public partial class MainWindow
{
    void Preview()
    {
        SaveScriptText(); if (Validation.Check(project).Count > 0) { Validate(); return; }
        activePreview?.Window.Close(); activePreview=new PreviewSession(this,Json.Clone(project),ui.Id);previewRevision=Revision();activePreview.Window.Show();
    }
    sealed class PreviewSession
    {
        internal async Task WaitReady() { while(busy || pending.Count>0) await Task.Delay(20); }
        internal string Snapshot() => Json.Write(new { screen=screen.Id,variables=state,elements=screen.Elements,logs=output.Text.Length>12000?output.Text[^12000..]:output.Text });
        internal async Task RunMcpEvent(string id,string eventName,string value) {
            await WaitReady();
            var events=id.Length==0?screen.Events:screen.Elements.Single(e=>e.Id==id).Events;
            if(!events.ContainsKey(eventName))throw new InvalidOperationException("No assigned event: "+id+"."+eventName);
            if(id.Length>0) {
                var target=screen.Elements.Single(e=>e.Id==id);
                if(target.Type=="checkbox" && eventName is "checked" or "unchecked") target.Value=value=eventName=="checked"?"true":"false";
                else if(eventName=="value_changed") {
                    if(target.Type=="slider" && (!double.TryParse(value,CultureInfo.InvariantCulture,out double number) || !double.IsFinite(number) || number<target.Minimum || number>target.Maximum))throw new InvalidOperationException("Slider value is outside its range.");
                    if(target.Type=="dropdown" && (!int.TryParse(value,out int index) || index<0 || index>=target.Options.Count))throw new InvalidOperationException("Dropdown index is outside its options.");
                    target.Value=value;
                }
                else if(target.Type=="textbox" && eventName is "text_changed" or "submit") target.Value=value;
                Refresh();
            }
            Enqueue(id,eventName,value);await WaitReady();
        }
        internal void CaptureCanvas(string path) {
            Window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)canvas.Width,(int)canvas.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);
            var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=System.IO.File.Create(path);png.Save(stream);
        }
        readonly MainWindow designer;
        readonly Project project;
        readonly string initialUi;
        UiDefinition screen;
        Dictionary<string, string> state;
        readonly Canvas canvas = new() { Background = new SolidColorBrush(Color.FromRgb(36, 40, 48)) };
        readonly TextBox output = new() { IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap };
        readonly TextBox code = new() { AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = "console.log('Hello from the preview!');\n// ui.setText('status', 'It works!');" };
        readonly Dictionary<string, (FrameworkElement Control, Element Display)> controls = [];
        readonly Dictionary<string, double> scrollOffsets = [];
        readonly Queue<(string Element, string Event, string Value)> pending = new();
        bool syncing, busy, closed; int navigationDepth;
        Task? closing;
        public Window Window { get; }
        public PreviewSession(MainWindow designer, Project project, string id)
        {
            this.designer = designer; this.project = project; initialUi = id; screen = Json.Clone(project.Screens.First(s => s.Id == id)); state = new(screen.Variables);
            Window = new Window { Title = "WYSICRAFT • Interactive Preview", Owner = designer, Width = Math.Max(760, screen.Size.Width * Zoom + 40), Height = Math.Max(650, screen.Size.Height * Zoom + 320), Background = new SolidColorBrush(Color.FromRgb(29, 32, 37)), Foreground = Brushes.White, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var layout = new DockPanel(); Window.Content = layout;
            var tools = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(tools, Dock.Top); layout.Children.Add(tools);
            var reset = new Button { Content = "Reset preview" }; reset.Click += (_, _) => { if (busy) return; Open(initialUi); }; tools.Children.Add(reset);
            var clear = new Button { Content = "Clear console" }; clear.Click += (_, _) => output.Clear(); tools.Children.Add(clear);
            tools.Children.Add(new TextBlock { Text = "Click controls to test • Server operations are simulated", Margin = new Thickness(12, 6, 4, 6), VerticalAlignment = VerticalAlignment.Center });
            var bottom = new Grid { Height = 235 }; bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition()); DockPanel.SetDock(bottom, Dock.Bottom); layout.Children.Add(bottom);
            var consolePanel = new DockPanel(); consolePanel.Children.Add(Header("CONSOLE • clicks, actions and script output")); consolePanel.Children.Add(output); bottom.Children.Add(consolePanel);
            var scriptPanel = new DockPanel(); Grid.SetColumn(scriptPanel, 1); bottom.Children.Add(scriptPanel); scriptPanel.Children.Add(Header("JAVASCRIPT • preview scratchpad"));
            var run = new Button { Content = "Run JavaScript", HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(run, Dock.Bottom); scriptPanel.Children.Add(run); scriptPanel.Children.Add(code);
            run.Click += async (_, _) => { if (busy) { Print("BUSY", "Wait for the current event."); return; } busy = true; try { await Script(code.Text, "", "", "", false); } finally { busy = false; Pump(); } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = canvas }; layout.Children.Add(scroll);
            canvas.PreviewMouseWheel += (_, args) => {
                var point = args.GetPosition(canvas);
                bool Hit(Element e) => point.X>=e.Bounds.X*Zoom && point.X<(e.Bounds.X+e.Bounds.Width)*Zoom && point.Y>=ContainerTree.Top(screen,e,scrollOffsets)*Zoom && point.Y<(ContainerTree.Top(screen,e,scrollOffsets)+e.Bounds.Height)*Zoom;
                var panels=screen.Elements.AsEnumerable().Reverse().Where(e=>e.Type is "scroll_panel" or "item_list" && e.Visible && e.Enabled && Expressions.Evaluate(e.VisibleIf,state) && Expressions.Evaluate(e.EnabledIf,state) && Hit(e) && ContainerTree.Ancestors(screen,e).All(p=>p.Visible && p.Enabled && Expressions.Evaluate(p.VisibleIf,state) && Expressions.Evaluate(p.EnabledIf,state) && Hit(p))).OrderByDescending(e=>ContainerTree.Ancestors(screen,e).Count());
                foreach(var panel in panels) {
                    if(panel.Type=="item_list" && controls.TryGetValue(panel.Id,out var pair)) {
                        var queue=new Queue<DependencyObject>();queue.Enqueue(pair.Control);ScrollViewer? viewer=null;
                        while(queue.Count>0) {var node=queue.Dequeue();if(node is ScrollViewer found){viewer=found;break;}for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)queue.Enqueue(VisualTreeHelper.GetChild(node,i));}
                        if(viewer==null)continue;double next=Math.Clamp(viewer.VerticalOffset-args.Delta/120d*24*Zoom,0,viewer.ScrollableHeight);
                        if(next==viewer.VerticalOffset)continue;viewer.ScrollToVerticalOffset(next);args.Handled=true;return;
                    }
                    double bottom=screen.Elements.Where(e=>e.Parent==panel.Id && e.Visible).Select(e=>e.Bounds.Y+e.Bounds.Height).DefaultIfEmpty(panel.Bounds.Y+panel.Bounds.Height).Max();
                    double offset=Math.Clamp(scrollOffsets.GetValueOrDefault(panel.Id)-args.Delta/120d*20,0,Math.Max(0,bottom-panel.Bounds.Y-panel.Bounds.Height));
                    if(offset==scrollOffsets.GetValueOrDefault(panel.Id))continue;scrollOffsets[panel.Id]=offset;args.Handled=true;Refresh();return;
                }
            };
            Window.Closing += (_, args) => { if (!closed) { args.Cancel = true; closing ??= CloseAsync(); } };
            Window.Closed += (_, _) => { closed = true; pending.Clear(); };
            Render(); Print("READY", "Buttons are live. Every click is logged, even without an assigned action.");
            Print("SCRIPTS", "Standard scripts run in the bundled Minecraft engine. Server operations here are simulated.");
            Enqueue("", "open", "");
        }
        static TextBlock Header(string title) { var label = new TextBlock { Text = title, Margin = new Thickness(6), Foreground = Brushes.LightSkyBlue, FontSize = 11 }; DockPanel.SetDock(label, Dock.Top); return label; }
        void Print(string category, string text)
        {
            if (output.Text.Length > 100000) output.Text = output.Text[^50000..];
            output.AppendText($"[{category}] {text}\n"); output.ScrollToEnd();
        }
        void Render()
        {
            syncing = true; controls.Clear(); canvas.Children.Clear(); canvas.Width = screen.Size.Width * Zoom; canvas.Height = screen.Size.Height * Zoom;
            foreach (var element in screen.Elements)
            {
                var display = Json.Clone(element); display.Text = Expressions.Bind(element.Text, state);
                var widget = designer.RenderControl(display, true, name => { if (syncing || closed) return; element.Value = display.Value; Enqueue(element.Id, name, element.Type=="item_list"?display.Text:display.Value); });
                widget.Width = element.Bounds.Width * Zoom; widget.Height = element.Bounds.Height * Zoom;
                Canvas.SetLeft(widget, element.Bounds.X * Zoom); Canvas.SetTop(widget, element.Bounds.Y * Zoom); canvas.Children.Add(widget); controls[element.Id] = (widget, display);
                // Decorative layers should not intercept a button beneath them.
                if (element.Type is "panel" or "scroll_panel" or "image" or "texture_region" or "label" or "item" or "progress") widget.IsHitTestVisible = false;
                else
                {
                    widget.MouseEnter += (_, _) => { if (!syncing && element.Events.ContainsKey("mouse_enter")) Enqueue(element.Id, "mouse_enter", ""); };
                    widget.MouseLeave += (_, _) => { if (!syncing && element.Events.ContainsKey("mouse_leave")) Enqueue(element.Id, "mouse_leave", ""); };
                }
            }
            syncing = false; Refresh();
        }
        void Refresh()
        {
            syncing = true;
            try
            {
                foreach (var element in screen.Elements)
                {
                    if (!controls.TryGetValue(element.Id, out var pair)) continue;
                    var widget = pair.Control; var display = pair.Display;
                    display.Text = Expressions.Bind(element.Text, state); display.Value = element.Value; display.Texture = element.Texture; display.Item = element.Item;
                    var parents = ContainerTree.Ancestors(screen,element).ToArray();
                    double top = ContainerTree.Top(screen,element,scrollOffsets);
                    Canvas.SetTop(widget, top * Zoom);
                    {
                        var bounds = new Rect(element.Bounds.X * Zoom, top * Zoom, element.Bounds.Width * Zoom, element.Bounds.Height * Zoom);
                        foreach(var parent in parents) bounds.Intersect(new Rect(parent.Bounds.X * Zoom, ContainerTree.Top(screen,parent,scrollOffsets) * Zoom, parent.Bounds.Width * Zoom, parent.Bounds.Height * Zoom));
                        widget.Clip = new RectangleGeometry(bounds.IsEmpty ? new Rect(0, 0, 0, 0) : new Rect(bounds.X - element.Bounds.X * Zoom, bounds.Y - top * Zoom, bounds.Width, bounds.Height));
                    }
                    widget.Visibility = element.Visible && Expressions.Evaluate(element.VisibleIf, state) && parents.All(parent=>parent.Visible && Expressions.Evaluate(parent.VisibleIf,state)) ? Visibility.Visible : Visibility.Collapsed;
                    widget.IsEnabled = element.Enabled && Expressions.Evaluate(element.EnabledIf, state) && parents.All(parent=>parent.Enabled && Expressions.Evaluate(parent.EnabledIf,state)); widget.Opacity = element.Opacity;
                    if (widget is Border frame && Equals(frame.Tag, "appearance")) { frame.Background = designer.FrameBrush(display); widget = (FrameworkElement)frame.Child; }
                    switch (widget)
                    {
                        case Button button: button.Content = designer.StyledText(display); button.Template = designer.SkinTemplate(display); break;
                        case TextBlock label: label.Text = element.Type == "item" ? "◆ " + display.Item.Split(':').Last() : display.Text; break;
                        case ListBox list: if(!Equals(list.Tag,ItemListStamp(element.Value,element,state))) designer.FillItemList(list,element.Value,element,name=>Enqueue(element.Id,name,element.Text),state); break;
                        case TextBox text: if (text.Text != element.Value) text.Text = element.Value; break;
                        case CheckBox check: check.Content = display.Text; check.IsChecked = element.Value == "true"; break;
                        case Slider slider: if (double.TryParse(element.Value, CultureInfo.InvariantCulture, out double value)) slider.Value = value; break;
                        case ProgressBar progress: if (double.TryParse(element.Value, CultureInfo.InvariantCulture, out double progressValue)) progress.Value = progressValue; break;
                        case ComboBox combo: if (int.TryParse(element.Value, out int index)) combo.SelectedIndex = index; break;
                        case Border border: border.Background = designer.SkinBrush(display); break;
                        case Image image: if (TextureAssets.TryGet(project, element.Texture, out var bytes)) image.Source = DecodeTexture(bytes); break;
                    }
                }
            }
            finally { syncing = false; }
        }
        void Enqueue(string element, string name, string value)
        {
            if (closed) return;
            if (pending.Count >= 32) { Print("WARNING", "Too many queued preview events"); return; }
            pending.Enqueue((element, name, value)); Pump();
        }
        internal void TriggerTest(string element, string eventName)
        {
            string value = screen.Elements.FirstOrDefault(e => e.Id == element)?.Value ?? "";
            if (eventName == "checked") value = "true"; if (eventName == "unchecked") value = "false";
            Print("TEST", (element == "" ? screen.Id : element) + "." + eventName);
            Enqueue(element, eventName, value);
        }
        async void Pump()
        {
            if (busy || closed) return; busy = true;
            try
            {
                while (pending.Count > 0 && !closed)
                {
                    var input = pending.Dequeue(); if (input.Element.Length > 0) navigationDepth = 0;
                    var events = input.Element == "" ? screen.Events : screen.Elements.FirstOrDefault(e => e.Id == input.Element)?.Events;
                    Print("EVENT", screen.Id + "." + (input.Element == "" ? "" : input.Element + ".") + input.Event);
                    if (events == null || !events.TryGetValue(input.Event, out var ev)) { Print("INFO", "No actions or script assigned to this event."); continue; }
                    string screenId = screen.Id;
                    foreach (var action in ev.Client.Actions) { Action(action); if (closed || screen.Id != screenId) break; }
                    if (closed || screen.Id != screenId) continue;
                    await AssignedScript(ev.Client, input.Element, input.Value, false);
                    foreach (var action in ev.Server.Actions) Print("SIMULATED SERVER", action.Type + " " + action.Target + " " + action.Value);
                    await AssignedScript(ev.Server, input.Element, input.Value, true);
                }
            }
            catch (Exception ex) { Print("ERROR", ex.Message); }
            finally { busy = false; }
        }
        async Task AssignedScript(Handler handler, string element, string value, bool server)
        {
            if (handler.Script.Length == 0 || closed) return;
            if (handler.ScriptEngine == "kubejs") { Print("KUBEJS", "Test this script in Minecraft using Export for KubeJS: " + handler.Script); return; }
            if (!project.Scripts.TryGetValue(handler.Script, out string? source)) { Print("ERROR", "Missing script " + handler.Script); return; }
            Print(server ? "SIMULATED SERVER SCRIPT" : "SCRIPT", handler.Script + " → " + handler.Function);
            await Script(source, handler.Function, element, value, server,handler.Script);
        }
        async Task Script(string source, string function, string element, string value, bool server,string sourceName="preview.js")
        {
            var result = await PreviewScripts.RunAsync(new() { Source = source,SourceName=sourceName,Server=server, Function = function, Element = element, Value = value, Variables = new(state), Texts = screen.Elements.ToDictionary(e => e.Id, e => Expressions.Bind(e.Text, state)) });
            if (closed) return;
            if (result.Error.Length > 0) Print("SCRIPT ERROR", result.Error);
            foreach (var action in result.Actions)
            {
                if (server && !action.Type.StartsWith("console_")) Print("SIMULATED SERVER", action.Type + " " + action.Target + " " + action.Value);
                else Action(action);
                if (closed) break;
            }
        }
        void Action(VisualAction action)
        {
            string value = Expressions.Bind(action.Value, state); var target = screen.Elements.FirstOrDefault(e => e.Id == action.Target);
            switch (action.Type)
            {
                case "set_text": if (target != null) target.Text = value; break;
                case "set_visible": if (target != null) target.Visible = value == "true"; break;
                case "set_enabled": if (target != null) target.Enabled = value == "true"; break;
                case "set_value": if (target != null) { if(target.Type=="item_list") { try { ItemRows.Parse(value); } catch(Exception ex) { Print("ERROR",ex.Message); return; } } target.Value = value; } break;
                case "set_item":
                    if (target?.Type != "item" || !Wysicraft.Core.Validation.Resource(value)) { Print("ERROR", "setItem requires an item element and namespaced item ID: " + action.Target + " = " + value); return; }
                    target.Item = value; break;
                case "change_texture": if (target != null) target.Texture = value; break;
                case "set_variable": state[action.Target] = value; break;
                case "toggle_variable": state[action.Target] = state.GetValueOrDefault(action.Target) == "true" ? "false" : "true"; break;
                case "message": case "console_log": Print("LOG", value); return;
                case "console_warn": Print("WARNING", value); return;
                case "console_error": Print("ERROR", value); return;
                case "simulated_command": case "simulated_message": Print("SIMULATED SERVER", value); return;
                case "play_sound": System.Media.SystemSounds.Beep.Play(); Print("SOUND", value); return;
                case "open_ui": Open(value); return;
                case "close_ui": Window.Close(); return;
                default: Print("ERROR", "Unsupported preview action " + action.Type); return;
            }
            Print("ACTION", action.Type + " " + action.Target + " = " + value); Refresh();
        }
        void Open(string id)
        {
            if (closing != null) return;
            var definition = project.Screens.FirstOrDefault(s => s.Id == id); if (definition == null) { Print("ERROR", "Unknown UI " + id); return; }
            if (++navigationDepth > 16) { pending.Clear(); Print("ERROR", "Preview navigation limit reached; reset the preview to continue."); navigationDepth = 0; return; }
            screen = Json.Clone(definition); state = new(screen.Variables); pending.Clear(); scrollOffsets.Clear(); Render(); Enqueue("", "open", "");
        }
        internal async Task CloseAsync()
        {
            // Yield until WPF has returned from its Closing event before closing again.
            await Task.Yield();
            Enqueue("", "close", "");
            while (busy && !closed) await Task.Delay(20);
            closed = true; Window.Close();
        }
        internal async Task VerifyClickAsync()
        {
            while (busy) await Task.Delay(20);
            var button = screen.Elements.First(e => e.Type == "button");
            button.Events["click"] = new UiEvent { Client = new Handler { Script = "scripts/client/preview_test.js", Function = "clicked" } };
            project.Scripts["scripts/client/preview_test.js"] = "function clicked(ctx) { console.log('Clicked!', ctx.elementId); ui.setText('status', 'Script ran'); }";
            ((Button)controls[button.Id].Control).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            while (busy) await Task.Delay(20);
            if (!output.Text.Contains("Clicked!") || screen.Elements.First(e => e.Id == "status").Text != "Script ran") throw new InvalidOperationException("Preview button did not execute its assigned JavaScript:\n" + output.Text);
            var result = await PreviewScripts.RunAsync(new() { Source = "while (true) {}" });
            if (result.Error.Length == 0) throw new InvalidOperationException("Infinite script was not stopped");
            result = await PreviewScripts.RunAsync(new() { Source = "console.log(typeof System, typeof require, typeof fetch);" });
            if (!result.Actions.Any(a => a.Value == "undefined undefined undefined")) throw new InvalidOperationException("Unexpected script host API exposure");
        }
        internal async Task VerifyEventTestAsync(string element, string eventName, string expected)
        {
            while (busy) await Task.Delay(20);
            TriggerTest(element, eventName);
            while (busy) await Task.Delay(20);
            if (!output.Text.Contains(expected)) throw new InvalidOperationException("Assigned event test failed:\n" + output.Text);
        }
    }
    internal async Task VerifyPreviewAsync(string capture)
    {
        var preview = new PreviewSession(this, Json.Clone(project), ui.Id);
        try
        {
            preview.Window.Show(); await preview.VerifyClickAsync(); preview.Window.UpdateLayout();
            var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)preview.Window.ActualWidth, (int)preview.Window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(preview.Window); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(capture); png.Save(stream);
        }
        finally { await preview.CloseAsync(); }
    }
}



