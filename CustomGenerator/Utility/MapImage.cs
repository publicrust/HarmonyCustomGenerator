using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;
using System.Net;
using HarmonyLib;
using UnityEngine;

using Color = UnityEngine.Color;
using Font = System.Drawing.Font;
using Graphics = System.Drawing.Graphics;
using SDFontStyle = System.Drawing.FontStyle;

using static CustomGenerator.ExtConfig;
using CustomGenerator.Utility;
using System.Diagnostics;
namespace CustomGenerator.Utility {
    // я честно не ебу что тут понаписал, но работает
    // upd. я ебу
    static class MapImage
    {
        private static Dictionary<string, string> RequirementResources = new Dictionary<string, string>() {
            {"PermanentMarker.ttf", "https://raw.githubusercontent.com/hammzat/HarmonyCustomGenerator/main/Resources/PermanentMarker.ttf"},
            {"dinpro.otf", "https://raw.githubusercontent.com/hammzat/HarmonyCustomGenerator/main/Resources/dinpro.otf"},
            {"dinprobold.otf", "https://raw.githubusercontent.com/hammzat/HarmonyCustomGenerator/main/Resources/dinprobold.otf"},
        };
        private static void CheckResources() {
            if (!Directory.Exists("mapimages")) Directory.CreateDirectory("mapimages");
            if (!Directory.Exists("mapimages/resources")) Directory.CreateDirectory("mapimages/resources");

            string path = "mapimages/resources";
            foreach (var resource in RequirementResources) {
                if (File.Exists(Path.Combine(path, resource.Key))) continue;

                using (var client = new WebClient()) {
                    Logging.Info($"DEPS - Downloading `{resource.Key}`...");
                    try {
                        client.DownloadFile(resource.Value, Path.Combine(path, resource.Key));
                    }
                    catch (Exception ex) {
                        Logging.Error($"DEPS - Error whilst downloading: {ex.Message} \nTry moving file from the `Resources` repository folder to the `mapimages/resources/`");
                    }
                }
            }
        }
        public static void RenderMap(float scale = 0.5f, int oceanMargin = 500) {
            CheckResources();

            byte[] array = MapImageRender.Render(out int num, out int num2, out Color color, scale, false, false, 350);
            if (array == null) {
                Logging.Error("MapImageGenerator returned null!"); return;
            }

            string mapName = string.Format(Config.mapSettings.MapName, tempData.mapsize, tempData.mapseed).Replace(".map", "");
            string fullPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, $"mapimages/{mapName}.png"));
            File.WriteAllBytes(fullPath, array);
            Logging.Info("Generated Map image: /mapimages/");
            Logging.Info(string.Format("Map saved to {0}", Config.mapSettings.OverrideFolder ? "/maps/" : "original map folder"));
        }
    }

    // Original Facepunch Code && MJSU plugin - Rust Map Api 
    public static class MapImageRender {
        private static readonly string PermanentMarkerFont = "mapimages/resources/PermanentMarker.ttf";
        private static readonly string DinProFont = "mapimages/resources/dinpro.otf";
        private static readonly string DinProFontBold = "mapimages/resources/dinprobold.otf";
        private static readonly Vector4 StartColor = new Vector4(0.286274523f, 23f / 85f, 0.247058839f, 1f);
        private static readonly Vector4 WaterColor = new Vector4(0.16941601f, 0.317557573f, 0.362000018f, 1f);
        private static readonly Vector4 GravelColor = new Vector4(0.25f, 37f / 152f, 0.220394745f, 1f);
        private static readonly Vector4 DirtColor = new Vector4(0.6f, 0.479594618f, 0.33f, 1f);
        private static readonly Vector4 SandColor = new Vector4(0.7f, 0.65968585f, 0.5277487f, 1f);
        private static readonly Vector4 GrassColor = new Vector4(0.354863644f, 0.37f, 0.2035f, 1f);
        private static readonly Vector4 ForestColor = new Vector4(0.248437509f, 0.3f, 9f / 128f, 1f);
        private static readonly Vector4 RockColor = new Vector4(0.4f, 0.393798441f, 0.375193775f, 1f);
        private static readonly Vector4 SnowColor = new Vector4(0.862745166f, 0.9294118f, 0.941176534f, 1f);
        private static readonly Vector4 PebbleColor = new Vector4(7f / 51f, 0.2784314f, 0.2761563f, 1f);
        private static readonly Vector4 OffShoreColor = new Vector4(0.04090196f, 0.220600322f, 14f / 51f, 1f);
        private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(0.95f, 2.87f, 2.37f));
        private const float SunPower = 0.65f;
        private const float Brightness = 1.05f;
        private const float Contrast = 0.94f;
        private const float OceanWaterLevel = 0f;
        private static readonly Vector4 Half = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
        private static Array2D<Color> generatedMap;
        private static Array2D<Color> generatedMapIcons;
        private static int width;
        private static int height;
        public readonly struct Array2D<T> {
            private readonly T[] _items;
            private readonly int _width;
            private readonly int _height;

            public ref T this[int x, int y] {
                get {
                    int num = Mathf.Clamp(x, 0, _width - 1);
                    int num2 = Mathf.Clamp(y, 0, _height - 1);
                    return ref _items[num2 * _width + num];
                }
            }

            public Array2D(T[] items, int width, int height) {
                _items = items;
                _width = width;
                _height = height;
            }

            public Bitmap ToBitmap()
            {
                Bitmap bitmap = new Bitmap(_width, _height);

                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        Color color = (Color)(object)this[x, y];
                        bitmap.SetPixel(x, y, color.ToSystemDrawingColor());
                    }
                }

                return bitmap;
            }

            public bool IsEmpty()
            {
                return _items == null || _width == 0 && _height == 0;
            }

            public Array2D<T> Clone()
            {
                return new Array2D<T>((T[])_items.Clone(), _width, _height);
            }

        }

        private class MapMonument
        {
            public string name;
            public int x = 0;
            public int y = 0;

            public Indication indication = Indication.None;
            public string imagePath = "";
        }
        private enum Indication
        {
            None = 0,
            Regular,
            Smaller,
            Image
        }
        private static FieldInfo _monuments = AccessTools.TypeByName("TerrainPath").GetField("Monuments", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static void LoadIcons(ref Array2D<Color> output, int imageWidth, int imageHeight, int mapResolution, int oceanMargin) {
            List<MonumentInfo> monuments = (List<MonumentInfo>)_monuments.GetValue(tempData.terrainPath);
            Logging.Info("3/4 | Proceeding map data...");

            var originalMap = mapResolution + oceanMargin;
            var originalMapOffset = imageWidth - originalMap;

            List<MapMonument> mapMonuments = new List<MapMonument>();
            foreach (MonumentInfo monument in monuments)
            {
                string name = GetMonumentName(monument);
                
                Vector3 position = monument.transform.position;
                //Debug.Log(name);

                int x = (int)(((position.x + (tempData.mapsize / 2.0)) / tempData.mapsize) * mapResolution) + originalMapOffset;
                int z = (int)(((position.z + (tempData.mapsize / 2.0)) / tempData.mapsize) * mapResolution) + originalMapOffset;

                if (name.ToLower().Contains("train")) { mapMonuments.Add(new MapMonument { name = name, x = x, y = z, indication = Indication.Image }); continue; } //

                if (monument.shouldDisplayOnMap && monument.mapIcon == null)
                    mapMonuments.Add(new MapMonument { name = name, x = x, y = z, indication = Indication.Regular });
                else
                    mapMonuments.Add(new MapMonument { name = name, x = x, y = z, indication = Indication.None });

                //RenderText(name, PermanentMarkerFont 9, System.Drawing.Color.Black, ref output, x, z);
            }

            //RenderDebug(ref output, imageWidth, originalMapOffset, originalMap);
            RenderMonument(mapMonuments, PermanentMarkerFont, ref output);
            RenderGithub(DinProFontBold, ref output, mapResolution, imageWidth);
        }
        private static void RenderDebug(ref Array2D<Color> output, int imageRes, int originalMapOffset, int originalMap) {
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    output[i, j] = Color.black;
                    output[imageRes - i, imageRes - j] = Color.cyan;

                    output[imageRes - i, j] = Color.magenta;
                    output[i, imageRes - j] = Color.grey;

                    output[originalMapOffset + i, originalMapOffset + j] = Color.black;
                    output[originalMap + i, originalMap + j] = Color.cyan;
                }
            }
        }
        private static void RenderText(string text, string fontPath, int fontSize, System.Drawing.Color color, ref Array2D<Color> output, int xx, int zz)
        {
            Bitmap bitmap = output.ToBitmap();
            PrivateFontCollection fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(fontPath);
            Font font = new Font(fontCollection.Families[0], fontSize);

            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush brush = new SolidBrush(color)) {
                    SizeF textSize = graphics.MeasureString(text, font);
                    float textX = xx - (textSize.Width / 2);
                    float textY = zz - (textSize.Height / 2);

                    graphics.TranslateTransform(textX, textY);
                    graphics.RotateTransform(180);
                    graphics.ScaleTransform(-1, 1);
                    graphics.DrawString(text, font, brush, 0, -textSize.Height);
                }
            }

            int width = bitmap.Width;
            int height = bitmap.Height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var px = bitmap.GetPixel(x, y);
                    output[x, y] = new UnityEngine.Color(
                        Mathf.Clamp(px.R / 255f, 0f, 1f),
                        Mathf.Clamp(px.G / 255f, 0f, 1f),
                        Mathf.Clamp(px.B / 255f, 0f, 1f),
                        Mathf.Clamp(px.A / 255f, 0f, 1f)
                    );
                }
            }
        }

        private static void RenderGithub(string fontPath, ref Array2D<Color> output, int mapResolution, int imageResolution) {
            var color = System.Drawing.Color.WhiteSmoke;
            var text = "github.com/hammzat/HarmonyCustomGenerator - Jungle Update [by aristocratos]";

            float scaleFactor = 0.04f;
            int fontSize = Mathf.Clamp((int)(imageResolution * scaleFactor), 10, 30);

            using (Font font = new Font(fontPath, fontSize)) {
                using (var dummyImage = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(dummyImage)) {
                    SizeF textSize = g.MeasureString(text, font);
                    int textWidth = (int)textSize.Width;
                    int textHeight = (int)textSize.Height;

                    int x = imageResolution / 2;
                    int y = imageResolution - textHeight;

                    RenderText(text, fontPath, fontSize, color, ref output, x, y);
                }
            }
        }


        private static void RenderMonument(List<MapMonument> monuments, string fontPath, ref Array2D<Color> output) {
            Logging.Info("3/4 | Rendering monuments...");
            var color = System.Drawing.Color.Black;

            foreach (MapMonument monument in monuments) {
                if (monument.indication == Indication.None) continue;
                if (monument.indication == Indication.Image) continue; // TODO

                var x = monument.x;
                var y = monument.y;
                var text = monument.name;
                int fontSize = monument.indication == Indication.Regular ? 20 : 11;
                var imagePath = monument.imagePath;

                RenderText(text, fontPath, fontSize, color, ref output, x, y);
            }
        }

        private static void RenderGrid(ref Array2D<Color> output, int mapResolution, int imageWidth, int oceanMargin) {
            Logging.Info("4/6 | Rendering grid...");
            var gridColor = System.Drawing.Color.FromArgb(120, 0, 0, 0);
            var bitmap = output.ToBitmap();
            
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                float gridSize = 146.3f;
                float cellSize = (float)mapResolution / (tempData.mapsize / gridSize);

                using (Pen gridPen = new Pen(gridColor, 1)) {
                    int gridCount = (int)(tempData.mapsize / gridSize);
                    for (int i = 0; i <= gridCount; i++) {
                        float x = oceanMargin + (i * cellSize);
                        if (x >= oceanMargin && x <= imageWidth - oceanMargin) {
                            graphics.DrawLine(gridPen, x, oceanMargin, x, imageWidth - oceanMargin);
                        }
                    }
                    
                    for (int i = 0; i <= gridCount; i++) {
                        float y = oceanMargin + (i * cellSize);
                        if (y >= oceanMargin && y <= imageWidth - oceanMargin) {
                            graphics.DrawLine(gridPen, oceanMargin, y, imageWidth - oceanMargin, y);
                        }
                    }

                    Font gridFont = new Font("Arial", 12, SDFontStyle.Bold);
                    float padding = 5f;

                    using (SolidBrush brush = new SolidBrush(gridColor)) {
                        for (int x = 0; x < gridCount; x++) {
                            for (int y = 0; y < gridCount; y++) {
                                float posX = oceanMargin + (x * cellSize);
                                float posY = oceanMargin + (y * cellSize);
                                float nextPosX = posX + cellSize;
                                float nextPosY = posY + cellSize;

                                bool isFull = posX >= oceanMargin && nextPosX <= imageWidth - oceanMargin && posY >= oceanMargin && nextPosY <= imageWidth - oceanMargin;
                                bool isPartRight = posX >= oceanMargin && posX <= imageWidth - oceanMargin && posY >= oceanMargin && posY <= imageWidth - oceanMargin && nextPosX > imageWidth - oceanMargin;

                                if (isFull || isPartRight) {
                                    string coords = x <= 25 ? $"{(char)('A' + x)}{gridCount - y}" : $"{(char)('A' + (x / 26 - 1))}{(char)('A' + (x % 26))}{gridCount - y}";
                                    float textX = posX + padding;
                                    float textY = posY + padding + (cellSize - (padding * 6));

                                    graphics.TranslateTransform(textX, textY);
                                    graphics.RotateTransform(180);
                                    graphics.ScaleTransform(-1, 1);
                                    graphics.DrawString(coords, gridFont, brush, 0, -gridFont.Height);
                                    graphics.ResetTransform();
                                }
                            }
                        }
                    }
                    gridFont.Dispose();
                }
            }

            int width = bitmap.Width;
            int height = bitmap.Height;
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var px = bitmap.GetPixel(x, y);
                    output[x, y] = new UnityEngine.Color(
                        Mathf.Clamp(px.R / 255f, 0f, 1f),
                        Mathf.Clamp(px.G / 255f, 0f, 1f),
                        Mathf.Clamp(px.B / 255f, 0f, 1f),
                        Mathf.Clamp(px.A / 255f, 0f, 1f)
                    );
                }
            }
        }

        public static byte[] Render(out int imageWidth, out int imageHeight, out Color background, float scale = 0.5f, bool lossy = true, bool transparent = false, int oceanMargin = 500) {
            Logging.Info("0/6 | Starting rendering map...");
            Stopwatch stopwatch = new();
            stopwatch.Start();
            if (lossy && transparent)
                throw new ArgumentException("Rendering a transparent map is not possible when using lossy compression (JPG)");

            imageWidth = 0;
            imageHeight = 0;
            background = OffShoreColor;
            TerrainTexturing instance = TerrainTexturing.Instance;

            if (instance == null)
                return null;

            Terrain component = instance.GetComponent<Terrain>();
            TerrainMeta component2 = instance.GetComponent<TerrainMeta>();
            TerrainHeightMap terrainHeightMap = instance.GetComponent<TerrainHeightMap>();
            TerrainSplatMap terrainSplatMap = instance.GetComponent<TerrainSplatMap>();
            TerrainTopologyMap terrainTopologyMap = instance.GetComponent<TerrainTopologyMap>();

            if (component == null || component2 == null || terrainHeightMap == null || terrainSplatMap == null || terrainTopologyMap == null)
                return null;
            
            int mapRes = (int)((float)World.Size * Mathf.Clamp(scale, 0.1f, 4f));
            float invMapRes = 1f / (float)mapRes;

            if (mapRes <= 0)
                return null;

            imageWidth = mapRes + oceanMargin * 2;
            imageHeight = mapRes + oceanMargin * 2;
            Color[] array = new Color[imageWidth * imageHeight];
            Array2D<Color> output = new Array2D<Color>(array, imageWidth, imageHeight);
            float maxDepth = (transparent ? Mathf.Max(Mathf.Abs(GetHeight(0f, 0f)), 5f) : 50f);
            Vector4 offShoreColor = (transparent ? Vector4.zero : OffShoreColor);
            Vector4 waterColor = (transparent ? new Vector4(WaterColor.x, WaterColor.y, WaterColor.z, 0.5f) : WaterColor);
            Logging.Info("1/6 | Render begin...");
            Parallel.For(0, imageHeight, delegate (int y) {
                y -= oceanMargin;
                float y2 = (float)y * invMapRes;
                int num = mapRes + oceanMargin;
                for (int i = -oceanMargin; i < num; i++) {
                    float x2 = (float)i * invMapRes;
                    Vector4 startColor = StartColor;
                    float num2 = GetHeight(x2, y2);
                    Vector3 lhs = GetNormal(x2, y2);
                    float num3 = GetShoreDist(x2, y2);
                    bool flag = (GetTopology(x2, y2) & 0x180) != 0;
                    float num4 = Math.Max(Vector3.Dot(lhs, SunDirection), 0f);
                    startColor = Vector4.Lerp(startColor, GravelColor, GetSplat(x2, y2, 128) * GravelColor.w);
                    startColor = Vector4.Lerp(startColor, PebbleColor, GetSplat(x2, y2, 64) * PebbleColor.w);
                    startColor = Vector4.Lerp(startColor, RockColor, GetSplat(x2, y2, 8) * RockColor.w);
                    startColor = Vector4.Lerp(startColor, DirtColor, GetSplat(x2, y2, 1) * DirtColor.w);
                    startColor = Vector4.Lerp(startColor, GrassColor, GetSplat(x2, y2, 16) * GrassColor.w);
                    startColor = Vector4.Lerp(startColor, ForestColor, GetSplat(x2, y2, 32) * ForestColor.w);
                    startColor = Vector4.Lerp(startColor, SandColor, GetSplat(x2, y2, 4) * SandColor.w);
                    startColor = Vector4.Lerp(startColor, SnowColor, GetSplat(x2, y2, 2) * SnowColor.w);
                    float num5 = 0f;
                    if (num3 > 0f) {
                        num5 = 0f - num2;
                        if (num5 <= 0f || !flag) {
                            num5 = Mathf.Max(num5, 0.1f * num3);
                        }
                    }
                    if (num5 > 0f) {
                        startColor = Vector4.Lerp(startColor, waterColor, Mathf.Clamp(0.5f + num5 / 5f, 0f, 1f));
                        startColor = Vector4.Lerp(startColor, offShoreColor, Mathf.Clamp(num5 / maxDepth, 0f, 1f));
                    }
                    else {
                        startColor += (num4 - 0.5f) * 0.65f * startColor;
                        startColor = (startColor - Half) * 0.94f + Half;
                    }
                    startColor *= 1.05f;
                    output[i + oceanMargin, y + oceanMargin] = (transparent ? new Color(startColor.x, startColor.y, startColor.z, startColor.w) : new Color(startColor.x, startColor.y, startColor.z));
                }
            });
            background = output[0, 0];

            LoadIcons(ref output, imageWidth, imageHeight, mapRes, oceanMargin);
            RenderGrid(ref output, mapRes, imageWidth, oceanMargin);
            Logging.Info($"  - Render took {stopwatch.Elapsed.Seconds}s.");
            Logging.Info("6/6 | Done! Encoding...");
            stopwatch.Stop();
            
            return EncodeToFile(imageWidth, imageHeight, array, lossy);

            float GetHeight(float x, float y) => terrainHeightMap.GetHeight(x, y);
            Vector3 GetNormal(float x, float y) => terrainHeightMap.GetNormal(x, y);
            float GetShoreDist(float x, float y) => TerrainTexturing.Instance.GetCoarseVectorToShore(x, y).shoreDist;
            float GetSplat(float x, float y, int mask) => terrainSplatMap.GetSplat(x, y, mask);
            int GetTopology(float x, float y) => terrainTopologyMap.GetTopology(x, y, 16f);
        }

        private static byte[] EncodeToFile(int width, int height, Color[] pixels, bool lossy) {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            Texture2D texture2D = null;
            try {
                texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
                texture2D.SetPixels(pixels);
                texture2D.Apply();
                return lossy ? ImageConversion.EncodeToJPG(texture2D, 85) : ImageConversion.EncodeToPNG(texture2D);
            }
            finally {
                if (texture2D != null) 
                    UnityEngine.Object.Destroy(texture2D);

                stopwatch.Stop();
                Logging.Info($"  - Encoding took {stopwatch.Elapsed.Seconds}s.");
            }
        }

        public static string GetMonumentName(MonumentInfo monument) {
            string name = monument.displayPhrase.english.Replace("\n", "");
            if (string.IsNullOrEmpty(name)) {
                if (monument.Type == MonumentType.Cave) name = "Cave";
                else if (monument.name.Contains("power_sub")) name = "Power Sub Station";
                else name = monument.name;
            }

            return name;
        }
    }
    public static class ColorExtensions
    {
        public static System.Drawing.Color ToSystemDrawingColor(this Color unityColor)
        {
            return System.Drawing.Color.FromArgb(
                Mathf.Clamp(Mathf.FloorToInt(unityColor.a * 255), 0, 255),
                Mathf.Clamp(Mathf.FloorToInt(unityColor.r * 255), 0, 255),
                Mathf.Clamp(Mathf.FloorToInt(unityColor.g * 255), 0, 255),
                Mathf.Clamp(Mathf.FloorToInt(unityColor.b * 255), 0, 255)
            );
        }
    }
}
