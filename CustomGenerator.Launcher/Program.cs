using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace CustomGenerator.Launcher
{
    // Put next to RustDedicated.exe and double-click: serves the config editor on http://127.0.0.1:<port>/,
    // saves the config and runs map generation from the browser. Listens on loopback only.
    internal static class Program
    {
        public static string Root;
        public static int Port;
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static Generator _generator;

        private static int Main(string[] args) {
            Root = Path.GetFullPath(Arg(args, "--root") ?? AppDomain.CurrentDomain.BaseDirectory);
            int port = int.TryParse(Arg(args, "--port"), out int p) ? p : 28190;
            _generator = new Generator(Root);

            TcpListener listener = null;
            for (int i = 0; i < 20 && listener == null; i++) {
                try { listener = new TcpListener(IPAddress.Loopback, port + i); listener.Start(); Port = port + i; }
                catch (SocketException) { listener = null; }
            }
            if (listener == null) { Console.WriteLine($"No free port in {port}-{port + 19}, use --port"); return 1; }

            string url = $"http://127.0.0.1:{Port}/";
            Console.WriteLine("CustomGenerator launcher");
            Console.WriteLine($"  Server folder: {Root}");
            Console.WriteLine(_generator.Executable != null ? $"  Rust server:   {_generator.Executable}" : "  Rust server:   NOT FOUND - put the launcher next to RustDedicated.exe or pass --root <folder>");
            Console.WriteLine($"  Editor:        {url}");
            Console.WriteLine("Keep this window open while you use the editor. Ctrl+C to quit.");
            if (!args.Contains("--no-browser")) {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }

            while (true) {
                var client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => Handle(client));
            }
        }

        private static string Arg(string[] args, string name) {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void Handle(TcpClient client) {
            using (client) {
                try {
                    var stream = client.GetStream();
                    stream.ReadTimeout = 15000;
                    var request = Request.Read(stream);
                    if (request == null) return;
                    Response response;
                    try { response = Route(request); }
                    catch (Exception ex) { response = Response.Error(500, ex.Message); }
                    response.Write(stream);
                } catch (IOException) { } catch (SocketException) { }
            }
        }

        private static Response Route(Request request) {
            // Only pages opened from this launcher: blocks DNS rebinding (Host) and other sites posting here (custom header needs CORS)
            string host = request.Header("Host");
            if (host != $"127.0.0.1:{Port}" && host != $"localhost:{Port}") return Response.Error(403, "Bad host");
            if (request.Method != "GET" && request.Header("X-CG") != "1") return Response.Error(403, "Missing X-CG header");

            switch ($"{request.Method} {request.Path}") {
                case "GET /":
                case "GET /index.html":
                    return new Response(200, "text/html; charset=utf-8", Utf8.GetBytes(Editor()));
                case "GET /api/state":
                    return Response.RawJson(State(request.Query("custom")));
                case "GET /api/files":
                    return Response.RawJson(Json.Serialize(Files(request.Query("custom"))));
                case "PUT /api/config":
                    return SaveConfig(request.Body);
                case "POST /api/generate":
                    return Generate(request.Body);
                case "GET /api/generation":
                    return Response.RawJson(Json.Serialize(_generator.Status()));
                case "POST /api/stop":
                    _generator.Stop();
                    return Response.RawJson("{\"ok\":true}");
            }
            if (request.Method == "GET" && request.Path.StartsWith("/files/")) return ServeFile(Uri.UnescapeDataString(request.Path.Substring(7)));
            return Response.Error(404, "Not found");
        }

        private static string Editor() {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("ConfigEditor.html"))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }

        private static string ConfigDir => Path.Combine(Root, "HarmonyConfig");
        private static string ConfigPath => Path.Combine(ConfigDir, "CustomGenerator.json");

        // Raw file contents glued together, so values reach the page exactly as the mod wrote them
        private static string State(string customFolder) {
            var json = new StringBuilder("{");
            json.Append("\"schema\":").Append(ReadJson(Path.Combine(ConfigDir, "CustomGenerator.schema.json")));
            json.Append(",\"config\":").Append(ReadJson(ConfigPath));
            json.Append(",\"lastRun\":").Append(ReadJson(Path.Combine(ConfigDir, "CustomGenerator.lastrun.json")));
            json.Append(",\"prefabs\":").Append(ReadJson(Path.Combine(ConfigDir, "CustomGenerator.prefabs.json")));
            json.Append(",\"files\":").Append(Json.Serialize(Files(customFolder)));
            json.Append(",\"generation\":").Append(Json.Serialize(_generator.Status()));
            json.Append(",\"rustFound\":").Append(_generator.Executable != null ? "true" : "false");
            return json.Append('}').ToString();
        }

        private static string ReadJson(string path) {
            if (!File.Exists(path)) return "null";
            string text = File.ReadAllText(path).TrimStart('﻿');
            try { Json.DeserializeObject(text); return text; } catch { return "null"; }
        }

        private static Dictionary<string, object> Files(string customFolder) => new Dictionary<string, object> {
            ["custom"] = List(customFolder ?? "maps/custom", ".map", ".prefab"),
            ["swap"] = List("maps/prefabs", ".map"),
        };

        private static string[] List(string folder, params string[] extensions) {
            string full = Inside(folder);
            if (full == null || !Directory.Exists(full)) return new string[0];
            return Directory.GetFiles(full).Where(x => extensions.Any(e => x.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName).OrderBy(x => x).ToArray();
        }

        // Full path of a server-relative path, null if it points outside the server folder
        private static string Inside(string relative) {
            try {
                string full = Path.GetFullPath(Path.Combine(Root, relative));
                return full.StartsWith(Root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
            } catch { return null; }
        }

        private static Response SaveConfig(byte[] body) {
            if (_generator.Running) return Response.Error(409, "Generation is running, the mod rewrites the config when it starts. Stop it or wait.");
            string text = Utf8.GetString(body);
            try { if (!(Json.DeserializeObject(text) is Dictionary<string, object>)) throw new FormatException("not an object"); }
            catch (Exception ex) { return Response.Error(400, "Invalid JSON: " + ex.Message); }
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(ConfigPath)) File.Copy(ConfigPath, ConfigPath + ".bak", true);
            File.WriteAllText(ConfigPath, text, Utf8);
            return Response.RawJson("{\"ok\":true}");
        }

        private static Response Generate(byte[] body) {
            if (_generator.Executable == null) return Response.Error(400, "RustDedicated not found in " + Root);
            var data = Json.DeserializeObject(Utf8.GetString(body)) as Dictionary<string, object>;
            var runs = new List<Run>();
            var list = data != null && data.TryGetValue("runs", out var r) ? r as object[] : null;
            foreach (var item in list ?? new object[0]) {
                if (!(item is Dictionary<string, object> run)) continue;
                int size = Convert.ToInt32(run["size"]);
                long seed = Convert.ToInt64(run["seed"]);
                if (size < 1000 || size > 9000) return Response.Error(400, $"Size {size} must be 1000-9000");
                if (seed < 0 || seed > uint.MaxValue) return Response.Error(400, $"Seed {seed} must be 0-{uint.MaxValue}");
                runs.Add(new Run { Size = size, Seed = (uint)seed });
            }
            if (runs.Count == 0) return Response.Error(400, "Nothing to generate");
            string extra = data.TryGetValue("args", out var a) ? a as string ?? "" : "";
            string error = _generator.Start(runs, extra);
            return error == null ? Response.RawJson("{\"ok\":true}") : Response.Error(409, error);
        }

        private static Response ServeFile(string relative) {
            string full = Inside(relative);
            string ext = Path.GetExtension(relative).ToLowerInvariant();
            string type = ext == ".png" ? "image/png" : ext == ".txt" || ext == ".log" ? "text/plain; charset=utf-8" : null;
            bool allowed = relative.StartsWith("maps/") || relative.StartsWith("mapimages/") || relative.StartsWith("HarmonyConfig/logs/");
            if (full == null || type == null || !allowed || !File.Exists(full)) return Response.Error(404, "Not found");
            using (var file = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                var bytes = new byte[file.Length];
                file.Read(bytes, 0, bytes.Length);
                return new Response(200, type, bytes);
            }
        }
    }
}
