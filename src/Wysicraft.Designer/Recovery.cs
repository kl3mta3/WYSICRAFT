using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wysicraft.Models;
using Wysicraft.Packaging;

namespace Wysicraft.Designer;
public partial class MainWindow
{
    static string RecoveryRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WYSICRAFT","Recovery");
    RecoveryStore? recovery;
    string? recoveredFrom;
    bool crashRecovery, recoveryErrorReported;
    readonly DispatcherTimer recoveryTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    void InitializeRecovery() {
        if(DockSmoke)return;
        try {recovery=new RecoveryStore(RecoveryRoot);}catch(Exception ex){Log("Recovery unavailable: "+ex.Message);return;}
        recoveryTimer.Tick+=(_,_)=>WriteRecovery();recoveryTimer.Start();
        Loaded+=(_,_)=>{if(RecoveryStore.Available(RecoveryRoot).Any())Log("Unsaved drafts are available in File → Recover unsaved project.");};
        Closed+=(_,_)=>{recoveryTimer.Stop();try {if(!crashRecovery)ClearRecovery();recovery?.Dispose();}catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex);}};
    }
    void WriteRecovery() {
        if(recovery==null || (!dirty && !(editingScript!=null && project.Scripts.GetValueOrDefault(editingScript)!=ScriptEditor.Text)))return;
        try {
            var snapshot=Json.Clone(project);
            if(editingScript!=null && snapshot.Scripts.ContainsKey(editingScript))snapshot.Scripts[editingScript]=ScriptEditor.Text;
            recovery.Write(snapshot);recoveryErrorReported=false;
        } catch(Exception ex) {if(!recoveryErrorReported){Log("Could not save recovery draft: "+ex.Message);recoveryErrorReported=true;}}
    }
    void ClearRecovery() {
        try {recovery?.Clear();if(recoveredFrom!=null && File.Exists(recoveredFrom))File.Delete(recoveredFrom);recoveredFrom=null;}
        catch(Exception ex){Log("Could not remove old recovery draft: "+ex.Message);}
    }
    internal void CaptureCrash(Exception error) {
        crashRecovery=true;WriteRecovery();
        try {string root=Path.Combine(RecoveryRoot,"..","Logs");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"crash-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".txt"),error.ToString());}catch{ }
    }
    void RecoverProject() {
        var drafts=RecoveryStore.Available(RecoveryRoot).ToArray();
        if(drafts.Length==0){MessageBox.Show(this,"No abandoned recovery drafts were found.","Recovery");return;}
        var dialog=new Window {Owner=this,Title="Recover unsaved project",Width=620,Height=350,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new DockPanel {Margin=new Thickness(12)};dialog.Content=panel;
        var note=new TextBlock {Text="Open a recovery copy, then Save As to keep it. Your saved project is never overwritten by autosave.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};DockPanel.SetDock(note,Dock.Top);panel.Children.Add(note);
        var open=new Button {Content="Recover selected",HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(open,Dock.Bottom);panel.Children.Add(open);
        var list=new ListBox();panel.Children.Add(list);
        foreach(string path in drafts) {string name;try{name=ProjectStore.Load(path).Manifest.Name;}catch{name="Unreadable draft";}list.Items.Add(new ListBoxItem{Content=$"{File.GetLastWriteTime(path):g} — {name}",Tag=path});}list.SelectedIndex=0;
        open.Click+=(_,_)=>Guard(()=>{if(list.SelectedItem is not ListBoxItem entry || !CanReplace())return;var loaded=ProjectStore.Load((string)entry.Tag);ClearRecovery();project=loaded;folder=null;recoveredFrom=(string)entry.Tag;ui=project.Screens.First();editingScript=null;selected.Clear();history.Clear();dirty=true;RefreshAll();WriteRecovery();dialog.Close();Log("Recovered draft. Use Save As to keep it.");});
        dialog.ShowDialog();
    }
    internal void OpenProjectPath(string path) {
        var loaded=ProjectStore.Load(Path.GetFileName(path)=="project.json"?Path.GetDirectoryName(path)!:path);
        ClearRecovery();project=loaded;folder=path.EndsWith(".wysicraftproj",StringComparison.OrdinalIgnoreCase)?path:null;
        ui=project.Screens.FirstOrDefault(s=>s.Id==project.Manifest.DefaultUi) ?? project.Screens.First();
        selected.Clear();history.Clear();dirty=false;editingScript=null;RefreshAll();Log("Loaded "+project.Manifest.Name);
    }
    internal void VerifyRecovery(string output) {
        string root=Path.GetFullPath(output+".drafts");recovery=new RecoveryStore(root);
        try {
            editingScript="scripts/client/recovery.js";project.Scripts[editingScript]="saved text";
            ScriptEditor.Text="function draft(ctx) { console.log('unsaved'); }";dirty=false;
            WriteRecovery();var recovered=ProjectStore.Load(recovery.DraftPath);
            if(recovered.Scripts[editingScript]!=ScriptEditor.Text)throw new Exception("Unsaved script buffer was not recovered");
            ClearRecovery();if(File.Exists(recovery.DraftPath))throw new Exception("Recovery cleanup failed");
            File.WriteAllText(output,"PASS: editor recovery includes unsaved script buffer and clears saved drafts");
        }finally{recovery.Dispose();recovery=null;dirty=false;editingScript=null;}
    }
}
