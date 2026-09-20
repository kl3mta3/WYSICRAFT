using System.Diagnostics;
using System.IO;
using System.Text;
using Jint;
using Wysicraft.Models;
namespace Wysicraft.Designer;

// Scripts never receive CLR objects. A disposable process contains engine failures
// and lets the editor terminate work even if an engine built-in misses its timeout.
internal static class PreviewScripts
{
    internal sealed class Request
    {
        public string Source { get; set; } = "";
        public string SourceName { get; set; } = "preview.js";
        public bool Server { get; set; }
        public string Function { get; set; } = "";
        public string Element { get; set; } = "";
        public string Value { get; set; } = "";
        public Dictionary<string, string> Variables { get; set; } = [];
        public Dictionary<string, string> Texts { get; set; } = [];
    }
    internal sealed class Result { public List<VisualAction> Actions { get; set; } = []; public string Error { get; set; } = ""; }
    internal static async Task<Result> RunAsync(Request request)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Wysicraft.Designer.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--script-host");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start preview script host");
        try
        {
            await process.StandardInput.WriteAsync(Json.Write(request)); process.StandardInput.Close();
            var output = ReadBounded(process.StandardOutput);
            var errors = ReadBounded(process.StandardError);
            var watch = Stopwatch.StartNew();
            while (!process.HasExited)
            {
                process.Refresh();
                if (watch.Elapsed > TimeSpan.FromSeconds(4) || process.WorkingSet64 > 256L * 1024 * 1024) throw new InvalidOperationException("Script exceeded its preview time/memory budget");
                await Task.Delay(25);
            }
            string text = await output, error = await errors;
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Script host failed. " + error);
            return Json.Read<Result>(text);
        }
        catch (Exception ex) { return new Result { Error = ex.Message }; }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
    static async Task<string> ReadBounded(StreamReader reader)
    {
        var result = new StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0) { if (result.Length + count > 262144) throw new InvalidDataException("Script output exceeds 256 KiB"); result.Append(buffer, 0, count); }
        return result.ToString();
    }
    internal static int RunHost()
    {
        Result result;
        try
        {
            using var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            string json = ReadBounded(input).GetAwaiter().GetResult(); var request = Json.Read<Request>(json);
            if (request.Source.Length > 65536) throw new InvalidDataException("Script exceeds 64 KiB");
            if (request.Function.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(request.Function, "^[a-zA-Z_][a-zA-Z0-9_]*$")) throw new InvalidDataException("Invalid script function");
            using var engine = new Engine(options => options.LimitMemory(16_000_000).MaxStatements(20_000).LimitRecursion(32).TimeoutInterval(TimeSpan.FromMilliseconds(300)));
            // Data enters as JSON syntax, not a host object or delegate.
            engine.Execute("const __input = " + Json.Write(request) + ";");
            engine.Execute(Bootstrap);
            var drain = engine.Evaluate("__drain");
            engine.Execute(request.Source,request.SourceName);
            if (request.Function.Length > 0) engine.Invoke(request.Function, engine.GetValue("ctx"));
            string actions = engine.Invoke(drain).AsString();
            if (actions.Length > 250000) throw new InvalidDataException("Too much script output");
            result = new Result { Actions = Json.Read<List<VisualAction>>(actions) };
        }
        catch (Exception ex) { result = new Result { Error = ex is Jint.Runtime.JavaScriptException js ? ex.Message+"\n"+js.JavaScriptStackTrace : ex.Message }; }
        using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)); output.Write(Json.Write(result)); output.Flush(); return 0;
    }
    const string Bootstrap = """
        const __drain = (() => {
          const queue = [], stringify = JSON.stringify;
          const vars = Object.assign(Object.create(null), __input.variables);
          function emit(type, target, value) {
            if (queue.length >= 128) throw new Error('Preview output limit: 128 operations');
            target = String(target ?? ''); value = String(value ?? '');
            if (target.length > 256 || value.length > 4096) throw new Error('Preview value is too long');
            queue.push({type, target, value});
          }
          function format(args) { return args.map(x => typeof x === 'string' ? x : stringify(x)).join(' '); }
          function setItem(id, resource) {
            if (typeof resource !== 'string' || !/^[a-z0-9_.-]+:[a-z0-9/._-]+$/.test(resource)) throw new Error('setItem requires a namespaced item ID, e.g. minecraft:diamond');
            emit('set_item', id, resource);
          }
          globalThis.console = Object.freeze({
            log: (...args) => emit('console_log', '', format(args)),
            warn: (...args) => emit('console_warn', '', format(args)),
            error: (...args) => emit('console_error', '', format(args))
          });
          const ui = Object.freeze({
            setText: (id, text) => emit('set_text', id, text),
            setItem,
            setItems: (id, items) => emit('set_value', id, stringify(items)),
            setVisible: (id, visible) => emit('set_visible', id, visible),
            setEnabled: (id, enabled) => emit('set_enabled', id, enabled),
            setValue: (id, value) => emit('set_value', id, value),
            changeTexture: (id, resource) => emit('change_texture', id, resource),
            getVariable: name => vars[name],
            setVariable: (name, value) => { vars[name] = value; emit('set_variable', name, value); },
            open: id => emit('open_ui', '', id), close: () => emit('close_ui', '', ''),
            getElement: id => Object.freeze({id, text: __input.texts[id], setText: text => emit('set_text', id, text), setItem: resource => setItem(id, resource)})
          });
          const server = Object.freeze({
            runCommand: command => emit('simulated_command', '', command),
            sendMessage: text => emit('simulated_message', '', text)
          });
          globalThis.ui = ui;
          globalThis.ctx = Object.freeze({ ui, server,
            player: __input.server ? Object.freeze({getName:()=> 'Preview player', getUuid:()=> '00000000-0000-0000-0000-000000000000',getPosition:()=>({x:0,y:0,z:0,dimension:'minecraft:overworld'}),getInventory:()=>[],hasPermission:()=>false}) : undefined,
            elementId: __input.element, value: __input.value,
            message: text => emit('message', '', text),
            getVariable: ui.getVariable, setVariable: ui.setVariable,
            client: Object.freeze({sendMessage: text => emit('message', '', text), playSound: id => emit('play_sound', '', id)}),
            state: Object.freeze({get: ui.getVariable, set: ui.setVariable})
          });
          return () => stringify(queue);
        })();
        """;
}
