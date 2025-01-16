using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using static CustomGenerator.ExtConfig;
public class SwapMonument {
    private static WorldSerialization _mainMap = new WorldSerialization();
    private static WorldSerialization _swapMap = new WorldSerialization();
    private static List<Monument> monuments = new List<Monument>();
    private static string mapPath = string.Empty;

    public static void Initiate(string path) {
        mapPath = path;
        _mainMap.Load(mapPath);

        Log(_mainMap.world.prefabs.Count);
        LoadMonuments();
        SwapMonuments();

        if (!Config.Swap.SaveBothMaps)
            _mainMap.Save(mapPath);
        else _mainMap.Save(mapPath.Replace(".map", ".swapped.map"));
    }

    private static void SwapMonuments() {
        foreach (Monument monument in monuments) {
            var matchPrefabs = _mainMap.world.prefabs.Where(x => StringPool.Get(x.id).Contains(monument.prefabShortname)).ToList();

            // debug
            /*Log("-----");
            Log(monument.prefabShortname.ToString());
            Log(monument.path);
            Log(matchPrefabs.Count());*/
            // debug

            if (matchPrefabs.Count() == 0) continue;
            foreach (var firstfab in matchPrefabs) {
                _swapMap.Load(monument.path);
                _mainMap.world.prefabs.Remove(firstfab);
                _mainMap.world.prefabs.AddRange(
                    MapHander.CreatePrefabFromMap(firstfab.position, firstfab.rotation, _swapMap.world.prefabs)
                );
            }
        }
    }

    private static void LoadMonuments() {
        if (!Directory.Exists("maps/prefabs")) Directory.CreateDirectory("maps/prefabs");

        string[] files = Directory.GetFiles("maps/prefabs");
        foreach (string file in files) {
            if (!Path.GetFileName(file).EndsWith(".map")) continue;

            string prefabShortname = Path.GetFileNameWithoutExtension(file);
            monuments.Add(new Monument(prefabShortname, file));
        }
    }

    class Monument {
        public string prefabShortname;
        public string path;

        public Monument(string prefabShortname, string path) {
            this.prefabShortname = prefabShortname;
            this.path = path;
        }
    }

    static void Log(object obj) => Debug.Log("[SWAP MN] " + obj);
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
