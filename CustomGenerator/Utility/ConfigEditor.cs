using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace CustomGenerator.Utility
{
    // Writes HarmonyConfig/CustomGenerator.editor.html: the config editor page (embedded resource) with the schema,
    // the current config and the last run inlined, so it opens with a double click, offline, and always matches this mod version.
    // The launcher serves the same page and feeds it the same data over HTTP.
    internal static class ConfigEditor
    {
        private const string Resource = "ConfigEditor.html";
        private const string Token = "__CG_DATA__";
        private const string SwapFolder = "maps/prefabs";

        private static string _path, _configJson, _customFolder;
        private static JObject _schema;

        public static void Write(string path, JObject schema, string configJson, string customFolder) {
            _path = path; _schema = schema; _configJson = configJson; _customFolder = customFolder;
            Refresh();
        }

        // Rewrites the page with the last saved config, e.g. after generation to include the new last run
        public static void Refresh() {
            if (_path == null) return;
            string template;
            using (var stream = typeof(ConfigEditor).Assembly.GetManifestResourceStream(Resource)) {
                if (stream == null) { Logging.Warning($"Config editor template '{Resource}' is missing from the mod, editor not written"); return; }
                using var reader = new StreamReader(stream);
                template = reader.ReadToEnd();
            }

            var data = new JObject {
                ["schema"] = _schema,
                ["config"] = JObject.Parse(_configJson),
                ["savedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ["lastRun"] = ReadJson(GenerationReport.LastRunLocation),
                ["prefabs"] = ReadJson(GenerationReport.PrefabsLocation),
                ["files"] = new JObject {
                    ["custom"] = ListFiles(_customFolder, ".map", ".prefab"),
                    ["swap"] = ListFiles(SwapFolder, ".map"),
                },
            };
            // "</" would end the <script> block the data sits in, "<\/" is the same string in JSON
            string inline = data.ToString(Formatting.None).Replace("</", "<\\/");
            File.WriteAllText(_path, template.Replace(Token, inline));
        }

        private static JToken ReadJson(string path) {
            try { return File.Exists(path) ? JToken.Parse(File.ReadAllText(path)) : null; }
            catch (Exception ex) { Logging.Warning($"Config editor: can't read {path}: {ex.Message}"); return null; }
        }

        private static JArray ListFiles(string folder, params string[] extensions) {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return new JArray();
            return new JArray(Directory.GetFiles(folder)
                .Where(x => extensions.Any(e => x.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName).OrderBy(x => x));
        }
    }
}
