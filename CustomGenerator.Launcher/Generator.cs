using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomGenerator.Launcher
{
    internal sealed class Run { public int Size; public uint Seed; }

    // Runs RustDedicated once per map (the mod generates, saves and quits), one after another
    internal sealed class Generator
    {
        // Not the default ports, so a live server on the same machine doesn't clash
        private const string Ports = "+server.port 28915 +server.queryport 28916 +rcon.port 28918 +app.port 28919";

        private readonly string _root;
        private readonly object _lock = new object();
        private readonly Queue<Run> _queue = new Queue<Run>();
        private readonly List<Dictionary<string, object>> _done = new List<Dictionary<string, object>>();
        private Process _process;
        private Run _current;
        private string _extra = "", _log;
        private DateTime _started;
        private bool _stopping;

        public Generator(string root) {
            _root = root;
            Executable = new[] { "RustDedicated.exe", "RustDedicated" }.Select(x => Path.Combine(root, x)).FirstOrDefault(File.Exists);
        }

        public string Executable { get; }
        public bool Running { get { lock (_lock) return _process != null; } }

        public string Start(List<Run> runs, string extra) {
            lock (_lock) {
                if (_process != null) return "Generation is already running";
                _queue.Clear();
                foreach (var run in runs) _queue.Enqueue(run);
                _done.Clear();
                _extra = extra ?? "";
                _stopping = false;
                return StartNext();
            }
        }

        public void Stop() {
            lock (_lock) {
                _stopping = true;
                _queue.Clear();
                try { _process?.Kill(); } catch { }
            }
        }

        // Called under the lock
        private string StartNext() {
            _current = null;
            if (_queue.Count == 0) return null;
            var run = _queue.Dequeue();
            string logs = Path.Combine(_root, "HarmonyConfig", "logs");
            Directory.CreateDirectory(logs);
            _log = Path.Combine(logs, $"launcher_{run.Size}_{run.Seed}.log");
            try { File.Delete(_log); } catch { }

            var info = new ProcessStartInfo(Executable,
                $"-batchmode -nographics -logfile \"{_log}\" +server.identity cgen_launcher +server.level \"Procedural Map\" " +
                $"+server.worldsize {run.Size} +server.seed {run.Seed} {Ports} {_extra}".TrimEnd()) {
                WorkingDirectory = _root, UseShellExecute = false, CreateNoWindow = true,
            };
            try {
                _process = Process.Start(info);
                _process.EnableRaisingEvents = true;
                _process.Exited += (s, e) => OnExit();
                _current = run;
                _started = DateTime.Now;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Generating {run.Size} / {run.Seed}");
                return null;
            } catch (Exception ex) {
                _process = null;
                return "Failed to start RustDedicated: " + ex.Message;
            }
        }

        private void OnExit() {
            lock (_lock) {
                if (_current != null) {
                    int code = 0;
                    try { code = _process.ExitCode; } catch { }
                    _done.Add(new Dictionary<string, object> {
                        ["size"] = _current.Size, ["seed"] = _current.Seed, ["exitCode"] = code, ["stopped"] = _stopping,
                        ["seconds"] = (int)(DateTime.Now - _started).TotalSeconds,
                    });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Finished {_current.Size} / {_current.Seed} ({(_stopping ? "stopped" : "exit code " + code)})");
                }
                _process = null;
                if (!_stopping) StartNext();
            }
        }

        public Dictionary<string, object> Status() {
            lock (_lock) {
                return new Dictionary<string, object> {
                    ["running"] = _process != null,
                    ["current"] = _current == null ? null : new Dictionary<string, object> {
                        ["size"] = _current.Size, ["seed"] = _current.Seed, ["seconds"] = (int)(DateTime.Now - _started).TotalSeconds,
                    },
                    ["queued"] = _queue.Select(x => new Dictionary<string, object> { ["size"] = x.Size, ["seed"] = x.Seed }).ToArray(),
                    ["done"] = _done.ToArray(),
                    ["log"] = Tail(_log, 400),
                };
            }
        }

        // Last lines of the server log, which RustDedicated keeps open for writing
        private static string[] Tail(string path, int lines) {
            if (path == null || !File.Exists(path)) return new string[0];
            try {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    long start = Math.Max(0, file.Length - 256 * 1024);
                    file.Seek(start, SeekOrigin.Begin);
                    var bytes = new byte[file.Length - start];
                    int read = file.Read(bytes, 0, bytes.Length);
                    var all = Encoding.UTF8.GetString(bytes, 0, read).Replace("\r", "").Split('\n');
                    return all.Where(x => x.Trim().Length > 0).Skip(Math.Max(0, all.Length - lines)).ToArray();
                }
            } catch { return new string[0]; }
        }
    }
}
