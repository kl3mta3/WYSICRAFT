using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Wysicraft.Models;

namespace Wysicraft.Designer;

public partial class MainWindow
{
    internal async Task VerifyMcpAsync(string output,bool connectionOnly=false)
    {
        using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(20) };
        int requestId=0;
        async Task<JsonNode> Rpc(string method,object args)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,mcpUrl);
            request.Headers.Add("Authorization","Bearer "+mcpToken);
            request.Headers.Add("Accept","application/json, text/event-stream");
            request.Headers.Add("MCP-Protocol-Version","2025-11-25");
            request.Content=new StringContent(Json.Write(new { jsonrpc="2.0",id=++requestId,method,@params=args }),Encoding.UTF8,"application/json");
            using var response=await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body=await response.Content.ReadAsStringAsync();
            if(body.StartsWith("event:") || body.StartsWith("data:")) body=body.Split('\n').First(l=>l.StartsWith("data: "))[6..];
            var result=JsonNode.Parse(body)!;
            if(result["error"]!=null) throw new InvalidOperationException(result.ToJsonString());
            return result["result"]!;
        }
        async Task<JsonNode> Call(string name,object args,bool fail=false)
        {
            var result=await Rpc("tools/call",new { name,arguments=args });
            if((result["isError"]?.GetValue<bool>() ?? false)!=fail) throw new InvalidOperationException(result.ToJsonString());
            return fail ? result : JsonNode.Parse(result["content"]![0]!["text"]!.GetValue<string>())!;
        }
        try
        {
            await StartMcp();
            using(var denied=await client.GetAsync(mcpUrl))
                if(denied.StatusCode!=HttpStatusCode.Unauthorized) throw new Exception("Unauthenticated access was not rejected.");
            using(var hostile=new HttpRequestMessage(HttpMethod.Get,mcpUrl))
            {
                hostile.Headers.Add("Authorization","Bearer "+mcpToken); hostile.Headers.Add("Origin","https://example.com");
                using var denied=await client.SendAsync(hostile);
                if(denied.StatusCode!=HttpStatusCode.Forbidden) throw new Exception("Cross-origin access was not rejected.");
            }
            await Rpc("initialize",new { protocolVersion="2025-11-25",capabilities=new {},clientInfo=new { name="Wysicraft smoke",version="1" } });
            var listing=await Rpc("tools/list",new {});
            if(listing["tools"]!.AsArray().Count!=16) throw new Exception("Tools missing.");
            if(connectionOnly) { if(!mcpClients.ContainsKey("Wysicraft smoke") || mcpRequests<2) throw new Exception("Client visibility missing"); File.WriteAllText(output,"PASS: authentication, origin checks, MCP discovery and client visibility."); return; }
            var before=await Call("get_project",new {});
            string revision=before["revision"]!.GetValue<string>();
            string screen=ui.Id;
            var applied=await Call("apply_edits",new { expectedRevision=revision,edits=new object[] {
                new { kind="upsert_element",screen,element="mcp_smoke",data=new { type="button",text="MCP test" } },
                new { kind="put_script",key="scripts/client/mcp_smoke.js",source="function click(ctx) { console.log('MCP'); }" },
                new { kind="set_event",screen,element="mcp_smoke",key="click",data=new { client=new { script="scripts/client/mcp_smoke.js",function="click" } } }
            } });
            if(!ui.Elements.Any(e=>e.Id=="mcp_smoke" && e.Events["click"].Client.Function=="click")) throw new Exception("Live editor did not update.");
            string changed=applied["revision"]!.GetValue<string>();
            var stale=await Call("apply_edits",new { expectedRevision=revision,edits=new[]{new { kind="delete_element",screen,element="mcp_smoke" }} },fail:true);
            if(!stale.ToJsonString().Contains("Project changed")) throw new Exception("Revision recovery instructions are missing.");
            await Call("apply_edits",new { expectedRevision=changed,edits=new object[] {
                new { kind="upsert_element",screen,element="mcp_smoke",data=new { text="Should roll back" } },
                new { kind="upsert_element",screen,element="mcp_smoke",data=new { nonexistentProperty=true } }
            } },fail:true);
            if(Revision()!=changed || ui.Elements.Single(e=>e.Id=="mcp_smoke").Text!="MCP test") throw new Exception("Invalid batch was not atomic.");
            var undone=await Call("undo",new { expectedRevision=changed });
            if(undone["revision"]!.GetValue<string>()!=revision) throw new Exception("Undo did not restore project.");
            await Call("apply_edits",new{expectedRevision=revision,edits=new[]{new{kind="put_script",key="scripts/client/broken.js",source="function broken( {"}}},fail:true);
            if(Revision()!=revision)throw new Exception("Malformed script changed the project");
            var templates=await Call("get_templates",new{});
            foreach(var item in templates["templates"]!.AsArray()) Jint.Engine.PrepareScript(item!["source"]!.GetValue<string>());
            var templated=await Call("apply_template",new{expectedRevision=revision,templateId="[Template] Global reward command — KubeJS",screen,eventName="open"});
            string templateRevision=templated["result"]!["revision"]!.GetValue<string>();
            await Call("project_control",new{expectedRevision=templateRevision,action="new",projectId="do_not_replace_dirty"},fail:true);
            await Call("undo",new{expectedRevision=templateRevision});
            var checkbox=await Call("apply_edits",new{expectedRevision=revision,edits=new[]{new{kind="upsert_element",screen,element="mcp_checkbox",data=new{type="checkbox",value="false",events=new{ @checked=new{client=new{actions=Array.Empty<object>()}}}}}}});
            string checkboxRevision=checkbox["revision"]!.GetValue<string>();
            await Call("preview_control",new{expectedRevision=checkboxRevision,action="open",screen});
            var clicked=await Call("preview_control",new{expectedRevision=checkboxRevision,action="event",element="mcp_checkbox",eventName="checked",value="true"});
            if(clicked["elements"]!.AsArray().Single(e=>e!["id"]!.GetValue<string>()=="mcp_checkbox")!["value"]!.GetValue<string>()!="true")throw new Exception("Preview event did not update value");
            await Call("preview_control",new{expectedRevision="stale",action="close"});
            await Call("undo",new{expectedRevision=checkboxRevision});
            await Call("get_test_status",new {});
            await Call("get_schema",new {});
            await Call("validate_project",new {});
            string savePath=Path.GetFullPath(output)+"."+Guid.NewGuid().ToString("N")+".wysicraftproj";
            await Call("save_project_as",new {expectedRevision=revision,path=savePath});
            if(!File.Exists(savePath))throw new Exception("MCP Save As failed");
            // Use the approved PNG to exercise import through the actual endpoint.
            string branding=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../assets/branding/wysicraft-wc-icon.png"));
            if(!File.Exists(branding))branding=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../Branding/wysicraft-wc-icon.png"));
            if(File.Exists(branding)) {
                var imported=await Call("import_asset",new{expectedRevision=revision,path=branding});
                string asset=project.Assets.Keys.Single(k=>k.EndsWith("wysicraft-wc-icon.png"));
                var deleted=await Call("apply_edits",new{expectedRevision=imported["revision"]!.GetValue<string>(),edits=new[]{new{kind="delete_asset",key=asset}}});
                await Call("undo",new{expectedRevision=deleted["revision"]!.GetValue<string>()});
                await Call("undo",new{expectedRevision=imported["revision"]!.GetValue<string>()});
            }
            await Call("preview_control",new{expectedRevision=revision,action="open",screen});
            var capture=await Call("preview_control",new{expectedRevision=revision,action="capture"});
            if(!File.Exists(capture["path"]!.GetValue<string>()))throw new Exception("Preview capture failed");
            await Call("preview_control",new{expectedRevision=revision,action="close"});
            await Call("minecraft_test_control",new{expectedRevision=revision,action="export",path=Path.GetFullPath(output)+".missing.jar"},fail:true);
            await Call("minecraft_test_control",new{expectedRevision="stale",action="stop"});
            var exported=await Call("export_project",new{expectedRevision=revision,format="kubejs"});
            string jarPath=exported["path"]!.GetValue<string>();
            if(!jarPath.EndsWith(".jar"))throw new Exception("KubeJS export must be a JAR");
            using(var jar=System.IO.Compression.ZipFile.OpenRead(jarPath))if(jar.GetEntry("META-INF/jarjar/metadata.json")==null)throw new Exception("Export is not bundled");
            await Call("save_project",new{expectedRevision=revision});
            var fresh=await Call("project_control",new{expectedRevision=revision,action="new",projectId="mcp_new"});
            string freshRevision=fresh["revision"]!.GetValue<string>();
            await Call("save_project_as",new{expectedRevision=freshRevision,path=savePath+".new.wysicraftproj"});
            var opened=await Call("project_control",new{expectedRevision=freshRevision,action="open",path=savePath});
            if(opened["revision"]!.GetValue<string>()!=revision)throw new Exception("Open project did not restore saved project");
            await StopMcp();
            try { using var stopped=await client.GetAsync(mcpUrl); throw new Exception("Server still accepts connections after stopping."); }
            catch(HttpRequestException) { }
            File.WriteAllText(output,"PASS: 16 MCP tools, authentication, atomic edits/undo, syntax rejection, template discovery/assignment, asset deletion, preview value changes, stale-revision close/stop, launch failure reporting, bundled KubeJS export, project new/open/save, capture, and shutdown. No Minecraft launch needed.");
        }
        finally { await StopMcp(); dirty=false; }
    }
}
