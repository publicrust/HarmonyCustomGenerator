using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using CustomGenerator.Utility;

using static CustomGenerator.ExtConfig;
public class SwapMonument {
    private const string Folder = "maps/prefabs";

    public static void Initiate(string path) {
        var mainMap = new WorldSerialization();
        mainMap.Load(path);
        if (mainMap.world?.prefabs == null || mainMap.world.prefabs.Count == 0) {
            Logging.Error($"Swap: failed to load the saved map {path}, swap skipped");
            return;
        }

        var files = LoadMonuments();
        if (files.Count == 0) {
            Logging.Warning($"Swap: no .map files in {Path.GetFullPath(Folder)}, nothing to swap");
            return;
        }

        int replaced = SwapMonuments(mainMap, files);
        string target = Config.Swap.SaveBothMaps ? Path.ChangeExtension(path, ".swapped.map") : path;
        mainMap.Save(target);
        Logging.Info($"Swap: {replaced} monuments replaced, saved to {target}");
        GenerationReport.SwapSaved(target);
    }

    private static int SwapMonuments(WorldSerialization mainMap, List<Monument> files) {
        // Matches come from the original prefabs, so prefabs inserted by one swap are never swapped again
        var original = mainMap.world.prefabs.ToList();
        int total = 0;

        foreach (Monument monument in files) {
            string file = Path.GetFileName(monument.path);
            var matches = original.Where(x => (StringPool.Get(x.id) ?? "").ToLowerInvariant().Contains(monument.prefabShortname)).ToList();
            if (matches.Count == 0) {
                Logging.Warning($"Swap: {file}: no '{monument.prefabShortname}' on the map, skipped (is the file named <vanilla prefab>.prefab.map?)");
                GenerationReport.Swap(file, 0, "no such monument on the map");
                continue;
            }

            var swapMap = new WorldSerialization();
            try { swapMap.Load(monument.path); }
            catch (Exception ex) { Logging.Error($"Swap: {file}: failed to load", ex); GenerationReport.Swap(file, 0, "failed to load the file"); continue; }
            if (swapMap.world?.prefabs == null || swapMap.world.prefabs.Count == 0) {
                Logging.Error($"Swap: {file}: no prefabs in the file");
                GenerationReport.Swap(file, 0, "no prefabs in the file");
                continue;
            }

            foreach (var prefab in matches) {
                mainMap.world.prefabs.Remove(prefab);
                mainMap.world.prefabs.AddRange(MapHander.CreatePrefabFromMap(prefab.position, prefab.rotation, swapMap.world.prefabs));
            }
            total += matches.Count;
            Logging.Info($"Swap: {file}: replaced {matches.Count} x '{monument.prefabShortname}' ({swapMap.world.prefabs.Count} prefabs each)");
            GenerationReport.Swap(file, matches.Count);
        }
        return total;
    }

    private static List<Monument> LoadMonuments() {
        if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

        return Directory.GetFiles(Folder)
            .Where(file => file.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
            .Select(file => new Monument(Path.GetFileNameWithoutExtension(file).ToLowerInvariant(), file))
            .ToList();
    }

    class Monument {
        public string prefabShortname;
        public string path;

        public Monument(string prefabShortname, string path) {
            this.prefabShortname = prefabShortname;
            this.path = path;
        }
    }
}


public class MapHander
{
    private static PrefabData CreatePrefab(uint PrefabID, VectorData position, VectorData rotation, VectorData scale, string category = "Monument")
    {
        var prefab = new PrefabData()
        {
            category = category,
            id = PrefabID,
            position = position,
            rotation = rotation,
            scale = scale
        };
        return prefab;
    }

    private static VectorData CalculateLocalPos(VectorData placePos, VectorData globalPos, VectorData rotation) => RotateVector(new VectorData(globalPos.x - placePos.x, globalPos.y - placePos.y, globalPos.z - placePos.z), rotation);

    private static VectorData RotateVector(VectorData vector, VectorData rotation) {
        float radX = rotation.x * (float)Math.PI / 180.0f;
        float radY = rotation.y * (float)Math.PI / 180.0f;
        float radZ = rotation.z * (float)Math.PI / 180.0f;

        float cosX = (float)Math.Cos(radX), sinX = (float)Math.Sin(radX);
        float cosY = (float)Math.Cos(radY), sinY = (float)Math.Sin(radY);
        float cosZ = (float)Math.Cos(radZ), sinZ = (float)Math.Sin(radZ);

        float newY = vector.y * cosX - vector.z * sinX;
        float newZ = vector.y * sinX + vector.z * cosX;
        vector.y = newY;
        vector.z = newZ;

        float newX = vector.x * cosY + vector.z * sinY;
        newZ = vector.z * cosY - vector.x * sinY;
        vector.x = newX;
        vector.z = newZ;

        newX = vector.x * cosZ - vector.y * sinZ;
        newY = vector.x * sinZ + vector.y * cosZ;
        vector.x = newX;
        vector.y = newY;

        return vector;
    }

    public static List<PrefabData> CreatePrefabFromMap(VectorData startPos, VectorData rotation, List<PrefabData> prefabs)
    {
        List<PrefabData> createdPrefabs = new List<PrefabData>();
        bool first = true;
        foreach (var prefab in prefabs) {
            createdPrefabs.Add(
                CreatePrefab(
                    (prefab.id == 2749405185u) ? 504351302u : prefab.id,
                    Calculate(startPos, prefab.position, prefab.scale, prefabs, rotation),
                    first ? rotation : CalculateRot(rotation, prefab.rotation),
                    (prefab.id == 2749405185u) ? new VectorData(0, 0, 0) : prefab.scale,
                    prefab.category
            ));
            first = false;
        }
        return createdPrefabs;
    }

    private static VectorData Calculate(VectorData globalPos, VectorData position, VectorData scale, List<PrefabData> prefabs, VectorData firstPrefabRotation) {
        VectorData localPos = CalculateLocalPos(prefabs[0].position, position, firstPrefabRotation);
        return new VectorData(globalPos.x + localPos.x, globalPos.y + localPos.y, globalPos.z + localPos.z);
    }

    private static VectorData CalculateRot(VectorData globalRot, VectorData localRot) => new VectorData(globalRot.x + localRot.x, globalRot.y + localRot.y, globalRot.z + localRot.z);
}
