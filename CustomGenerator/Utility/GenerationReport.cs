using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomGenerator.Utility
{
    // Collects what happened during generation and prints it as one block at the end,
    // to the log and to <map name>.report.txt next to the map. The same data goes to
    // HarmonyConfig/CustomGenerator.lastrun.json for the config editor's "Last run" tab.
    internal static class GenerationReport
    {
        public static readonly string LastRunLocation = Path.Combine("HarmonyConfig", "CustomGenerator.lastrun.json");
        public static readonly string PrefabsLocation = Path.Combine("HarmonyConfig", "CustomGenerator.prefabs.json");

        private sealed class Group { public string Name, Folder; public int Target; public List<string> Prefabs; }
        private sealed class Custom { public string Name; public int Placed, Count; public string Note; }
        private sealed class Swapped { public string File; public int Replaced; public string Note; }

        private static readonly List<Group> Groups = new List<Group>();
        private static readonly List<Custom> CustomMonuments = new List<Custom>();
        private static readonly List<Swapped> Swaps = new List<Swapped>();
        private static readonly Dictionary<string, SortedSet<string>> GroupPrefabs = new Dictionary<string, SortedSet<string>>();
        private static string _swapSavedTo, _imagePath;

        public static int Warnings, Errors;

        // Folder of the monument group being generated right now, null outside of PlaceMonuments
        public static string CurrentFolder;

        // target: TargetCount from the config if the group was changed, otherwise -1
        public static void MonumentGroup(string name, string folder, int target, IEnumerable<string> prefabs) =>
            Groups.Add(new Group { Name = name, Folder = folder, Target = target, Prefabs = prefabs.ToList() });

        // Prefab names the current group can choose from
        public static void PrefabNames(IEnumerable<string> names) {
            if (CurrentFolder == null) return;
            if (!GroupPrefabs.TryGetValue(CurrentFolder, out var set)) GroupPrefabs[CurrentFolder] = set = new SortedSet<string>();
            set.UnionWith(names);
        }

        public static void CustomMonument(string name, int placed, int count, string note = null) =>
            CustomMonuments.Add(new Custom { Name = name, Placed = placed, Count = count, Note = note });

        public static void Swap(string file, int replaced, string note = null) =>
            Swaps.Add(new Swapped { File = file, Replaced = replaced, Note = note });

        public static void SwapSaved(string path) => _swapSavedTo = path;
        public static void Image(string path) => _imagePath = path;

        public static void Write(string mapPath) {
            var elapsed = DateTime.Now - Process.GetCurrentProcess().StartTime;
            string report = BuildText(mapPath, elapsed);
            foreach (var line in report.Split('\n')) Logging.Info(line.TrimEnd('\r'));
            try {
                string reportPath = Path.ChangeExtension(Path.GetFullPath(mapPath), ".report.txt");
                File.WriteAllText(reportPath, report.Replace("\r\n", "\n").Replace("\n", Environment.NewLine) + Environment.NewLine);
                Logging.Info($"Report saved to {reportPath}");
            } catch (Exception ex) {
                Logging.Error("Failed to save the report", ex);
            }

            try {
                File.WriteAllText(LastRunLocation, BuildJson(mapPath, elapsed, report).ToString(Formatting.Indented));
                SavePrefabNames();
                ConfigEditor.Refresh();
            } catch (Exception ex) {
                Logging.Error("Failed to save the last run data for the config editor", ex);
            }
        }

        private static string BuildText(string mapPath, TimeSpan elapsed) {
            var text = new StringBuilder();
            text.AppendLine("===== CustomGenerator report =====");
            text.AppendLine($"Map:   {Path.GetFullPath(mapPath)}");
            text.AppendLine($"Image: {_imagePath ?? "not rendered"}");
            text.AppendLine($"Size:  {World.Size}, seed {World.Seed}");
            text.AppendLine($"Time:  {Format(elapsed)} since server start");

            if (Groups.Count > 0) {
                text.AppendLine();
                text.AppendLine("Monuments:");
                int width = Groups.Max(x => x.Name.Length);
                foreach (var group in Groups) {
                    string count = group.Target >= 0 ? $"{group.Prefabs.Count}/{group.Target}" : group.Prefabs.Count.ToString();
                    string names = string.Join(", ", group.Prefabs.GroupBy(x => x).Select(x => x.Count() > 1 ? $"{x.Key} x{x.Count()}" : x.Key));
                    text.AppendLine($"  {group.Name.PadRight(width)}  {count,5}  {names}".TrimEnd());
                }
            }

            if (CustomMonuments.Count > 0) {
                text.AppendLine();
                text.AppendLine("Custom monuments:");
                int width = CustomMonuments.Max(x => x.Name.Length);
                foreach (var custom in CustomMonuments)
                    text.AppendLine($"  {custom.Name.PadRight(width)}  {custom.Placed + "/" + custom.Count,5}  {custom.Note}".TrimEnd());
            }

            if (Swaps.Count > 0 || _swapSavedTo != null) {
                text.AppendLine();
                text.AppendLine("Swap:");
                int width = Swaps.Count > 0 ? Swaps.Max(x => x.File.Length) : 0;
                foreach (var swap in Swaps)
                    text.AppendLine($"  {swap.File.PadRight(width)}  {swap.Replaced,3} replaced  {swap.Note}".TrimEnd());
                if (_swapSavedTo != null) text.AppendLine($"  Saved to {_swapSavedTo}");
            }

            text.AppendLine();
            text.AppendLine($"Warnings: {Warnings}, errors: {Errors}{(Warnings + Errors > 0 ? $" - see {Logging.LogFilePath}" : "")}");
            text.Append("==================================");
            return text.ToString();
        }

        private static JObject BuildJson(string mapPath, TimeSpan elapsed, string text) => new JObject {
            ["finishedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            ["map"] = Relative(mapPath),
            ["image"] = _imagePath == null ? null : Relative(_imagePath),
            ["swapMap"] = _swapSavedTo == null ? null : Relative(_swapSavedTo),
            ["size"] = World.Size,
            ["seed"] = World.Seed,
            ["seconds"] = (int)elapsed.TotalSeconds,
            ["groups"] = new JArray(Groups.Select(x => new JObject {
                ["name"] = x.Name, ["folder"] = x.Folder, ["target"] = x.Target, ["prefabs"] = new JArray(x.Prefabs) })),
            ["custom"] = new JArray(CustomMonuments.Select(x => new JObject {
                ["name"] = x.Name, ["placed"] = x.Placed, ["count"] = x.Count, ["note"] = x.Note })),
            ["swap"] = new JArray(Swaps.Select(x => new JObject {
                ["file"] = x.File, ["replaced"] = x.Replaced, ["note"] = x.Note })),
            ["swapFiles"] = SwapFiles(),
            ["warnings"] = Warnings,
            ["errors"] = Errors,
            ["log"] = Relative(Logging.LogFilePath),
            ["text"] = text,
        };

        // Swap files and whether a prefab with that name exists in this Rust version at all
        private static JArray SwapFiles() {
            var result = new JArray();
            if (!Directory.Exists("maps/prefabs")) return result;
            var paths = PrefabPaths();
            foreach (var file in Directory.GetFiles("maps/prefabs", "*.map")) {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                result.Add(new JObject { ["file"] = Path.GetFileName(file), ["known"] = paths.Count == 0 ? null : (JToken)paths.Any(x => x.Contains(name)) });
            }
            return result;
        }

        // All prefab paths of the game: StringPool's path -> id dictionary, found by type since its name differs between versions
        private static List<string> PrefabPaths() {
            var field = typeof(StringPool).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .FirstOrDefault(x => x.FieldType == typeof(Dictionary<string, uint>));
            return (field?.GetValue(null) as Dictionary<string, uint>)?.Keys.ToList() ?? new List<string>();
        }

        // Accumulated over runs: a group only reports its names when it's generated
        private static void SavePrefabNames() {
            var all = File.Exists(PrefabsLocation) ? JObject.Parse(File.ReadAllText(PrefabsLocation)) : new JObject();
            foreach (var group in GroupPrefabs) {
                var names = new SortedSet<string>(group.Value);
                if (all[group.Key] is JArray known) names.UnionWith(known.Values<string>());
                all[group.Key] = new JArray(names);
            }
            File.WriteAllText(PrefabsLocation, all.ToString(Formatting.Indented));
        }

        // Relative to the server folder with forward slashes, so the editor page can link it
        private static string Relative(string path) {
            string full = Path.GetFullPath(path), root = Path.GetFullPath(".").TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full).Replace('\\', '/');
        }

        private static string Format(TimeSpan time) => time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}m {time.Seconds}s" : $"{time.Minutes}m {time.Seconds}s";
    }
}
