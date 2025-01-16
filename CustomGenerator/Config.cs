using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CustomGenerator.Utility;
using CustomGenerator.Utilities;

namespace CustomGenerator
{
    public class ExtConfig {
        public const bool EN = true;
        public static ConfigData Config;
        public static TempData tempData;
        private static string CurrentVersion = "0.1.0";

        private static readonly string Location = Path.Combine("HarmonyConfig", "CustomGeneratorCFG.json");

        static ExtConfig() {
            LoadConfig();
        }

        public class ConfigData {
            [JsonProperty(EN ? "Skip Asset Warmup" : "Пропустить Asset Warmup")]
            public bool SkipAssetWarmup = true;

            [JsonProperty(EN ? "Map Settings" : "Настройки Карты")]
            public MapSettings mapSettings = new MapSettings();

            [JsonProperty(EN ? "Main Generator" : "Основной Генератор")]
            public GeneratorSettings Generator = new GeneratorSettings();

            [JsonProperty(EN ? "Swap Monuments" : "Замена Монументов")]
            public SwapSettings Swap = new SwapSettings();

            [JsonProperty(EN ? "Monuments" : "Монументы")]
            public MonumentSettings Monuments = new MonumentSettings();

            public string Version = CurrentVersion;
        }
        public sealed class MapSettings {
            [JsonProperty(EN ? "Generate new map everytime" : "Генерировать новую карту каждый раз")]
            public bool GenerateNewMapEverytime = true;
            [JsonProperty(EN ? "Override Map Sizes (9000 not be changed to 6000)" : "Принудительный размер карты (карта 9000 не сменится на 6000)")]
            public bool OverrideSizes = true;
            [JsonProperty(EN ? "Override Map Folder (saves to <Server Root>/maps/)" : "Перезаписать папку с картой (<папка сервера>/maps/)")]
            public bool OverrideFolder = true;
            [JsonProperty(EN ? "Override Map Name" : "Перезаписать название карты")]
            public bool OverrideName = true;
            [JsonProperty(EN ? "Map Name ({0} - size, {1} - seed)" : "Название карты ({0} - размер, {1} - сид)")]
            public string MapName = "Map{0}_{1}.CGEN";
        }

        public sealed class GeneratorSettings {
            public SimplePath Road = new SimplePath();
            public SimplePath Rail = new SimplePath();
            public UniqueEnviroment UniqueEnviroment = new UniqueEnviroment();

            [JsonProperty(EN ? "Remove Car Wrecks around Road" : "Удалить разбитые префабы машин около дороги")]
            public bool RemoveCarWrecks = false;
            [JsonProperty(EN ? "Remove Rivers" : "Удалить реки")]
            public bool RemoveRivers = false;
            //[JsonProperty("Remove large powerlines")]
            //public bool RemovePowerlines = false;
            [JsonProperty(EN ? "Remove tunnel entrances" : "Удалить входы в туннели")]
            public bool RemoveTunnelsEntrances = false;

            //[JsonProperty("Remove underground tunnels")]
            //public bool RemoveTunnels = false;

            [JsonProperty(EN ? "Change percentages" : "Изменить проценты")]
            public bool ModifyPercentages = false;
            [JsonProperty(EN ? "Tier Percentages (100 in total)" : "Проценты Тиров (всего 100)")]
            public TierSettings Tier = new TierSettings();
            [JsonProperty(EN ? "Bioms Percentages (100 in total)" : "Проценты Биомов (всего 100)")]
            public BiomSettings Biom = new BiomSettings();
        }

        public sealed class SwapSettings {
            [JsonProperty(EN ? "Enabled" : "Включить")]
            public bool Enabled = false;
            [JsonProperty(EN ? "Save both maps (with swap and without)" : "Сохранить обе карты (с заменой и без)")]
            public bool SaveBothMaps = false;
        }

        public class MonumentSettings
        {
            [JsonProperty(EN ? "Enabled" : "Включить")]
            public bool Enabled = false;
            [JsonProperty(EN ? "MonumentList" : "Лист монументов")]
            public List<Monument> monuments = new List<Monument>();
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
        }

        public sealed class TempData {
            public uint mapsize = 0;
            public uint mapseed = 0;
            public bool mapGenerated = false;
            public bool shouldGetMonuments = false;
            public TerrainTexturing terrainTexturing;
            public TerrainMeta terrainMeta;
            public TerrainPath terrainPath;
        }

        private static void LoadConfig() {
            tempData = new TempData();

            if (!Directory.Exists("HarmonyConfig")) 
            {
                Directory.CreateDirectory("HarmonyConfig");
                Logging.Info("Created HarmonyConfig directory");
            }
            
            if (!File.Exists(Location)) 
            {
                Logging.Info("Config file not found, creating default configuration");
                LoadDefaultConfig();
                return;
            }

            try {
                var oldConfig = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(Location));

                if (oldConfig.Version != CurrentVersion) {
                    Logging.Config($"Version mismatch! Old: {oldConfig.Version}, Current: {CurrentVersion}");
                    Logging.Config("Creating backup and migrating settings...");
                    
                    string backupPath = Location + $".{oldConfig.Version}.backup";
                    File.WriteAllText(backupPath, JsonConvert.SerializeObject(oldConfig, Formatting.Indented));
                    Logging.Config($"Backup created at: {backupPath}");
                    
                    Config = new ConfigData();
                    Config.SkipAssetWarmup = oldConfig.SkipAssetWarmup;
                    
                    if (oldConfig.mapSettings != null) {
                        Config.mapSettings.GenerateNewMapEverytime = oldConfig.mapSettings.GenerateNewMapEverytime;
                        Config.mapSettings.OverrideSizes = oldConfig.mapSettings.OverrideSizes;
                        Config.mapSettings.OverrideFolder = oldConfig.mapSettings.OverrideFolder;
                        Config.mapSettings.OverrideName = oldConfig.mapSettings.OverrideName;
                        Config.mapSettings.MapName = oldConfig.mapSettings.MapName;
                        Logging.Config("Map settings migrated");
                    }
                    
                    if (oldConfig.Generator != null) {
                        Config.Generator.Road = oldConfig.Generator.Road;
                        Config.Generator.Rail = oldConfig.Generator.Rail;
                        Config.Generator.UniqueEnviroment = oldConfig.Generator.UniqueEnviroment;
                        Config.Generator.RemoveCarWrecks = oldConfig.Generator.RemoveCarWrecks;
                        Config.Generator.RemoveRivers = oldConfig.Generator.RemoveRivers;
                        Config.Generator.RemoveTunnelsEntrances = oldConfig.Generator.RemoveTunnelsEntrances;
                        Config.Generator.ModifyPercentages = oldConfig.Generator.ModifyPercentages;
                        Config.Generator.Tier = oldConfig.Generator.Tier;
                        Config.Generator.Biom = oldConfig.Generator.Biom;
                        Logging.Config("Generator settings migrated");
                    }
                    
                    if (oldConfig.Swap != null) {
                        Config.Swap.Enabled = oldConfig.Swap.Enabled;
                        Config.Swap.SaveBothMaps = oldConfig.Swap.SaveBothMaps;
                        Logging.Config("Swap settings migrated");
                    }
                    
                    if (oldConfig.Monuments != null) {
                        Config.Monuments.Enabled = oldConfig.Monuments.Enabled;
                        Config.Monuments.monuments = oldConfig.Monuments.monuments;
                        Logging.Config("Monument settings migrated");
                    }

                    SaveConfig();
                    Logging.Config("Settings migration completed successfully");
                } else {
                    Config = oldConfig;
                    Logging.Config("Configuration loaded successfully");
                }

                if (Config.Monuments.monuments.IsNullOrEmpty()) 
                    tempData.shouldGetMonuments = true;
            } catch (Exception ex) {
                Logging.Error("Failed to load configuration", ex);
                Logging.Config("Loading default configuration...");
                LoadDefaultConfig();
            }
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
                File.WriteAllText(Location, JsonConvert.SerializeObject(Config, Formatting.Indented));
                Logging.Config("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                Logging.Error("Failed to save configuration", ex);
            }
        }
    }
}