using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using CustomGenerator.Utility;
using Newtonsoft.Json.Linq;

namespace CustomGenerator
{
    public class ExtConfig {
        public static ConfigData Config;
        public static TempData tempData;
        private static readonly string CurrentVersion = "0.2.5";

        private static readonly string Location = Path.Combine("HarmonyConfig", "CustomGenerator.json");

        static ExtConfig() => LoadConfig();

        public class ConfigData {
            [JsonProperty("Language (en/ru)", Order = -2)]
            public string Language = DetectLanguage();

            [Loc("Map Settings", "Настройки Карты")]
            public MapSettings mapSettings = new();

            [Loc("Main Generator", "Основной Генератор")]
            public GeneratorSettings Generator = new();

            [Loc("Swap Monuments", "Замена Монументов")]
            public SwapSettings Swap = new();

            [Loc("Monuments", "Монументы")]
            public MonumentSettings Monuments = new();

            [Loc("Custom Monuments", "Кастомные Монументы")]
            public CustomMonumentSettings CustomMonuments = new();

            [JsonProperty(Order = -1)]
            public string Version = CurrentVersion;
        }
        public sealed class MapSettings {
            [Loc("Generate new map everytime", "Генерировать новую карту каждый раз")]
            public bool GenerateNewMapEverytime = true;
            [Loc("Override Map Sizes (9000 not be changed to 6000)", "Принудительный размер карты (карта 9000 не сменится на 6000)")]
            public bool OverrideSizes = true;
            [Loc("Override Map Folder (saves to <Server Root>/maps/)", "Перезаписать папку с картой (<папка сервера>/maps/)")]
            public bool OverrideFolder = true;
            [Loc("Override Map Name", "Перезаписать название карты")]
            public bool OverrideName = true;
            [Loc("Map Name ({0} - size, {1} - seed)", "Название карты ({0} - размер, {1} - сид)")]
            public string MapName = "CustomGenerator{0}_{1}";
        }

        public sealed class GeneratorSettings {
            public SimplePath Road = new();
            public SimplePath Rail = new();
            public UniqueEnviroment UniqueEnviroment = new();

            [Loc("Remove Rivers", "Удалить реки")]
            public bool RemoveRivers = false;
            [Loc("River width scale (1 = default)", "Множитель ширины рек (1 = по умолчанию)")]
            public float RiverWidthScale = 1f;

            [Loc("Remove Car Wrecks around Road", "Удалить разбитые префабы машин около дороги")]
            public bool RemoveCarWrecks = false;
            [Loc("Allow building on road", "Разрешить строительство на дорогах")]
            public bool AllowRoadBuild = false;
            [Loc("Remove large powerlines", "Удалить большие ЛЭП")]
            public bool RemovePowerlines = false;
            [Loc("Remove tunnel entrances", "Удалить входы в туннели")]
            public bool RemoveTunnelsEntrances = false;

            [Loc("Remove underground tunnels (also removes entrances)", "Удалить подземные туннели (вместе со входами)")]
            public bool RemoveTunnels = false;

            [Loc("Change percentages", "Изменить проценты")]
            public bool ModifyPercentages = false;
            [Loc("Tier Percentages (100 in total)", "Проценты Тиров (всего 100)")]
            public TierSettings Tier = new ();
            [Loc("Biome Percentages (Arid+Temperate+Tundra+Arctic = 100, Jungle is separate)", "Проценты Биомов (Пустыня+Умеренный+Тундра+Арктика = 100, Джунгли отдельно)")]
            public BiomSettings Biom = new ();
        }

        public sealed class CustomMonumentSettings {
            [Loc("Enabled", "Включить")]
            public bool Enabled = false;
            [Loc("Folder with .map files (relative to server root)", "Папка с .map файлами (относительно папки сервера)")]
            public string Folder = "maps/custom";
            [Loc("List", "Список")]
            public List<CustomMonument> List = new();
        }

        public class CustomMonument {
            public bool Enabled = true;
            public string Name = "";
            // File in the custom monuments folder, e.g. "my_gas_station.map"
            public string File = "";
            public int Count = 1;

            // Footprint radius in meters, 0 = auto from prefab positions
            public float Radius = 0f;
            // Width of the transition ring between the monument terrain and the world, meters
            public float Blend = 25f;
            // Stamp = terrain heights from the .map, Flatten = flat pad, None = keep world terrain
            public string HeightMode = "Stamp";
            public bool CopySplat = false;
            public bool CopyTopology = false;
            // Terrain holes (e.g. bunker entrances)
            public bool CopyAlpha = true;
            public bool RandomRotation = true;

            // Placement checks on the world terrain before stamping
            public float MaxHeightDifference = 15f;
            public float MinHeight = 2f;
            public float MaxHeight = 150f;
            public int MinDistanceToMonuments = 150;
            public int MinDistanceSameType = 500;
            public SpawnFilterCfg Filter = new SpawnFilterCfg();
        }

        public sealed class SwapSettings {
            [Loc("Enabled", "Включить")]
            public bool Enabled = false;
            [Loc("Save both maps (with swap and without)", "Сохранить обе карты (с заменой и без)")]
            public bool SaveBothMaps = false;
        }

        public class MonumentSettings
        {
            [Loc("Enabled", "Включить")]
            public bool Enabled = false;
            [Loc("MonumentList", "Лист монументов")]
            public List<Monument> monuments = new ();
        }

        public class Monument {
            public bool ShouldChange;
            public bool Generate;
            public string Description;
            public string Folder;

            public int MinWorldSize = 0;
            public int TargetCount = 0;

            [JsonConverter(typeof(StringEnumConverter))]
            public PlaceMonuments.DistanceMode distanceSame = PlaceMonuments.DistanceMode.Max;
            public int MinDistanceSameType = 500;

            [JsonConverter(typeof(StringEnumConverter))]
            public PlaceMonuments.DistanceMode distanceDifferent = PlaceMonuments.DistanceMode.Any;
            public int MinDistanceDifferentType = 0;

            public SpawnFilterCfg Filter = new SpawnFilterCfg();

            // Empty = vanilla folder. Path inside the game bundles (not on disk), comma separated, relative to assets/bundled/prefabs/autospawn/
            public string OverrideFolder = "";
            // Parts of prefab file names (without folder), e.g. "harbor_1". Include empty = all prefabs of the group
            public List<string> IncludePrefabs = new List<string>();
            public List<string> ExcludePrefabs = new List<string>();
            // Part of prefab name -> how many copies go to the candidate pool (0 = none)
            public Dictionary<string, int> PrefabCopies = new Dictionary<string, int>();
            // Vanilla scales TargetCount by world size (curve defined only up to 6000)
            public bool IgnoreWorldSizeMultiplier = false;

            [JsonIgnore]
            public bool HasPrefabRules => IncludePrefabs.Count > 0 || ExcludePrefabs.Count > 0 || PrefabCopies.Count > 0;
        }
        //private struct DistanceInfo {
        //    public float minDistanceSameType;
        //    public float maxDistanceSameType;
        //    public float minDistanceDifferentType;
        //    public float maxDistanceDifferentType;
        //    public float minDistanceDungeonEntrance;
        //    public float maxDistanceDungeonEntrance;
        //}
        public class SpawnFilterCfg
        {
            public bool Enabled = false;
            public List<string> SplatType = new List<string>();
            public List<string> BiomeType = new List<string>();
            public List<string> TopologyAny = new List<string>();
            public List<string> TopologyAll = new List<string>();
            public List<string> TopologyNot = new List<string>();
        }
        public class SimplePath {
            public bool ShouldChange = true;
            public bool Enabled = true;
            public bool GenerateRing = true;
            public bool GenerateSideMonuments = true;
            public bool GenerateSideObjects = false;
        }
        public class UniqueEnviroment {
            public bool ShouldChange = true;
            public bool GenerateOasis = true;
            public bool GenerateCanyons = true;
            public bool GenerateLakes = true;
        }

        public sealed class TierSettings {
            public float Tier0 = 30f;
            public float Tier1 = 30f;
            public float Tier2 = 40f;
        }

        public sealed class BiomSettings {
            public float Arid = 40f;
            public float Temperate = 15f;
            public float Tundra = 15f;
            public float Arctic = 30f;
            public float Jungle = 50f;
        }

        public sealed class TempData {
            public uint mapsize = 0;
            public uint mapseed = 0;
            public bool mapGenerated = false;
            public bool shouldGetMonuments = false;
            public TerrainTexturing terrainTexturing;
            public TerrainMeta terrainMeta;
            public TerrainPath terrainPath;
            public List<KeyValuePair<string, Vector3>> customMonuments = new();
        }

        private static JsonSerializerSettings SerializerSettings(string language) => new() {
            ContractResolver = new LocalizedContractResolver(language),
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.Indented,
        };

        private static string DetectLanguage() =>
            CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";

        private static void LoadConfig() {
            tempData = new TempData();

            if (!Directory.Exists("HarmonyConfig")) {
                Directory.CreateDirectory("HarmonyConfig");
                Logging.Info("Created HarmonyConfig directory");
            }

            if (!File.Exists(Location))  {
                Logging.Info("Config file not found, creating default configuration");
                LoadDefaultConfig();
                return;
            }

            try {
                string raw = File.ReadAllText(Location);
                string language = JObject.Parse(raw).Value<string>("Language (en/ru)") ?? DetectLanguage();

                // Keys in either language are accepted, missing keys keep their defaults
                Config = JsonConvert.DeserializeObject<ConfigData>(raw, SerializerSettings(language));

                if (Config.Version != CurrentVersion) {
                    string backupPath = Location + $".{Config.Version}.backup";
                    File.WriteAllText(backupPath, raw);
                    Logging.Config($"Config version {Config.Version} -> {CurrentVersion}, backup saved to {backupPath}");
                    Config.Version = CurrentVersion;
                }

                // Rewrites the file with new options and keys in the selected language
                SaveConfig();
                Logging.Config("Configuration loaded successfully");
            } catch (Exception ex) {
                Logging.Error("Failed to load configuration", ex);
                string brokenPath = Location + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(Location, brokenPath, true);
                Logging.Config($"Broken config saved to {brokenPath}, loading default configuration...");
                LoadDefaultConfig();
            }

            Validate();

            if (Config.Monuments.monuments.IsNullOrEmpty())
                tempData.shouldGetMonuments = true;
        }

        private static void LoadDefaultConfig() {
            try
            {
                Config = new ConfigData();
                SaveConfig();
                Logging.Config("Default configuration created successfully");
            }
            catch (Exception ex)
            {
                Logging.Error("Failed to create default configuration", ex);
            }
        }

        public static void SaveConfig() {
            try
            {
                File.WriteAllText(Location, JsonConvert.SerializeObject(Config, SerializerSettings(Config.Language)));
                Logging.Config("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                Logging.Error("Failed to save configuration", ex);
            }
        }

        // Fixes values the generator can't use. Changes are runtime-only, the file keeps what the user wrote.
        private static void Validate() {
            var gen = Config.Generator;

            if (Config.Language != "en" && Config.Language != "ru") {
                Logging.Warning($"Unknown language '{Config.Language}', expected 'en' or 'ru'");
            }

            try { string.Format(Config.mapSettings.MapName, 0, 0); }
            catch (FormatException) {
                Logging.Warning($"Map name '{Config.mapSettings.MapName}' has invalid placeholders (only {{0}} and {{1}} allowed), using default");
                Config.mapSettings.MapName = new MapSettings().MapName;
            }

            if (gen.RiverWidthScale <= 0f) {
                Logging.Warning($"River width scale must be > 0 (got {gen.RiverWidthScale}), using 1");
                gen.RiverWidthScale = 1f;
            }

            if (gen.ModifyPercentages) {
                float tiers = gen.Tier.Tier0 + gen.Tier.Tier1 + gen.Tier.Tier2;
                float biomes = gen.Biom.Arid + gen.Biom.Temperate + gen.Biom.Tundra + gen.Biom.Arctic;
                bool negative = new[] { gen.Tier.Tier0, gen.Tier.Tier1, gen.Tier.Tier2, gen.Biom.Arid, gen.Biom.Temperate, gen.Biom.Tundra, gen.Biom.Arctic, gen.Biom.Jungle }.Any(x => x < 0f);

                if (negative || tiers <= 0f || biomes <= 0f) {
                    Logging.Warning("Tier/biome percentages contain negative values or sum to 0, keeping vanilla percentages");
                    gen.ModifyPercentages = false;
                } else {
                    if (Mathf.Abs(tiers - 100f) > 0.01f)
                        Logging.Warning($"Tier percentages sum to {tiers}, not 100 - they will be scaled proportionally");
                    if (Mathf.Abs(biomes - 100f) > 0.01f)
                        Logging.Warning($"Biome percentages (without Jungle) sum to {biomes}, not 100 - they will be scaled proportionally");
                    if (gen.Biom.Jungle > 100f) {
                        Logging.Warning($"Jungle percentage {gen.Biom.Jungle} is over 100, using 100");
                        gen.Biom.Jungle = 100f;
                    }
                }
            }

            foreach (var monument in Config.Monuments.monuments) {
                string name = string.IsNullOrEmpty(monument.Description) ? monument.Folder : monument.Description;

                if (monument.TargetCount < 0 || monument.MinWorldSize < 0 || monument.MinDistanceSameType < 0 || monument.MinDistanceDifferentType < 0) {
                    Logging.Warning($"Monument '{name}': negative count/size/distance replaced with 0");
                    monument.TargetCount = Math.Max(0, monument.TargetCount);
                    monument.MinWorldSize = Math.Max(0, monument.MinWorldSize);
                    monument.MinDistanceSameType = Math.Max(0, monument.MinDistanceSameType);
                    monument.MinDistanceDifferentType = Math.Max(0, monument.MinDistanceDifferentType);
                }

                monument.OverrideFolder = (monument.OverrideFolder ?? "").Trim();
                monument.IncludePrefabs = (monument.IncludePrefabs ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).ToList();
                monument.ExcludePrefabs = (monument.ExcludePrefabs ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).ToList();
                var copies = new Dictionary<string, int>();
                foreach (var pair in monument.PrefabCopies ?? new Dictionary<string, int>()) {
                    if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                    if (pair.Value < 0) Logging.Warning($"Monument '{name}': negative copies for '{pair.Key}' replaced with 0");
                    copies[pair.Key.Trim().ToLowerInvariant()] = Math.Max(0, pair.Value);
                }
                monument.PrefabCopies = copies;

                var filter = monument.Filter ??= new SpawnFilterCfg();
                filter.SplatType = ValidEnumNames<TerrainSplat.Enum>(filter.SplatType, name, "SplatType");
                filter.BiomeType = ValidEnumNames<TerrainBiome.Enum>(filter.BiomeType, name, "BiomeType");
                filter.TopologyAny = ValidEnumNames<TerrainTopology.Enum>(filter.TopologyAny, name, "TopologyAny");
                filter.TopologyAll = ValidEnumNames<TerrainTopology.Enum>(filter.TopologyAll, name, "TopologyAll");
                filter.TopologyNot = ValidEnumNames<TerrainTopology.Enum>(filter.TopologyNot, name, "TopologyNot");
            }

            var custom = Config.CustomMonuments ??= new CustomMonumentSettings();
            custom.List ??= new List<CustomMonument>();
            if (string.IsNullOrWhiteSpace(custom.Folder)) custom.Folder = new CustomMonumentSettings().Folder;
            foreach (var monument in custom.List) {
                string name = string.IsNullOrEmpty(monument.Name) ? monument.File : monument.Name;

                if (string.IsNullOrWhiteSpace(monument.File)) {
                    Logging.Warning($"Custom monument '{name}': File is empty, disabled");
                    monument.Enabled = false;
                } else if (custom.Enabled && monument.Enabled && !File.Exists(Path.Combine(custom.Folder, monument.File))) {
                    Logging.Warning($"Custom monument '{name}': file {Path.Combine(custom.Folder, monument.File)} not found, disabled");
                    monument.Enabled = false;
                }

                var modes = new[] { "Stamp", "Flatten", "None" };
                string mode = modes.FirstOrDefault(x => string.Equals(x, monument.HeightMode?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (mode == null) Logging.Warning($"Custom monument '{name}': unknown HeightMode '{monument.HeightMode}' (valid: {string.Join(", ", modes)}), using Stamp");
                monument.HeightMode = mode ?? "Stamp";

                if (monument.Count < 0 || monument.Radius < 0 || monument.Blend < 0 || monument.MaxHeightDifference < 0 || monument.MinDistanceToMonuments < 0 || monument.MinDistanceSameType < 0) {
                    Logging.Warning($"Custom monument '{name}': negative values replaced with 0");
                    monument.Count = Math.Max(0, monument.Count);
                    monument.Radius = Math.Max(0, monument.Radius);
                    monument.Blend = Math.Max(0, monument.Blend);
                    monument.MaxHeightDifference = Math.Max(0, monument.MaxHeightDifference);
                    monument.MinDistanceToMonuments = Math.Max(0, monument.MinDistanceToMonuments);
                    monument.MinDistanceSameType = Math.Max(0, monument.MinDistanceSameType);
                }
                if (monument.MinHeight > monument.MaxHeight) {
                    Logging.Warning($"Custom monument '{name}': MinHeight > MaxHeight, values swapped");
                    (monument.MinHeight, monument.MaxHeight) = (monument.MaxHeight, monument.MinHeight);
                }

                var filter = monument.Filter ??= new SpawnFilterCfg();
                filter.SplatType = ValidEnumNames<TerrainSplat.Enum>(filter.SplatType, name, "SplatType");
                filter.BiomeType = ValidEnumNames<TerrainBiome.Enum>(filter.BiomeType, name, "BiomeType");
                filter.TopologyAny = ValidEnumNames<TerrainTopology.Enum>(filter.TopologyAny, name, "TopologyAny");
                filter.TopologyAll = ValidEnumNames<TerrainTopology.Enum>(filter.TopologyAll, name, "TopologyAll");
                filter.TopologyNot = ValidEnumNames<TerrainTopology.Enum>(filter.TopologyNot, name, "TopologyNot");
            }
        }

        private static List<string> ValidEnumNames<T>(List<string> values, string monument, string field) where T : struct, Enum {
            if (values == null) return new List<string>();
            var valid = new List<string>();
            foreach (var value in values) {
                if (Enum.TryParse(value?.Trim(), out T _)) valid.Add(value.Trim());
                else Logging.Warning($"Monument '{monument}': unknown {field} value '{value}' ignored (valid: {string.Join(", ", Enum.GetNames(typeof(T)))})");
            }
            return valid;
        }
    }

    // Localized JSON key: written in the config's language, read in either
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LocAttribute : Attribute {
        public readonly string En, Ru;
        public LocAttribute(string en, string ru) { En = en; Ru = ru; }
    }

    internal sealed class LocalizedContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver {
        private readonly bool _ru;
        public LocalizedContractResolver(string language) { _ru = language == "ru"; }

        protected override IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
            var properties = base.CreateProperties(type, memberSerialization);
            var aliases = new List<Newtonsoft.Json.Serialization.JsonProperty>();

            foreach (var property in properties) {
                var loc = property.AttributeProvider?.GetAttributes(typeof(LocAttribute), true).OfType<LocAttribute>().FirstOrDefault();
                if (loc == null) continue;

                property.PropertyName = _ru ? loc.Ru : loc.En;

                var alias = CreateProperty(type.GetMember(property.UnderlyingName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)[0], memberSerialization);
                alias.PropertyName = _ru ? loc.En : loc.Ru;
                alias.ShouldSerialize = _ => false;
                aliases.Add(alias);
            }

            foreach (var alias in aliases) properties.Add(alias);
            return properties;
        }
    }
}
