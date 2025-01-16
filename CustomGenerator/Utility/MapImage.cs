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
using CustomGenerator.Utilities;
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
        public static void RenderMap(TerrainTexturing _instance, float scale = 0.5f, int oceanMargin = 500) {
            CheckResources();

            byte[] array = MapImageRender.Render(_instance, out int num, out int num2, out Color color, scale, false, false, 350);
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
        private static void LoadIcons(ref Array2D<Color> output, TerrainTexturing _instance, int imageWidth, int imageHeight, int mapResolution, int oceanMargin) {
            List<MonumentInfo> monuments = (List<MonumentInfo>)_monuments.GetValue(tempData.terrainPath);
            Logging.Info("Proceeding map data...");

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
            var text = "github.com/hammzat/HarmonyCustomGenerator - Unique enviroment Update";

            int minFontSize = 10;
            int maxFontSize = 20;
            int minMapSize = 1350;
            int maxMapSize = 6350;

            int fontSize = minFontSize + (maxFontSize - minFontSize) * (mapResolution - minMapSize) / (maxMapSize - minMapSize);
            fontSize = Mathf.Clamp(fontSize, minFontSize, maxFontSize);

            using (Font font = new Font(fontPath, fontSize)) {
                SizeF textSize = Graphics.FromImage(new Bitmap(1, 1)).MeasureString(text, font);
                int textWidth = (int)textSize.Width;
                int textHeight = (int)textSize.Height;

                int x = imageResolution / 2;
                int y = imageResolution - textHeight;

                RenderText(text, fontPath, fontSize, color, ref output, x, y);
            }
        }

        private static void RenderMonument(List<MapMonument> monuments, string fontPath, ref Array2D<Color> output) {
            Logging.Info("Rendering monuments...");
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

        private static void RenderGrid(ref Array2D<Color> output, int mapResolution, int imageWidth, int oceanMargin)
        {
            Logging.Info("Rendering grid...");
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

        public static byte[] Render(TerrainTexturing _instance, out int imageWidth, out int imageHeight, out Color background, float scale = 0.5f, bool lossy = true, bool transparent = false, int oceanMargin = 500)
        {
            Logging.Info("Starting rendering map...");
            if (lossy && transparent) {
                throw new ArgumentException("Rendering a transparent map is not possible when using lossy compression (JPG)");
            }

            imageWidth = 0;
            imageHeight = 0;
            background = OffShoreColor;
            TerrainTexturing instance = _instance;
            if (instance == null) return null;

            Terrain component = instance.GetComponent<Terrain>();
            TerrainMeta component2 = instance.GetComponent<TerrainMeta>();
            TerrainHeightMap terrainHeightMap = instance.GetComponent<TerrainHeightMap>();
            TerrainSplatMap terrainSplatMap = instance.GetComponent<TerrainSplatMap>();
            if ((UnityEngine.Object)(object)component == null || component2 == null || terrainHeightMap == null || terrainSplatMap == null) return null;

            int mapRes = (int)(tempData.mapsize * Mathf.Clamp(scale, 0.1f, 4f)); ;
            float invMapRes = 1f / mapRes;
            if (mapRes <= 0) return null;

            imageWidth = mapRes + oceanMargin * 2;
            imageHeight = mapRes + oceanMargin * 2;
            Color[] array = new Color[imageWidth * imageHeight];
            Array2D<Color> output = new Array2D<Color>(array, imageWidth, imageHeight);
            float maxDepth = (transparent ? Mathf.Max(Mathf.Abs(GetHeight(0f, 0f)), 5f) : 50f);
            Vector4 offShoreColor = (transparent ? Vector4.zero : OffShoreColor);
            Vector4 waterColor = (transparent ? new Vector4(WaterColor.x, WaterColor.y, WaterColor.z, 0.5f) : WaterColor);
            System.Threading.Tasks.Parallel.For(0, imageHeight, delegate (int y) {
                y -= oceanMargin;
                float y2 = y * invMapRes;
                int num = mapRes + oceanMargin;
                for (int i = -oceanMargin; i < num; i++) {
                    float x2 = i * invMapRes;
                    Vector4 startColor = StartColor;
                    float height = GetHeight(x2, y2);
                    float num2 = Math.Max(Vector3.Dot(GetNormal(x2, y2), SunDirection), 0f);
                    startColor = Vector4.Lerp(startColor, GravelColor, GetSplat(x2, y2, 128) * GravelColor.w);
                    startColor = Vector4.Lerp(startColor, PebbleColor, GetSplat(x2, y2, 64) * PebbleColor.w);
                    startColor = Vector4.Lerp(startColor, RockColor, GetSplat(x2, y2, 8) * RockColor.w);
                    startColor = Vector4.Lerp(startColor, DirtColor, GetSplat(x2, y2, 1) * DirtColor.w);
                    startColor = Vector4.Lerp(startColor, GrassColor, GetSplat(x2, y2, 16) * GrassColor.w);
                    startColor = Vector4.Lerp(startColor, ForestColor, GetSplat(x2, y2, 32) * ForestColor.w);
                    startColor = Vector4.Lerp(startColor, SandColor, GetSplat(x2, y2, 4) * SandColor.w);
                    startColor = Vector4.Lerp(startColor, SnowColor, GetSplat(x2, y2, 2) * SnowColor.w);
                    float num3 = 0f - height;
                    if (num3 > 0f) {
                        startColor = Vector4.Lerp(startColor, waterColor, Mathf.Clamp(0.5f + num3 / 5f, 0f, 1f));
                        startColor = Vector4.Lerp(startColor, offShoreColor, Mathf.Clamp(num3 / maxDepth, 0f, 1f));
                    }
                    else {
                        startColor += (num2 - 0.5f) * 0.65f * startColor;
                        startColor = (startColor - Half) * 0.94f + Half;
                    }

                    startColor *= 1.05f;
                    output[i + oceanMargin, y + oceanMargin] = (transparent ? new Color(startColor.x, startColor.y, startColor.z, startColor.w) : new Color(startColor.x, startColor.y, startColor.z));
                }
            });
            background = output[0, 0];

            LoadIcons(ref output, _instance, imageWidth, imageHeight, mapRes, oceanMargin);
            RenderGrid(ref output, mapRes, imageWidth, oceanMargin);
            Logging.Info("Done! Encoding...");
            return EncodeToFile(imageWidth, imageHeight, array, lossy);

            Vector3 GetNormal(float x, float y) => terrainHeightMap.GetNormal(x, y);
            float GetHeight(float x, float y) => terrainHeightMap.GetHeight(x, y);
            float GetSplat(float x, float y, int mask) => terrainSplatMap.GetSplat(x, y, mask);
        }

        private static byte[] EncodeToFile(int width, int height, Color[] pixels, bool lossy)
        {
            Texture2D texture2D = null;
            try {
                texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
                texture2D.SetPixels(pixels);
                texture2D.Apply();
                return lossy ? ImageConversion.EncodeToJPG(texture2D, 85) : ImageConversion.EncodeToPNG(texture2D);
            }
            finally {
                if (texture2D != null) {
                    UnityEngine.Object.Destroy(texture2D);
                }
            }
        }

        public static string GetMonumentName(MonumentInfo monument)
        {
            string name = monument.displayPhrase.english.Replace("\n", "");
            if (string.IsNullOrEmpty(name)) {
                if (monument.Type == MonumentType.Cave) {
                    name = "Cave";
                }
                else if (monument.name.Contains("power_sub")) {
                    name = "Power Sub Station";
                }
                else {
                    name = monument.name;
                }
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
