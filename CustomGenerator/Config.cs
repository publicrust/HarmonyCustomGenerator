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
        private static readonly string SchemaLocation = Path.Combine("HarmonyConfig", "CustomGenerator.schema.json");

        static ExtConfig() => LoadConfig();

        public class ConfigData {
            // Lets editors (VS Code etc.) pick up the schema written next to the config
            [JsonProperty("$schema", Order = -3)]
            public string SchemaPath = "./" + Path.GetFileName(SchemaLocation);

            [JsonProperty("Language (en/ru)", Order = -2)]
            [Desc("Config language. Change it and restart the server: keys are rewritten in this language, values are kept",
                  "Язык конфига. Измените и перезапустите сервер: ключи перепишутся на этом языке, значения сохранятся")]
            [Schema(Values = new[] { "en", "ru" })]
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
            [Desc("Mod version the config was written by. Don't edit", "Версия мода, которой записан конфиг. Не изменяйте")]
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
            [Desc("Roads: ring road and roadside monuments/objects", "Дороги: кольцевая дорога и придорожные монументы/объекты")]
            public SimplePath Road = new();
            [Desc("Rails: ring rail and railside monuments", "Железная дорога: кольцо и монументы у железной дороги")]
            public SimplePath Rail = new();
            [Desc("Oases, canyons and lakes on any map size", "Оазисы, каньоны и озёра на любом размере карты")]
            public UniqueEnviroment UniqueEnviroment = new();

            [Loc("Remove Rivers", "Удалить реки")]
            public bool RemoveRivers = false;
            [Loc("River width scale (1 = default)", "Множитель ширины рек (1 = по умолчанию)")]
            [Schema(Min = 0)]
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
            [Desc("Place this monument", "Размещать этот монумент")]
            public bool Enabled = true;
            [Desc("Name for the log, the file name is used if empty", "Имя для лога, если пусто - используется имя файла")]
            public string Name = "";
            [Desc("File in the custom monuments folder, e.g. \"my_gas_station.map\" or \"my_gas_station.prefab\"",
                  "Файл в папке кастомных монументов, например \"my_gas_station.map\" или \"my_gas_station.prefab\"")]
            public string File = "";
            [Desc("How many copies to place (fewer if there is no room)", "Сколько копий разместить (меньше, если не хватит места)")]
            [Schema(Min = 0)]
            public int Count = 1;

            [Desc("Footprint radius in meters, 0 = auto from prefab positions", "Радиус площадки в метрах, 0 = автоматически по префабам")]
            [Schema(Min = 0)]
            public float Radius = 0f;
            [Desc("Width of the transition ring between the monument terrain and the world, meters",
                  "Ширина переходного кольца между рельефом монумента и миром, в метрах")]
            [Schema(Min = 0)]
            public float Blend = 25f;
            [Desc("Stamp = terrain heights from the file, Flatten = flat pad, None = keep world terrain",
                  "Stamp = рельеф из файла, Flatten = ровная площадка, None = оставить рельеф мира")]
            [Schema(Values = new[] { "Stamp", "Flatten", "None" })]
            public string HeightMode = "Stamp";
            [Desc("Copy ground textures from the file", "Копировать текстуры земли из файла")]
            public bool CopySplat = false;
            [Desc("Copy topology from the file", "Копировать топологию из файла")]
            public bool CopyTopology = false;
            [Desc("Copy terrain holes (e.g. bunker entrances)", "Копировать дыры в рельефе (например, входы в бункеры)")]
            public bool CopyAlpha = true;
            [Desc("Rotate every copy randomly", "Поворачивать каждую копию случайно")]
            public bool RandomRotation = true;

            [Desc("Max height difference of the world terrain under the footprint, meters", "Макс. перепад высот рельефа мира под площадкой, в метрах")]
            [Schema(Min = 0)]
            public float MaxHeightDifference = 15f;
            [Desc("Min terrain height (above sea level) at the center", "Мин. высота рельефа (над уровнем моря) в центре")]
            public float MinHeight = 2f;
            [Desc("Max terrain height (above sea level) at the center", "Макс. высота рельефа (над уровнем моря) в центре")]
            public float MaxHeight = 150f;
            [Desc("Min distance to any other monument, meters", "Мин. расстояние до любого другого монумента, в метрах")]
            [Schema(Min = 0)]
            public int MinDistanceToMonuments = 150;
            [Desc("Min distance between copies of this monument, meters", "Мин. расстояние между копиями этого монумента, в метрах")]
            [Schema(Min = 0)]
            public int MinDistanceSameType = 500;
            [Desc("Where the monument may stand", "Где может стоять монумент")]
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
            [Desc("true = apply the settings below, false = keep the group vanilla", "true = применить настройки ниже, false = оставить группу стандартной")]
            public bool ShouldChange;
            [Desc("false = don't generate the group at all (needs ShouldChange: true)", "false = не генерировать группу вообще (нужен ShouldChange: true)")]
            public bool Generate;
            [Desc("Group name (for the log)", "Название группы (для лога)")]
            public string Description;
            [Desc("Group path in the bundles, the group is found by it. Don't change, use OverrideFolder",
                  "Путь группы в бандлах, по нему ищется группа. Не изменяйте, используйте OverrideFolder")]
            public string Folder;

            [Desc("Min map size for the group to appear, 0 = any", "Мин. размер карты, на котором появляется группа, 0 = любой")]
            [Schema(Min = 0)]
            public int MinWorldSize = 0;
            [Desc("How many monuments to place, 0 = every prefab of the group", "Сколько монументов разместить, 0 = все префабы группы")]
            [Schema(Min = 0)]
            public int TargetCount = 0;

            [JsonConverter(typeof(StringEnumConverter))]
            [Desc("Placement preference relative to the same group: Max = far, Min = close, Any = no preference",
                  "Предпочтение размещения относительно своей группы: Max = подальше, Min = поближе, Any = без разницы")]
            public PlaceMonuments.DistanceMode distanceSame = PlaceMonuments.DistanceMode.Max;
            [Desc("Min distance to monuments of the same group, meters", "Мин. расстояние до монументов своей группы, в метрах")]
            [Schema(Min = 0)]
            public int MinDistanceSameType = 500;

            [JsonConverter(typeof(StringEnumConverter))]
            [Desc("Placement preference relative to other groups: Max = far, Min = close, Any = no preference",
                  "Предпочтение размещения относительно других групп: Max = подальше, Min = поближе, Any = без разницы")]
            public PlaceMonuments.DistanceMode distanceDifferent = PlaceMonuments.DistanceMode.Any;
            [Desc("Min distance to monuments of other groups, meters", "Мин. расстояние до монументов других групп, в метрах")]
            [Schema(Min = 0)]
            public int MinDistanceDifferentType = 0;

            [Desc("Where the monument may stand", "Где может стоять монумент")]
            public SpawnFilterCfg Filter = new SpawnFilterCfg();

            [Desc("Different path inside the game bundles (not on disk), relative to assets/bundled/prefabs/autospawn/, comma separated. Empty = vanilla",
                  "Другой путь в бандлах игры (не на диске), относительно assets/bundled/prefabs/autospawn/, через запятую. Пусто = стандартный")]
            public string OverrideFolder = "";
            [Desc("Keep only prefabs whose name (without folder) contains one of these strings, e.g. \"harbor_1\". Empty = all",
                  "Оставить только префабы, в имени которых (без папки) есть одна из строк, например \"harbor_1\". Пусто = все")]
            public List<string> IncludePrefabs = new List<string>();
            [Desc("Remove prefabs whose name contains one of these strings", "Убрать префабы, в имени которых есть одна из строк")]
            public List<string> ExcludePrefabs = new List<string>();
            [Desc("\"part of name\": N - how many copies of the prefab go to the candidate pool, 0 = none",
                  "\"часть имени\": N - сколько копий префаба попадёт в пул кандидатов, 0 = ни одной")]
            [Schema(Min = 0)]
            public Dictionary<string, int> PrefabCopies = new Dictionary<string, int>();
            [Desc("true = place exactly TargetCount (vanilla scales it by map size, curve defined only up to 6000)",
                  "true = ставить ровно TargetCount (игра умножает его на коэффициент размера карты, заданный только до 6000)")]
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
            [Desc("true = use this filter instead of the vanilla one", "true = использовать этот фильтр вместо стандартного")]
            public bool Enabled = false;
            [Desc("Allowed ground textures, empty = any", "Разрешённые текстуры земли, пусто = любые")]
            [Schema(Enum = typeof(TerrainSplat.Enum))]
            public List<string> SplatType = new List<string>();
            [Desc("Allowed biomes, empty = any", "Разрешённые биомы, пусто = любые")]
            [Schema(Enum = typeof(TerrainBiome.Enum))]
            public List<string> BiomeType = new List<string>();
            [Desc("At least one of these topologies, empty = any", "Хотя бы одна из этих топологий, пусто = любая")]
            [Schema(Enum = typeof(TerrainTopology.Enum))]
            public List<string> TopologyAny = new List<string>();
            [Desc("All of these topologies, empty = no condition", "Все эти топологии, пусто = без условия")]
            [Schema(Enum = typeof(TerrainTopology.Enum))]
            public List<string> TopologyAll = new List<string>();
            [Desc("None of these topologies, empty = no condition", "Ни одной из этих топологий, пусто = без условия")]
            [Schema(Enum = typeof(TerrainTopology.Enum))]
            public List<string> TopologyNot = new List<string>();
        }
        public class SimplePath {
            [Desc("Master switch: false = vanilla, other fields are ignored", "Главный переключатель: false = как в игре, остальные поля игнорируются")]
            public bool ShouldChange = true;
            [Desc("The ring. false = no ring on any map size", "Кольцо. false = без кольца на любом размере карты")]
            public bool Enabled = true;
            [Desc("Generate the ring on any map size (vanilla: large maps only)", "Генерировать кольцо на любом размере карты (в игре - только на больших)")]
            public bool GenerateRing = true;
            [Desc("Roadside/railside monuments: gas stations, supermarkets, stations, etc.", "Монументы у дороги/железной дороги: заправки, супермаркеты, станции и т.д.")]
            public bool GenerateSideMonuments = true;
            [Desc("Road only: roadside objects. Does nothing for Rail", "Только для Road: придорожные объекты. Для Rail ни на что не влияет")]
            public bool GenerateSideObjects = false;
        }
        public class UniqueEnviroment {
            [Desc("Master switch: false = vanilla (only on maps 4000-4500+)", "Главный переключатель: false = как в игре (только на картах от 4000-4500)")]
            public bool ShouldChange = true;
            public bool GenerateOasis = true;
            public bool GenerateCanyons = true;
            public bool GenerateLakes = true;
        }

        public sealed class TierSettings {
            [Schema(Min = 0)] public float Tier0 = 30f;
            [Schema(Min = 0)] public float Tier1 = 30f;
            [Schema(Min = 0)] public float Tier2 = 40f;
        }

        public sealed class BiomSettings {
            [Schema(Min = 0)] public float Arid = 40f;
            [Schema(Min = 0)] public float Temperate = 15f;
            [Schema(Min = 0)] public float Tundra = 15f;
            [Schema(Min = 0)] public float Arctic = 30f;
            [Desc("Separate from the others, 0-100", "Отдельно от остальных, 0-100")]
            [Schema(Min = 0, Max = 100)] public float Jungle = 50f;
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
            SaveSchema();
        }

        // Regenerated on every save, so it always matches the mod version and the config's language
        private static void SaveSchema() {
            try {
                var schema = ConfigSchema.Build(typeof(ConfigData), new LocalizedContractResolver(Config.Language), Config.Language == "ru");
                File.WriteAllText(SchemaLocation, schema.ToString(Formatting.Indented));
            } catch (Exception ex) {
                Logging.Error("Failed to save config schema", ex);
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
