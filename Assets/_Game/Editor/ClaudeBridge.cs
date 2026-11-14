#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// File-driven bridge so an external agent can drive the open Editor without typing into it.
/// Write commands (one per line) to ClaudeBridge/cmd.txt; results are appended to ClaudeBridge/out.txt.
/// Commands: ping | refresh | open <scenePath> | save | menu <path> | run <Type.Method> | play | stop
///           | capture <file.png> | log | clearlog | wait <seconds>
/// The folder lives at the project root (outside Assets), so Unity never imports it.
/// </summary>
[InitializeOnLoad]
public static class ClaudeBridge
{
    static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ClaudeBridge"));
    static string Cmd => Path.Combine(Dir, "cmd.txt");
    static string Out => Path.Combine(Dir, "out.txt");
    static string Pending => Path.Combine(Dir, "pending.txt");
    static string LogFile => Path.Combine(Dir, "console.txt");
    static double nextPoll, waitUntil;
    static readonly List<string> compileErrors = new();

    static ClaudeBridge()
    {
        Directory.CreateDirectory(Dir);
        EditorApplication.update += Poll;
        Application.logMessageReceivedThreaded += OnLog;
        CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
        {
            foreach (var m in msgs) if (m.type == CompilerMessageType.Error) lock (compileErrors) compileErrors.Add(m.message);
        };
        CompilationPipeline.compilationStarted += _ => { lock (compileErrors) compileErrors.Clear(); };
        Write("loaded " + DateTime.Now.ToString("HH:mm:ss") + " play=" + EditorApplication.isPlaying);
    }

    static void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Log || condition.Contains("Sharing violation") || condition.Contains("is obsolete")) return;
        try { File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] {type}: {condition}\n{(type == LogType.Exception || type == LogType.Error ? stack + "\n" : "")}"); } catch { }
    }

    static void Write(string line)
    {
        try { File.AppendAllText(Out, line + "\n"); } catch { }
    }

    static System.Threading.Tasks.Task pendingTask;
    static string pendingName;
    static double pendingStarted;

    static string Describe(object result)
    {
        if (result == null) return "(void)";
        if (result is string str) return str;
        if (result is System.Collections.IEnumerable list) { var sb = new StringBuilder(); foreach (var o in list) sb.Append("\n  ").Append(o); return sb.ToString(); }
        return result.ToString();
    }

    static void Poll()
    {
        if (pendingTask != null)
        {
            if (!pendingTask.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup - pendingStarted < 240) return;
                Write("TIMEOUT async " + pendingName); pendingTask = null; return;
            }
            var t = pendingTask; pendingTask = null;
            if (t.IsFaulted) Write("ERROR async " + pendingName + " :: " + t.Exception?.GetBaseException().Message);
            else
            {
                var prop = t.GetType().GetProperty("Result");
                Write("run " + pendingName + " => " + Describe(prop != null ? prop.GetValue(t) : null));
            }
        }
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        // A queue survives domain reloads (entering Play recompiles nothing, but reloads the domain).
        if (File.Exists(Cmd))
        {
            string text = File.ReadAllText(Cmd);
            File.Delete(Cmd);
            File.AppendAllText(Pending, text.TrimEnd() + "\n");
        }
        if (!File.Exists(Pending)) return;
        var lines = File.ReadAllLines(Pending).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0) { File.Delete(Pending); return; }
        string next = lines[0].Trim();
        lines.RemoveAt(0);
        File.WriteAllLines(Pending, lines);
        try { Execute(next); }
        catch (Exception e) { var x = e.InnerException ?? e; Write("ERROR " + next + " :: " + x.GetType().Name + ": " + x.Message + "\n" + x.StackTrace); }
    }

    static void Execute(string line)
    {
        int space = line.IndexOf(' ');
        string verb = space < 0 ? line : line[..space];
        string arg = space < 0 ? "" : line[(space + 1)..].Trim();
        switch (verb)
        {
            case "ping": Write("pong scene=" + EditorSceneManager.GetActiveScene().path + " dirty=" + EditorSceneManager.GetActiveScene().isDirty + " play=" + EditorApplication.isPlaying); break;
            case "refresh":
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Write("refreshed compiling=" + EditorApplication.isCompiling);
                break;
            case "compile":
                lock (compileErrors) Write(compileErrors.Count == 0 ? "compile OK" : "compile ERRORS\n" + string.Join("\n", compileErrors.Distinct()));
                break;
            case "open":
                if (EditorApplication.isPlaying) throw new Exception("stop Play first");
                if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("active scene has unsaved changes: " + EditorSceneManager.GetActiveScene().path);
                EditorSceneManager.OpenScene(arg, OpenSceneMode.Single);
                Write("opened " + arg);
                break;
            case "save":
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Write("saved " + EditorSceneManager.GetActiveScene().path);
                break;
            case "menu":
                Write((EditorApplication.ExecuteMenuItem(arg) ? "menu OK " : "menu NOT FOUND ") + arg);
                break;
            case "run":
            {
                int dot = arg.LastIndexOf('.');
                string typeName = arg[..dot], method = arg[(dot + 1)..];
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
                if (type == null) throw new Exception("type not found " + typeName);
                var m = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var result = m.Invoke(null, null);
                if (result is System.Threading.Tasks.Task task) { pendingTask = task; pendingName = arg; pendingStarted = EditorApplication.timeSinceStartup; Write("run " + arg + " started (async)"); break; }
                Write("run " + arg + " => " + Describe(result));
                break;
            }
            case "play": EditorApplication.isPlaying = true; Write("entering play"); waitUntil = EditorApplication.timeSinceStartup + 3; break;
            case "stop": EditorApplication.isPlaying = false; Write("stopping play"); waitUntil = EditorApplication.timeSinceStartup + 2; break;
            case "wait": waitUntil = EditorApplication.timeSinceStartup + double.Parse(arg, System.Globalization.CultureInfo.InvariantCulture); Write("waiting " + arg); break;
            case "capture":
            {
                string path = Path.IsPathRooted(arg) ? arg : Path.GetFullPath(Path.Combine(Application.dataPath, "..", arg));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (EditorApplication.isPlaying)
                {
                    // Focus the Game view so it renders this frame, then capture it including overlay UI.
                    var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
                    if (gameView != null) EditorWindow.FocusWindowIfItsOpen(gameView);
                    ScreenCapture.CaptureScreenshot(path, 1);
                    Write("capture requested (play) " + path);
                }
                else
                {
                    var cam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : Camera.main;
                    CaptureCamera(cam, path, 1600, 900);
                    Write("capture (edit/scene view) " + path);
                }
                waitUntil = EditorApplication.timeSinceStartup + 1.5;
                break;
            }
            case "log":
                Write("---- console ----\n" + (File.Exists(LogFile) ? File.ReadAllText(LogFile) : "(empty)") + "---- end ----");
                break;
            case "clearlog":
                if (File.Exists(LogFile)) File.Delete(LogFile);
                var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
                Write("log cleared");
                break;
            default: Write("unknown command " + verb); break;
        }
    }

    public static void CaptureCamera(Camera cam, string path, int width, int height)
    {
        if (cam == null) throw new Exception("no camera");
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var previous = cam.targetTexture;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = previous;
        var active = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
        RenderTexture.active = active;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
}
#endif
