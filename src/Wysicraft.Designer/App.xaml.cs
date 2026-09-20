using System.Windows;
namespace Wysicraft.Designer;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 1 && e.Args[0] == "--script-host") { Shutdown(PreviewScripts.RunHost()); return; }
        var icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Wysicraft.ico"));
        icon.Freeze();
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => { if (sender is Window window && window.Icon == null) window.Icon = icon; }));
        var window = new MainWindow(); MainWindow = window;
        DispatcherUnhandledException+=(_,args)=>{window.CaptureCrash(args.Exception);MessageBox.Show("WYSICRAFT encountered an unexpected error and will close. Any recovery draft is available from File → Recover unsaved project on the next launch.\n\n"+args.Exception.Message,"WYSICRAFT");args.Handled=true;Shutdown(1);};
        if (e.Args.Length == 3 && e.Args[0] is "--smoke-preview" or "--smoke-events" or "--smoke-mcp" or "--smoke-connection" or "--smoke-docking" or "--smoke-layers" or "--smoke-nesting" or "--smoke-recovery" or "--smoke-arrange")
        {
            window.LoadForSmoke(e.Args[1]); window.Show();
            if (e.Args[0] is "--smoke-mcp" or "--smoke-connection") window.Hide();
            window.Dispatcher.InvokeAsync(async () =>
            {
                try { if(e.Args[0]=="--smoke-arrange") window.VerifyArrangement(e.Args[2]); else if(e.Args[0]=="--smoke-recovery") window.VerifyRecovery(e.Args[2]); else if(e.Args[0]=="--smoke-nesting") await window.VerifyNesting(e.Args[2]); else if(e.Args[0]=="--smoke-layers") window.VerifyLayerEditing(e.Args[2]); else if(e.Args[0]=="--smoke-docking") window.VerifyDocking(e.Args[2]); else if (e.Args[0] is "--smoke-mcp" or "--smoke-connection") await window.VerifyMcpAsync(e.Args[2],e.Args[0]=="--smoke-connection"); else if (e.Args[0] == "--smoke-events") await window.VerifyEventScriptsAsync(e.Args[2]); else await window.VerifyPreviewAsync(e.Args[2]); window.Close(); }
                catch (Exception ex) { System.IO.File.WriteAllText(e.Args[2] + ".error.txt", ex.ToString()); Shutdown(1); }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        else if (e.Args.Length == 3 && e.Args[0] == "--smoke")
        {
            window.LoadForSmoke(e.Args[1]);
            window.Show();
            window.Dispatcher.InvokeAsync(() => { window.Capture(e.Args[2]); window.Close(); }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        else { window.Show(); if(e.Args.Length==1 && System.IO.File.Exists(e.Args[0])) {try {window.OpenProjectPath(e.Args[0]);}catch(Exception ex){MessageBox.Show(window,ex.Message,"Could not open project");}} }
    }
}




