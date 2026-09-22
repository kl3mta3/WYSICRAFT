using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ModelContextProtocol.Server;
using Wysicraft.Core;
using Wysicraft.Models;
using Wysicraft.Packaging;

namespace Wysicraft.Designer;
public partial class MainWindow
{
    PreviewSession? activePreview;
    string previewRevision="";
    internal Task<string> McpWork(string operation,string expected="",string path="",string screen="",string element="",string eventName="click",string value="",CancellationToken cancellationToken=default) => Dispatcher.InvokeAsync(async ()=> {
        try {
            if(mcpHost==null) throw new InvalidOperationException("MCP server is stopped.");
            if(operation is not ("test_stop" or "preview_close" or "templates")) CheckRevision(expected);
            switch(operation) {
                case "save_as":
                    if(!Path.IsPathFullyQualified(path) || !path.EndsWith(".wysicraftproj",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Provide an absolute .wysicraftproj path in an existing directory.");
                    if(File.Exists(path) && !string.Equals(path,folder,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Destination exists. Choose a new filename.");
                    ProjectStore.SaveProject(project,path);folder=path;dirty=false;ClearRecovery();Log("MCP saved "+path);return Json.Write(new{path,revision=Revision()});
                case "import_asset":
                    if(!Path.IsPathFullyQualified(path) || !path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Provide an absolute PNG path.");
                    if(new FileInfo(path).Length>ProjectStore.MaxEntry) throw new InvalidDataException("PNG exceeds 32 MiB.");
                    var bytes=File.ReadAllBytes(path);ProjectStore.TextureSize(bytes);
                    string name=System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(),"[^a-z0-9_-]","_")+".png";
                    string resource=TextureAssets.Resource(project.Manifest.Id,name), asset=TextureAssets.Path(project.Manifest.Id,name);
                    if(project.Assets.ContainsKey(asset)) throw new InvalidDataException("Asset already exists. Import a uniquely named PNG.");
                    Change();project.Assets[asset]=bytes;RefreshAll();return Json.Write(new{resource,revision=Revision()});
                case "preview_open":
                    if(activePreview!=null) await activePreview.CloseAsync();
                    if(screen.Length==0) screen=ui.Id;
                    if(!project.Screens.Any(s=>s.Id==screen)) throw new InvalidDataException("Screen not found");
                    var errors=Wysicraft.Core.Validation.Check(project);if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors));
                    activePreview=new PreviewSession(this,Json.CloneProject(project),screen);previewRevision=Revision();activePreview.Window.Show();
                    await activePreview.WaitReady();return activePreview.Snapshot();
                case "preview_close":
                    if(activePreview!=null) await activePreview.CloseAsync();activePreview=null;return Json.Write(new{closed=true});
                case "preview_event": case "preview_capture":
                    if(activePreview==null || !activePreview.Window.IsVisible)throw new InvalidOperationException("Call preview_open first.");
                    if(previewRevision!=Revision())throw new InvalidOperationException("Project changed. Reopen Preview to use the latest project.");
                    if(operation=="preview_event") { await activePreview.RunMcpEvent(element,eventName,value);return activePreview.Snapshot(); }
                    await activePreview.WaitReady();
                    string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wysicraft","McpCaptures");Directory.CreateDirectory(root);
                    string capture=Path.Combine(root,Guid.NewGuid().ToString("N")+".png");activePreview.CaptureCanvas(capture);return Json.Write(new{path=capture});
                case "templates":
                    return Json.Write(new { templates=ScriptTemplate.All.Select(t=>new{ id=t.Title,title=t.Title,server=t.Server,engine=t.Engine,global=t.Global,source=t.Source("on_event",project.Manifest.Id).Replace("__PROJECT__",project.Manifest.Id) }),instructions="Use apply_template with an id, screen, eventName, and optional element. Constants in source are editable placeholders. Global registration uses KubeJS and exports because its script is assigned to an event." });
                case "template_apply":
                    var template=ScriptTemplate.All.SingleOrDefault(t=>t.Title==path) ?? throw new InvalidDataException("Template not found; call get_templates.");
                    var screenDefinition=project.Screens.SingleOrDefault(s=>s.Id==screen) ?? throw new InvalidDataException("Screen not found");
                    string function=value.Length==0?"on_"+eventName:value; CheckFunction(function);
                    string prefix=template.Server?"scripts/server/":"scripts/client/";
                    string stem=screen+"_"+(element.Length==0?"screen":element)+"_"+eventName;
                    string scriptPath=prefix+stem+".js";int n=2;while(project.Scripts.ContainsKey(scriptPath))scriptPath=prefix+stem+"_"+(n++)+".js";
                    var handler=new {script=scriptPath,function,scriptEngine=template.Engine};
                    var edits=new List<ProjectEdit> {
                        new(){Kind="put_script",Key=scriptPath,Source=template.Source(function,project.Manifest.Id).Replace("__PROJECT__",project.Manifest.Id)},
                        new(){Kind="set_event",Screen=screen,Element=element,Key=eventName,Data=System.Text.Json.JsonSerializer.SerializeToElement(template.Server?(object)new{server=handler}:new{client=handler})}
                    };
                    string result=await McpInvoke("apply_edits",expected,edits,cancellationToken:cancellationToken);
                    return Json.Write(new{script=scriptPath,function,engine=template.Engine,result=System.Text.Json.JsonSerializer.Deserialize<object>(result)});
                case "project_new": case "project_open":
                    if(dirty) throw new InvalidOperationException("Save the current project before replacing it.");
                    if(activePreview!=null) throw new InvalidOperationException("Close Preview before replacing the project.");
                    Project replacement;
                    if(operation=="project_open") {
                        if(!Path.IsPathFullyQualified(path))throw new InvalidDataException("Provide an absolute project path.");
                        replacement=ProjectStore.Load(path);
                    } else {
                        if(!Wysicraft.Core.Validation.Id(value))throw new InvalidDataException("Provide a lowercase project ID.");
                        replacement=new();replacement.Manifest.Id=value;replacement.Manifest.Name=value;
                    }
                    if(replacement.Screens.Count==0)throw new InvalidDataException("Project needs a screen.");
                    project=replacement;ui=project.Screens.FirstOrDefault(s=>s.Id==project.Manifest.DefaultUi)??project.Screens[0];
                    folder=operation=="project_open" && path.EndsWith(".wysicraftproj",StringComparison.OrdinalIgnoreCase)?path:null;
                    editingScript=null;selected.Clear();history.Clear();dirty=operation=="project_new";RefreshAll();return await McpInvoke("get_project",cancellationToken:cancellationToken);
                case "test_start": case "test_export": case "test_apply": case "test_stop": case "test_open": case "test_close": case "test_command":
                    if(operation=="test_stop" && minecraftTest==null)return Json.Write(new{running=false});
                    if(minecraftTest==null)TestMinecraft();
                    await minecraftTest!.McpControl(operation,path,value);return minecraftTest.McpStatus();
                default: throw new InvalidOperationException("Unknown operation");
            }
        } catch(Exception ex) when(ex is not OperationCanceledException) { throw new ModelContextProtocol.McpException(ex.Message); }
    },System.Windows.Threading.DispatcherPriority.Normal,cancellationToken).Task.Unwrap();
}

public sealed partial class DesignerMcpTools
{
    [McpServerTool(Name="save_project_as"),Description("Save the live project as a single editable .wysicraftproj at an absolute path. Requires an existing parent directory; refuses to overwrite a different existing file.")]
    public Task<string> SaveAs(string expectedRevision,string path,CancellationToken cancellationToken)=>editor.McpWork("save_as",expectedRevision,path,cancellationToken:cancellationToken);
    [McpServerTool(Name="import_asset"),Description("Import a local PNG (absolute path, at most 32 MiB) as a project texture. Returns its resource ID. One Undo step; refuses duplicate asset names.")]
    public Task<string> ImportAsset(string expectedRevision,string path,CancellationToken cancellationToken)=>editor.McpWork("import_asset",expectedRevision,path,cancellationToken:cancellationToken);
    [McpServerTool(Name="preview_control"),Description("Operate the desktop simulation. action is open, close, event or capture. open optionally takes screen; event takes element, eventName and value (omit element for screen events). capture returns a local PNG path. Server operations remain simulated. close ignores revision and field-edit locks.")]
    public Task<string> PreviewControl(string expectedRevision,string action,string screen="",string element="",string eventName="click",string value="",CancellationToken cancellationToken=default)=>editor.McpWork("preview_"+action,expectedRevision,screen:screen,element:element,eventName:eventName,value:value,cancellationToken:cancellationToken);
    [McpServerTool(Name="minecraft_test_control"),Description("Control Minecraft: action is start (editor), export (absolute JAR/ZIP path), apply, open, close, command (command text), or stop. Read get_test_status until ready and to obtain command completion/errors. Uses configured Java/instance. Stop ignores revision and field-edit locks. Launch may download dependencies.")]
    public Task<string> MinecraftControl(string expectedRevision,string action,string path="",string command="",CancellationToken cancellationToken=default)=>editor.McpWork("test_"+action,expectedRevision,path:path,value:command,cancellationToken:cancellationToken);
    [McpServerTool(Name="get_templates",ReadOnly=true),Description("List editable script and global-command templates, source code, engine, and assignment guidance.")]
    public Task<string> Templates(CancellationToken cancellationToken)=>editor.McpWork("templates",cancellationToken:cancellationToken);
    [McpServerTool(Name="apply_template"),Description("Create a uniquely named script from a template id returned by get_templates and assign it to an event in one Undo step. Existing scripts are preserved; assignment on that side is replaced. Edit its placeholder constants using put_script.")]
    public Task<string> ApplyTemplate(string expectedRevision,string templateId,string screen,string eventName,string element="",string function="",CancellationToken cancellationToken=default)=>editor.McpWork("template_apply",expectedRevision,path:templateId,screen:screen,element:element,eventName:eventName,value:function,cancellationToken:cancellationToken);
    [McpServerTool(Name="project_control"),Description("Create or open a project. action is new (projectId required) or open (absolute path required). Refuses to replace unsaved changes or an active preview; save/close first.")]
    public Task<string> ProjectControl(string expectedRevision,string action,string path="",string projectId="",CancellationToken cancellationToken=default)=>editor.McpWork("project_"+action,expectedRevision,path:path,value:projectId,cancellationToken:cancellationToken);
}

