using Wysicraft.Models;

namespace Wysicraft.Packaging;

/// <summary>Each editor owns one draft. An exclusive lease keeps other instances from offering a live draft.</summary>
public sealed class RecoveryStore : IDisposable
{
    readonly FileStream lease;
    public string DraftPath { get; }
    readonly string lockPath;
    public RecoveryStore(string root) {
        Directory.CreateDirectory(root);
        string stem=Path.Combine(root,Guid.NewGuid().ToString("N"));
        DraftPath=stem+".wysicraftproj";lockPath=stem+".lock";
        lease=new FileStream(lockPath,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
    }
    public void Write(Project project) => ProjectStore.SaveProject(project,DraftPath);
    public void Clear() {if(File.Exists(DraftPath))File.Delete(DraftPath);}
    public static IEnumerable<string> Available(string root) {
        if(!Directory.Exists(root))yield break;
        foreach(string path in Directory.GetFiles(root,"*.wysicraftproj").OrderByDescending(File.GetLastWriteTimeUtc)) {
            string marker=Path.ChangeExtension(path,".lock");bool active=false;
            try {if(File.Exists(marker)){using var probe=new FileStream(marker,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}}
            catch(IOException){active=true;}catch(UnauthorizedAccessException){active=true;}
            if(!active)yield return path;
        }
    }
    public void Dispose() {lease.Dispose();if(File.Exists(lockPath))File.Delete(lockPath);}
}
