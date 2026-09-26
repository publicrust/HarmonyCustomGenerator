using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomGenerator.Utility;
using HarmonyLib;
using ProtoBuf;
using UnityEngine;

using static CustomGenerator.ExtConfig;
namespace CustomGenerator.Custom
{
    // Monument made in RustEdit, saved either as .map (the first prefab is the anchor: original monument or SpawnPoint,
    // the terrain comes from the whole map) or as .prefab (pivot at 0,0,0, terrain in .prefab.heights/.splat0/... next to it).
    internal sealed class CustomMonumentData
    {
        public const uint SpawnPointId = 2749405185u;
        private const float HeightOffset = -500f, HeightScale = 1000f;

        public struct Item {
            public string Category;
            public uint Id;
            public Vector3 Local;       // x/z relative to the anchor, y = height above the ground under the anchor
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        public readonly List<Item> Items = new List<Item>();
        public float AutoRadius;
        public int UnknownPrefabs;
        public int BrokenPrefabs;

        private float _size;
        private Vector3 _anchorPos;
        private Quaternion _anchorRot;
        private int _heightRes, _splatRes, _topologyRes, _alphaRes;
        private short[] _heights;
        private byte[] _splat;
        private int[] _topology;
        private byte[] _alpha;
        private PrefabTerrain _prefabTerrain;

        public bool HasHeights => _heights != null || _prefabTerrain?.Heights != null;
        public bool HasSplat => _splat != null || _prefabTerrain?.Splat != null;
        public bool HasTopology => _topology != null || _prefabTerrain?.Topology != null;
        public bool HasAlpha => _alpha != null || _prefabTerrain?.Alpha != null;
        public float AnchorGround { get; private set; }

        public static CustomMonumentData Load(string path) {
            if (string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase))
                return LoadPrefab(path);

            var world = new WorldSerialization();
            world.Load(path);
            if (world.world.prefabs == null || world.world.prefabs.Count == 0)
                throw new InvalidDataException($"{path} has no prefabs (or failed to load)");

            var data = new CustomMonumentData { _size = world.world.size };
            var anchor = world.world.prefabs[0];
            data._anchorPos = ToVector(anchor.position);
            data._anchorRot = Quaternion.Euler(ToVector(anchor.rotation));

            data._heights = ToArray<short>(world.GetMap("height")?.data, 2, out data._heightRes);
            data._splat = ToArray<byte>(world.GetMap("splat")?.data, 1, out data._splatRes, TerrainSplat.COUNT);
            data._topology = ToArray<int>(world.GetMap("topology")?.data, 4, out data._topologyRes);
            data._alpha = ToArray<byte>(world.GetMap("alpha")?.data, 1, out data._alphaRes);

            data.AnchorGround = data.HasHeights ? data.SourceHeight(Vector2.zero) : data._anchorPos.y;
            if (float.IsNaN(data.AnchorGround)) data.AnchorGround = data._anchorPos.y;

            var inverse = Quaternion.Inverse(data._anchorRot);
            float radius = 0f;
            for (int i = 0; i < world.world.prefabs.Count; i++) {
                var prefab = world.world.prefabs[i];
                if (i == 0 && prefab.id == SpawnPointId) continue;
                if (string.IsNullOrEmpty(StringPool.Get(prefab.id))) { data.UnknownPrefabs++; continue; }
                if (!IsFinite(ToVector(prefab.position)) || !IsFinite(ToVector(prefab.rotation)) || !IsFinite(ToVector(prefab.scale))) { data.BrokenPrefabs++; continue; }

                Vector3 local = inverse * (ToVector(prefab.position) - data._anchorPos);
                local.y += data._anchorPos.y - data.AnchorGround;
                data.Items.Add(new Item {
                    Category = prefab.category,
                    Id = prefab.id,
                    Local = local,
                    Rotation = inverse * Quaternion.Euler(ToVector(prefab.rotation)),
                    Scale = ToVector(prefab.scale),
                });
                radius = Mathf.Max(radius, new Vector2(local.x, local.z).magnitude);
            }
            data.AutoRadius = Mathf.Max(20f, radius + 10f);
            return data;
        }

        // RustEdit "Save as prefab": uint version + LZ4 stream (same as .map) with a protobuf message whose field 3
        // is a repeated PrefabData. Positions are relative to the prefab pivot at (0, 0, 0), y = 0 is the ground. No terrain.
        private static CustomMonumentData LoadPrefab(string path) {
            byte[] payload;
            using (var file = File.OpenRead(path)) {
                var header = new byte[4];
                if (file.Read(header, 0, 4) != 4) throw new InvalidDataException($"{path} is too short");
                using var lz4 = new LZ4.LZ4Stream(file, LZ4.LZ4StreamMode.Decompress);
                using var memory = new MemoryStream();
                lz4.CopyTo(memory);
                payload = memory.ToArray();
            }

            var data = new CustomMonumentData { _anchorRot = Quaternion.identity };
            float radius = 0f;
            var reader = new ProtoReader(payload, 0, payload.Length);
            float terrainSize = 0f;
            while (reader.Next(out int field, out int wire)) {
                if (field == 1 && wire == 2) {
                    // Terrain settings, first value is the side of the captured terrain square in meters
                    var settings = reader.Message();
                    while (settings.Next(out int f, out int w)) {
                        if (f == 1 && w == 0) terrainSize = (float)settings.Varint(); else settings.Skip(w);
                    }
                    continue;
                }
                if (field != 3 || wire != 2) { reader.Skip(wire); continue; }
                var prefab = reader.Message();
                string category = ""; uint id = 0;
                Vector3 position = Vector3.zero, rotation = Vector3.zero, scale = Vector3.one;
                while (prefab.Next(out int f, out int w)) {
                    switch (f) {
                        case 1 when w == 2: category = prefab.String(); break;
                        case 2 when w == 0: id = (uint)prefab.Varint(); break;
                        case 3 when w == 2: position = prefab.Message().Vector(Vector3.zero); break;
                        case 4 when w == 2: rotation = prefab.Message().Vector(Vector3.zero); break;
                        case 5 when w == 2: scale = prefab.Message().Vector(Vector3.zero); break;
                        default: prefab.Skip(w); break;
                    }
                }
                if (id == SpawnPointId) continue;
                if (string.IsNullOrEmpty(StringPool.Get(id))) { data.UnknownPrefabs++; continue; }
                // RustEdit may save objects with NaN positions, RustEdit then fails to open the generated map
                if (!IsFinite(position) || !IsFinite(rotation) || !IsFinite(scale)) { data.BrokenPrefabs++; continue; }
                data.Items.Add(new Item { Category = category, Id = id, Local = position, Rotation = Quaternion.Euler(rotation), Scale = scale });
                radius = Mathf.Max(radius, new Vector2(position.x, position.z).magnitude);
            }
            if (data.Items.Count == 0 && data.UnknownPrefabs == 0) throw new InvalidDataException($"{path} has no prefabs");

            data.AnchorGround = 0f;
            data.AutoRadius = Mathf.Max(20f, radius + 10f);
            if (terrainSize > 0f) data._prefabTerrain = PrefabTerrain.Load(path, terrainSize);
            return data;
        }

        // Terrain saved by RustEdit next to a .prefab: RGBA PNGs covering a square of Size meters centered on the pivot,
        // top row = north (+z). splat0/splat1 = 8 splat channels, alpha = A channel, topology = int packed little-endian in RGBA,
        // heights = signed 16-bit offset from the pivot ground in R (low) and B (high).
        private sealed class PrefabTerrain
        {
            // Assumption: height offsets use the same units as the Rust height map (1000 m / short.MaxValue)
            private const float HeightUnit = 1000f / short.MaxValue;

            public float Size;
            public Grid<float> Heights, Alpha;
            public Grid<Vector4>[] Splat;
            public Grid<int> Topology;

            public static PrefabTerrain Load(string path, float size) {
                var terrain = new PrefabTerrain { Size = size };
                terrain.Heights = Png.Read(path + ".heights", p => (short)(p[0] | (p[2] << 8)) * HeightUnit);
                terrain.Alpha = Png.Read(path + ".alpha", p => p[3] / 255f);
                terrain.Topology = Png.Read(path + ".topology", p => p[0] | (p[1] << 8) | (p[2] << 16) | (p[3] << 24));
                var first = Png.Read(path + ".splat0", ToVector);
                var second = Png.Read(path + ".splat1", ToVector);
                if (first != null && second != null) terrain.Splat = new[] { first, second };
                return terrain;

                static Vector4 ToVector(byte[] p) => new Vector4(p[0], p[1], p[2], p[3]) / 255f;
            }

            // Pixel coordinates for an offset from the pivot, false outside the captured square
            public bool ToPixel<T>(Grid<T> grid, Vector2 offset, out float x, out float y) {
                float u = offset.x / Size + 0.5f, v = offset.y / Size + 0.5f;
                x = u * grid.Width - 0.5f;
                y = (1f - v) * grid.Height - 0.5f;
                return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
            }
        }

        private sealed class Grid<T>
        {
            public int Width, Height;
            public T[] Values;
            public T this[int x, int y] => Values[Mathf.Clamp(y, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1)];
        }

        // Minimal 8-bit RGBA PNG decoder (the files are tiny, no need for Unity textures)
        private static class Png
        {
            public static Grid<T> Read<T>(string path, Func<byte[], T> convert) {
                if (!File.Exists(path)) return null;
                byte[] file = File.ReadAllBytes(path);
                int width = 0, height = 0, position = 8;
                using var idat = new MemoryStream();
                while (position + 8 <= file.Length) {
                    int length = (file[position] << 24) | (file[position + 1] << 16) | (file[position + 2] << 8) | file[position + 3];
                    string type = System.Text.Encoding.ASCII.GetString(file, position + 4, 4);
                    if (type == "IHDR") {
                        width = (file[position + 8] << 24) | (file[position + 9] << 16) | (file[position + 10] << 8) | file[position + 11];
                        height = (file[position + 12] << 24) | (file[position + 13] << 16) | (file[position + 14] << 8) | file[position + 15];
                        if (file[position + 16] != 8 || file[position + 17] != 6) throw new InvalidDataException($"{path}: only 8-bit RGBA PNG is supported");
                    } else if (type == "IDAT") idat.Write(file, position + 8, length);
                    position += 12 + length;
                }

                byte[] raw;
                idat.Position = 2; // zlib header
                using (var inflate = new System.IO.Compression.DeflateStream(idat, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new MemoryStream()) { inflate.CopyTo(output); raw = output.ToArray(); }

                int stride = width * 4, offset = 0;
                var previous = new byte[stride];
                var grid = new Grid<T> { Width = width, Height = height, Values = new T[width * height] };
                var pixel = new byte[4];
                for (int y = 0; y < height; y++) {
                    byte filter = raw[offset++];
                    var line = new byte[stride];
                    Buffer.BlockCopy(raw, offset, line, 0, stride);
                    offset += stride;
                    for (int i = 0; i < stride; i++) {
                        int a = i >= 4 ? line[i - 4] : 0, b = previous[i], c = i >= 4 ? previous[i - 4] : 0;
                        switch (filter) {
                            case 1: line[i] = (byte)(line[i] + a); break;
                            case 2: line[i] = (byte)(line[i] + b); break;
                            case 3: line[i] = (byte)(line[i] + (a + b) / 2); break;
                            case 4:
                                int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                                line[i] = (byte)(line[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
                                break;
                        }
                    }
                    for (int x = 0; x < width; x++) {
                        Buffer.BlockCopy(line, x * 4, pixel, 0, 4);
                        grid.Values[y * width + x] = convert(pixel);
                    }
                    previous = line;
                }
                return grid;
            }
        }

        // Minimal protobuf reader for the .prefab format
        private struct ProtoReader
        {
            private readonly byte[] _buffer;
            private int _position;
            private readonly int _end;

            public ProtoReader(byte[] buffer, int start, int end) { _buffer = buffer; _position = start; _end = end; }

            public bool Next(out int field, out int wire) {
                field = wire = 0;
                if (_position >= _end) return false;
                ulong key = Varint();
                field = (int)(key >> 3); wire = (int)(key & 7);
                return true;
            }

            public ulong Varint() {
                ulong result = 0;
                for (int shift = 0; shift < 64; shift += 7) {
                    byte b = _buffer[_position++];
                    result |= (ulong)(b & 0x7f) << shift;
                    if ((b & 0x80) == 0) break;
                }
                return result;
            }

            public float Fixed32() { float v = BitConverter.ToSingle(_buffer, _position); _position += 4; return v; }

            public ProtoReader Message() {
                int length = (int)Varint();
                var sub = new ProtoReader(_buffer, _position, _position + length);
                _position += length;
                return sub;
            }

            public string String() {
                int length = (int)Varint();
                string s = System.Text.Encoding.UTF8.GetString(_buffer, _position, length);
                _position += length;
                return s;
            }

            // VectorData: 1 = x, 2 = y, 3 = z as fixed32 floats, missing fields are 0
            public Vector3 Vector(Vector3 value) {
                while (Next(out int f, out int w)) {
                    if (w != 5) { Skip(w); continue; }
                    float v = Fixed32();
                    if (f == 1) value.x = v; else if (f == 2) value.y = v; else if (f == 3) value.z = v;
                }
                return value;
            }

            public void Skip(int wire) {
                switch (wire) {
                    case 0: Varint(); break;
                    case 1: _position += 8; break;
                    case 2: int length = (int)Varint(); _position += length; break;
                    case 5: _position += 4; break;
                    default: throw new InvalidDataException($"Unsupported protobuf wire type {wire}");
                }
            }
        }

        // Terrain height of the source map at an offset (in the anchor frame) from the anchor, NaN outside the map
        public float SourceHeight(Vector2 offset) {
            if (_prefabTerrain != null) {
                var grid = _prefabTerrain.Heights;
                if (grid == null || !_prefabTerrain.ToPixel(grid, offset, out float px, out float py)) return float.NaN;
                int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
                float wx = px - ix, wy = py - iy;
                return AnchorGround + Mathf.Lerp(Mathf.Lerp(grid[ix, iy], grid[ix + 1, iy], wx), Mathf.Lerp(grid[ix, iy + 1], grid[ix + 1, iy + 1], wx), wy);
            }
            if (!ToSourceUV(offset, out float u, out float v)) return float.NaN;
            float fx = u * (_heightRes - 1), fz = v * (_heightRes - 1);
            int x0 = Mathf.Clamp((int)fx, 0, _heightRes - 2), z0 = Mathf.Clamp((int)fz, 0, _heightRes - 2);
            float tx = fx - x0, tz = fz - z0;
            float h00 = H(x0, z0), h10 = H(x0 + 1, z0), h01 = H(x0, z0 + 1), h11 = H(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);

            float H(int x, int z) => HeightOffset + _heights[z * _heightRes + x] / (float)short.MaxValue * HeightScale;
        }

        public bool SourceSplat(Vector2 offset, out Vector4 first, out Vector4 second) {
            first = second = Vector4.zero;
            if (_prefabTerrain != null) {
                var splat = _prefabTerrain.Splat;
                if (splat == null || !_prefabTerrain.ToPixel(splat[0], offset, out float px, out float py)) return false;
                int ix = Mathf.RoundToInt(px), iy = Mathf.RoundToInt(py);
                first = splat[0][ix, iy]; second = splat[1][ix, iy];
                return true;
            }
            if (!ToSourceUV(offset, out float u, out float v)) return false;
            int x = Mathf.Clamp((int)(u * _splatRes), 0, _splatRes - 1), z = Mathf.Clamp((int)(v * _splatRes), 0, _splatRes - 1);
            float S(int i) => _splat[(i * _splatRes + z) * _splatRes + x] / 255f;
            first = new Vector4(S(0), S(1), S(2), S(3));
            second = new Vector4(S(4), S(5), S(6), S(7));
            return true;
        }

        public int SourceTopology(Vector2 offset) {
            if (_prefabTerrain != null) {
                var grid = _prefabTerrain.Topology;
                if (grid == null || !_prefabTerrain.ToPixel(grid, offset, out float px, out float py)) return 0;
                return grid[Mathf.RoundToInt(px), Mathf.RoundToInt(py)];
            }
            if (!ToSourceUV(offset, out float u, out float v)) return 0;
            int x = Mathf.Clamp((int)(u * _topologyRes), 0, _topologyRes - 1), z = Mathf.Clamp((int)(v * _topologyRes), 0, _topologyRes - 1);
            return _topology[z * _topologyRes + x];
        }

        // Terrain opacity (0 = hole), NaN outside the source terrain
        public float SourceAlpha(Vector2 offset) {
            if (_prefabTerrain != null) {
                var grid = _prefabTerrain.Alpha;
                if (grid == null || !_prefabTerrain.ToPixel(grid, offset, out float px, out float py)) return float.NaN;
                return grid[Mathf.RoundToInt(px), Mathf.RoundToInt(py)];
            }
            if (_alpha == null || !ToSourceUV(offset, out float u, out float v)) return float.NaN;
            int x = Mathf.Clamp((int)(u * _alphaRes), 0, _alphaRes - 1), z = Mathf.Clamp((int)(v * _alphaRes), 0, _alphaRes - 1);
            return _alpha[z * _alphaRes + x] / 255f;
        }

        private bool ToSourceUV(Vector2 offset, out float u, out float v) {
            Vector3 world = _anchorPos + _anchorRot * new Vector3(offset.x, 0f, offset.y);
            u = (world.x + _size / 2f) / _size;
            v = (world.z + _size / 2f) / _size;
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        private static T[] ToArray<T>(byte[] bytes, int elementSize, out int res, int channels = 1) where T : struct {
            res = 0;
            if (bytes == null || bytes.Length == 0) return null;
            int count = bytes.Length / elementSize;
            res = (int)Math.Round(Math.Sqrt(count / (double)channels));
            if (res * res * channels != count) { res = 0; return null; }
            var result = new T[count];
            Buffer.BlockCopy(bytes, 0, result, 0, count * elementSize);
            return result;
        }

        private static bool IsFinite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                                                 && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        private static Vector3 ToVector(VectorData v) => v == null ? Vector3.zero : new Vector3(v.x, v.y, v.z);
    }

    // Runs right after the vanilla monuments: finds a spot, stamps the terrain and writes the prefabs to the map
    internal static class CustomMonumentPlacer
    {
        // Never place over these, whatever the filter says
        private const int BlockedTopology = TerrainTopology.OCEAN | TerrainTopology.OFFSHORE | TerrainTopology.RIVER | TerrainTopology.LAKE
                                          | TerrainTopology.MONUMENT | TerrainTopology.ROAD | TerrainTopology.RAIL;
        // World bits kept when the topology is copied from the .map
        private const int KeptTopology = TerrainTopology.TIER0 | TerrainTopology.TIER1 | TerrainTopology.TIER2 | (1 << 29) | (1 << 30);
        private const int Attempts = 20000;
        private const int Candidates = 16;

        private static readonly AccessTools.FieldRef<TerrainPath, List<MonumentInfo>> _monuments = AccessTools.FieldRefAccess<TerrainPath, List<MonumentInfo>>("Monuments");

        private struct Placed { public string Name; public Vector3 Position; public float Radius; }

        private static bool _done;

        public static void Run(uint seed, string after) {
            var settings = Config.CustomMonuments;
            if (_done || World.Networked || settings == null || !settings.Enabled) return;
            _done = true;
            if (!settings.List.Any(x => x.Enabled && x.Count > 0)) return;
            Logging.Generation($"Custom monuments: placing after '{after}'");
            var placed = new List<Placed>();

            foreach (var cfg in settings.List.Where(x => x.Enabled && x.Count > 0)) {
                string name = string.IsNullOrEmpty(cfg.Name) ? Path.GetFileNameWithoutExtension(cfg.File) : cfg.Name;
                CustomMonumentData data;
                try {
                    data = CustomMonumentData.Load(Path.Combine(settings.Folder, cfg.File));
                } catch (Exception ex) {
                    Logging.Error($"Custom monument '{name}': failed to load {cfg.File}", ex);
                    continue;
                }
                if (data.UnknownPrefabs > 0)
                    Logging.Warning($"Custom monument '{name}': {data.UnknownPrefabs} prefabs are unknown to this Rust version and were skipped");
                if (data.BrokenPrefabs > 0)
                    Logging.Warning($"Custom monument '{name}': {data.BrokenPrefabs} prefabs have NaN/infinite position, rotation or scale in the file and were skipped");

                float radius = cfg.Radius > 0f ? cfg.Radius : data.AutoRadius;
                int done = 0;
                for (int i = 0; i < cfg.Count; i++) {
                    if (!TryFindSpot(cfg, name, radius, placed, ref seed, out Vector3 position, out float baseHeight)) break;
                    float angle = cfg.RandomRotation ? SeedRandom.Range(ref seed, 0f, 360f) : 0f;

                    Stamp(cfg, data, position, baseHeight, angle, radius);
                    WritePrefabs(data, position, baseHeight, angle);

                    placed.Add(new Placed { Name = name, Position = position, Radius = radius });
                    tempData.customMonuments.Add(new KeyValuePair<string, Vector3>(name, position));
                    done++;
                    Logging.Generation($"Custom monument '{name}' #{done}: {position.x:0}, {position.z:0} (height {baseHeight:0.0}, rotation {angle:0})");
                }
                Logging.Generation($"Custom monument '{name}': placed {done}/{cfg.Count}, radius {radius:0}m, {data.Items.Count} prefabs each");
            }
        }

        private static bool TryFindSpot(CustomMonument cfg, string name, float radius, List<Placed> placed, ref uint seed, out Vector3 best, out float bestHeight) {
            best = Vector3.zero; bestHeight = 0f;
            var heightMap = TerrainMeta.HeightMap;
            var topologyMap = TerrainMeta.TopologyMap;
            var monuments = _monuments(TerrainMeta.Path);

            float outer = radius + cfg.Blend;
            float margin = outer + 50f;
            Vector3 origin = TerrainMeta.Position, size = TerrainMeta.Size;
            if (size.x <= margin * 2f || size.z <= margin * 2f) return false;

            int notMask = MaskOf(cfg.Filter, "TopologyNot");
            float bestScore = float.MaxValue;
            int found = 0;

            for (int attempt = 0; attempt < Attempts && found < Candidates; attempt++) {
                var center = new Vector3(
                    SeedRandom.Range(ref seed, origin.x + margin, origin.x + size.x - margin), 0f,
                    SeedRandom.Range(ref seed, origin.z + margin, origin.z + size.z - margin));
                float centerHeight = heightMap.GetHeight(center);
                if (centerHeight < cfg.MinHeight || centerHeight > cfg.MaxHeight) continue;
                if (!FilterAllows(cfg.Filter, center)) continue;

                if (monuments.Any(m => m != null && Distance2D(m.transform.position, center) < cfg.MinDistanceToMonuments + radius)) continue;
                if (placed.Any(p => Distance2D(p.Position, center) < p.Radius + outer
                                 || Distance2D(p.Position, center) < (p.Name == name ? cfg.MinDistanceSameType : cfg.MinDistanceToMonuments))) continue;

                // Footprint: flat enough, on land, nothing blocking. The blend ring must stay out of water and other monuments too.
                float min = float.MaxValue, max = float.MinValue, sum = 0f;
                int samples = 0;
                bool blocked = false;
                for (int ring = 0; ring <= 4 && !blocked; ring++) {
                    float r = ring == 4 ? outer : radius * ring / 3f;
                    int steps = ring == 0 ? 1 : 12;
                    for (int s = 0; s < steps; s++) {
                        float a = s * Mathf.PI * 2f / steps;
                        var p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        int topology = topologyMap.GetTopology(p);
                        if ((topology & (ring == 4 ? TerrainTopology.OCEAN | TerrainTopology.MONUMENT | TerrainTopology.RIVER : BlockedTopology | notMask)) != 0) { blocked = true; break; }
                        if (ring == 4) continue;
                        float h = heightMap.GetHeight(p);
                        if (h < 0.5f) { blocked = true; break; }
                        min = Mathf.Min(min, h); max = Mathf.Max(max, h); sum += h; samples++;
                    }
                }
                if (blocked || max - min > cfg.MaxHeightDifference) continue;

                found++;
                float score = max - min;
                if (score < bestScore) { bestScore = score; best = center; bestHeight = sum / samples; }
            }

            if (found == 0) Logging.Warning($"Custom monument '{name}': no suitable spot found (try lowering distances, MaxHeightDifference limits or the filter)");
            best.y = bestHeight;
            return found > 0;
        }

        private static void Stamp(CustomMonument cfg, CustomMonumentData data, Vector3 center, float baseHeight, float angle, float radius) {
            float outer = radius + cfg.Blend;
            var inverse = Quaternion.Inverse(Quaternion.Euler(0f, angle, 0f));
            Vector3 origin = TerrainMeta.Position, size = TerrainMeta.Size;

            float Weight(float distance) => distance <= radius ? 1f : cfg.Blend <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, 1f - (distance - radius) / cfg.Blend);
            Vector2 Local(Vector3 world) { var l = inverse * (world - center); return new Vector2(l.x, l.z); }

            if (cfg.HeightMode != "None") {
                var map = TerrainMeta.HeightMap;
                int res = Res(map);
                bool stamp = cfg.HeightMode == "Stamp" && data.HasHeights;
                ForEachPixel(center, outer, res, res - 1, (x, z, world, distance) => {
                    float target = baseHeight;
                    if (stamp) {
                        float source = data.SourceHeight(Local(world));
                        if (!float.IsNaN(source)) target = baseHeight + (source - data.AnchorGround);
                    }
                    // GetHeight returns meters, SetHeight takes the normalized 0..1 value
                    map.SetHeight(x, z, TerrainMeta.NormalizeY(Mathf.Lerp(map.GetHeight(x, z), target, Weight(distance))));
                });
            }

            if (cfg.CopySplat && data.HasSplat) {
                var map = TerrainMeta.SplatMap;
                int res = Res(map);
                ForEachPixel(center, outer, res, res, (x, z, world, distance) => {
                    if (data.SourceSplat(Local(world), out var first, out var second))
                        map.SetSplatRaw(x, z, first, second, Weight(distance));
                });
            }

            if (cfg.CopyAlpha && data.HasAlpha) {
                var map = TerrainMeta.AlphaMap;
                int res = Res(map);
                ForEachPixel(center, outer, res, res, (x, z, world, distance) => {
                    float source = data.SourceAlpha(Local(world));
                    if (!float.IsNaN(source)) map.SetAlpha(x, z, Mathf.Lerp(map.GetAlpha(x, z), source, Weight(distance)));
                });
            }

            // Mark the footprint as a monument so roads, rails, cliffs and decor keep away
            var topologyMap = TerrainMeta.TopologyMap;
            int topologyRes = Res(topologyMap);
            ForEachPixel(center, radius, topologyRes, topologyRes, (x, z, world, distance) => {
                int topology = topologyMap.GetTopology(x, z);
                if (cfg.CopyTopology && data.HasTopology)
                    topology = (topology & KeptTopology) | (data.SourceTopology(Local(world)) & ~KeptTopology);
                topologyMap.SetTopology(x, z, topology | TerrainTopology.MONUMENT);
            });
        }

        private static void WritePrefabs(CustomMonumentData data, Vector3 center, float baseHeight, float angle) {
            var rotation = Quaternion.Euler(0f, angle, 0f);
            foreach (var item in data.Items) {
                Vector3 offset = rotation * new Vector3(item.Local.x, 0f, item.Local.z);
                var position = new Vector3(center.x + offset.x, baseHeight + item.Local.y, center.z + offset.z);
                World.Serialization.AddPrefab(item.Category, item.Id, position, rotation * item.Rotation, item.Scale);
            }
        }

        // Calls action for every map pixel within radius of center. Height maps have res = cells + 1 (pixels on cell corners).
        private static void ForEachPixel(Vector3 center, float radius, int res, int cells, Action<int, int, Vector3, float> action) {
            Vector3 origin = TerrainMeta.Position, size = TerrainMeta.Size;
            float offset = cells == res ? 0.5f : 0f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - origin.x) / size.x * cells), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + radius - origin.x) / size.x * cells), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - radius - origin.z) / size.z * cells), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + radius - origin.z) / size.z * cells), 0, res - 1);
            for (int z = z0; z <= z1; z++) {
                for (int x = x0; x <= x1; x++) {
                    var world = new Vector3(origin.x + (x + offset) / cells * size.x, 0f, origin.z + (z + offset) / cells * size.z);
                    float distance = Distance2D(world, center);
                    if (distance <= radius) action(x, z, world, distance);
                }
            }
        }

        private static bool FilterAllows(SpawnFilterCfg filter, Vector3 position) {
            if (filter == null || !filter.Enabled) return true;
            if (filter.SplatType.Count > 0 && (TerrainMeta.SplatMap.GetSplatMaxType(position) & MaskOf(filter, "SplatType")) == 0) return false;
            if (filter.BiomeType.Count > 0 && (TerrainMeta.BiomeMap.GetBiomeMaxType(position) & MaskOf(filter, "BiomeType")) == 0) return false;
            int topology = TerrainMeta.TopologyMap.GetTopology(position);
            if (filter.TopologyAny.Count > 0 && (topology & MaskOf(filter, "TopologyAny")) == 0) return false;
            int all = MaskOf(filter, "TopologyAll");
            if (filter.TopologyAll.Count > 0 && (topology & all) != all) return false;
            return true;
        }

        private static int MaskOf(SpawnFilterCfg filter, string field) {
            if (filter == null || !filter.Enabled) return 0;
            var values = field switch {
                "SplatType" => filter.SplatType, "BiomeType" => filter.BiomeType,
                "TopologyAny" => filter.TopologyAny, "TopologyAll" => filter.TopologyAll, _ => filter.TopologyNot,
            };
            return values.Count == 0 ? 0 : Convert.ToInt32(EnumParser.GetFilterEnum(field, values));
        }

        private static int Res(object map) => (int)AccessTools.Field(map.GetType(), "res").GetValue(map);
        private static float Distance2D(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }

    // All procedural steps are components of a single WorldSetup object, so a new step can't be put in the middle.
    // Run right after the vanilla "Main Monuments" step instead, or before rails/roads if that step isn't found.
    [HarmonyPatch]
    internal static class PlaceMonuments_RunCustomMonuments
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlaceMonuments), nameof(PlaceMonuments.Process)); }
        private static void Postfix(PlaceMonuments __instance, uint seed) {
            if (__instance.Description == "Main Monuments") CustomMonumentPlacer.Run(seed ^ 0x5eed, __instance.Description);
        }
    }

    [HarmonyPatch]
    internal static class PathGenerators_RunCustomMonuments
    {
        private static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.Method(typeof(GenerateRailRing), "Process");
            yield return AccessTools.Method(typeof(GenerateRailLayout), "Process");
            yield return AccessTools.Method(typeof(GenerateRoadRing), "Process");
            yield return AccessTools.Method(typeof(GenerateRoadLayout), "Process");
        }
        private static void Prefix(ProceduralComponent __instance, uint seed) {
            CustomMonumentPlacer.Run(seed ^ 0x5eed, "fallback: before " + __instance.Description);
        }
    }
}
