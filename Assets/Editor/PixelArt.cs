using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    // Editor-only toolbox for code-generated pixel art (the isometric skins). Everything is
    // deterministic: the noise is a pure hash of (x, y, seed) and random choices come from
    // Rng(seed), so rebuilding always writes identical PNG files.
    //
    // Pixel coordinates in PixelCanvas are IMAGE coordinates (x right, y DOWN, origin top-left),
    // like the Python reference script, so its drawing code ports one-to-one. The canvas is
    // flipped once when it is turned into a texture.
    //
    // Generated sprites are saved under Assets/Art/Generated/ and imported as: Sprite (Single),
    // Point filter, no compression, no mipmaps, 64 pixels per unit (a 64 x 32 px diamond tile
    // = the 1 x 0.5 unit isometric tile), with a pivot chosen per sprite (center for tiles,
    // feet for standing objects).
    public static class PixelArt
    {
        public const int PixelsPerUnit = 64;
        public const int DefaultSeed = 7;
        public const string GeneratedFolder = "Assets/Art/Generated";

        public static readonly Vector2 PivotCenter = new Vector2(0.5f, 0.5f);

        // ------------------------------------------------------------ Noise and randomness

        // Hash noise in [0, 1], same formula as the reference script's hashn().
        public static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                int h = (x * 73856093) ^ (y * 19349663) ^ (seed * 83492791);
                return (h & 0xffff) / 65535f;
            }
        }

        // Smooth value noise in [0, 1]: Hash on an integer lattice, smoothstep-interpolated.
        public static float ValueNoise(float x, float y, int seed = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = Smooth(x - x0), ty = Smooth(y - y0);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        // ValueNoise that repeats every 'period' lattice steps in x and y, so textures generated
        // for neighbouring tiles from the same continuous coordinates join without seams.
        public static float PeriodicNoise(float x, float y, int period, int seed = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = Smooth(x - x0), ty = Smooth(y - y0);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period), ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed);
            float c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        public static int Wrap(int a, int n) => ((a % n) + n) % n;

        // Several octaves of ValueNoise (each half the size and weight of the previous), in [0, 1].
        public static float Fractal(float x, float y, int seed = 0, int octaves = 3)
        {
            float sum = 0f, weight = 1f, total = 0f, scale = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x * scale, y * scale, seed + i * 101) * weight;
                total += weight;
                weight *= 0.5f;
                scale *= 2f;
            }
            return sum / total;
        }

        // Deterministic random source for placement choices (never UnityEngine.Random).
        public static System.Random Rng(int seed = DefaultSeed)
        {
            return new System.Random(seed);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // ------------------------------------------------------------ Colors and ramps

        public static Color32 Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        // Color times a brightness factor (alpha kept), like the reference script's mul().
        public static Color32 Mul(Color32 c, float f)
        {
            return new Color32(ClampByte(c.r * f), ClampByte(c.g * f), ClampByte(c.b * f), c.a);
        }

        // Band of 'ramp' (dark to light) for t in [0, 1] (flat bands, no smooth gradient).
        public static Color32 Ramp(Color32[] ramp, float t)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(t * ramp.Length), 0, ramp.Length - 1);
            return ramp[i];
        }

        // Band of 'ramp' for a light value: the first threshold it does not reach picks the band.
        // E.g. thresholds {0.05, 0.38, 0.72} with a 4-color ramp = the reference sphere shading.
        public static Color32 RampShade(Color32[] ramp, float light, float[] thresholds)
        {
            int i = 0;
            while (i < thresholds.Length && light > thresholds[i]) i++;
            return ramp[Mathf.Min(i, ramp.Length - 1)];
        }

        // Truncates like Python's int() (all values here are >= 0), so ported colors match.
        private static byte ClampByte(float v) => (byte)Mathf.Clamp(Mathf.FloorToInt(v), 0, 255);

        // Palettes taken from the reference preview (dark to light where it is a ramp).
        public static class Palettes
        {
            public static readonly Color32[] Grass = { Rgb(70, 138, 44), Rgb(86, 158, 52), Rgb(100, 174, 58), Rgb(118, 192, 68) };
            public static readonly Color32[] GrassBlades = { Rgb(60, 120, 38), Rgb(130, 205, 80), Rgb(54, 110, 34) };
            public static readonly Color32[] Flowers = { Rgb(246, 246, 236), Rgb(250, 222, 84), Rgb(238, 140, 172) };
            public static readonly Color32[] Dirt = { Rgb(188, 142, 92), Rgb(170, 126, 78), Rgb(208, 164, 110) };
            public static readonly Color32[] Pebbles = { Rgb(158, 152, 140), Rgb(120, 114, 104) };
            public static readonly Color32[] Cliff = { Rgb(122, 82, 50), Rgb(104, 68, 42), Rgb(142, 98, 60), Rgb(88, 58, 36) };
            public static readonly Color32[] Yellow = { Rgb(150, 92, 10), Rgb(214, 150, 22), Rgb(244, 200, 48), Rgb(255, 236, 120) };
            public static readonly Color32[] Purple = { Rgb(60, 28, 96), Rgb(104, 56, 150), Rgb(142, 86, 196), Rgb(190, 146, 240) };
            public static readonly Color32[] Leaves = { Rgb(44, 104, 40), Rgb(62, 134, 48), Rgb(92, 170, 60) };
            public static readonly Color32[] Stone = { Rgb(104, 102, 98), Rgb(138, 136, 130), Rgb(176, 174, 166) };
            public static readonly Color32[] Wood = { Rgb(72, 46, 28), Rgb(98, 64, 38), Rgb(112, 76, 48), Rgb(150, 104, 66) };
            public static readonly Color32 ShadowColor = Rgb(10, 24, 10);
        }

        // ------------------------------------------------------------ Canvas

        public sealed class PixelCanvas
        {
            public readonly int Width;
            public readonly int Height;
            private readonly Color32[] pixels; // Row-major, row 0 = TOP of the image.

            public PixelCanvas(int width, int height)
            {
                Width = width;
                Height = height;
                pixels = new Color32[width * height]; // Fully transparent.
            }

            public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

            public Color32 Get(int x, int y) => Inside(x, y) ? pixels[y * Width + x] : default;

            // Opaque pixel (ignored outside the canvas).
            public void Put(int x, int y, Color32 c)
            {
                if (!Inside(x, y)) return;
                c.a = 255;
                pixels[y * Width + x] = c;
            }

            // Mixes 'c' over the pixel with opacity 'a' (alpha grows, never shrinks).
            public void Blend(int x, int y, Color32 c, float a)
            {
                if (!Inside(x, y)) return;
                Color32 o = pixels[y * Width + x];
                if (o.a == 0) { c.a = ClampByte(255f * a); pixels[y * Width + x] = c; return; }
                pixels[y * Width + x] = new Color32(
                    ClampByte(o.r * (1f - a) + c.r * a),
                    ClampByte(o.g * (1f - a) + c.g * a),
                    ClampByte(o.b * (1f - a) + c.b * a),
                    (byte)Mathf.Max(o.a, ClampByte(255f * a)));
            }

            // Fills a polygon (points in pixel coordinates); a pixel is inside when its center is
            // (even-odd rule), so shared edges of neighbouring polygons do not overlap.
            public void FillPolygon(Vector2[] points, Color32 c, float alpha = 1f)
            {
                if (points == null || points.Length < 3) return;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector2 p in points) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
                var crossings = new System.Collections.Generic.List<float>();
                for (int y = y0; y <= y1; y++)
                {
                    float sy = y + 0.5f;
                    crossings.Clear();
                    for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    {
                        Vector2 a = points[i], b = points[j];
                        if ((a.y > sy) == (b.y > sy)) continue;
                        crossings.Add(a.x + (sy - a.y) * (b.x - a.x) / (b.y - a.y));
                    }
                    crossings.Sort();
                    for (int k = 0; k + 1 < crossings.Count; k += 2)
                    {
                        int xa = Mathf.CeilToInt(crossings[k] - 0.5f), xb = Mathf.FloorToInt(crossings[k + 1] - 0.5f);
                        for (int x = xa; x <= xb; x++)
                        {
                            if (alpha >= 1f) Put(x, y, c);
                            else Blend(x, y, c, alpha);
                        }
                    }
                }
            }

            // Filled axis-aligned ellipse centered on pixel (cx, cy) with radii rx, ry.
            public void FillEllipse(int cx, int cy, int rx, int ry, Color32 c, float alpha = 1f)
            {
                if (rx <= 0 || ry <= 0) return;
                for (int dy = -ry; dy <= ry; dy++)
                    for (int dx = -rx; dx <= rx; dx++)
                    {
                        float fx = dx / (float)rx, fy = dy / (float)ry;
                        if (fx * fx + fy * fy > 1f) continue;
                        if (alpha >= 1f) Put(cx + dx, cy + dy, c);
                        else Blend(cx + dx, cy + dy, c, alpha);
                    }
            }

            // Soft ground shadow (a dark ellipse blended in), like the reference shadow().
            public void Shadow(int cx, int cy, int rx, int ry, float alpha = 0.35f)
            {
                FillEllipse(cx, cy, rx, ry, Palettes.ShadowColor, alpha);
            }

            // Shaded pixel sphere of radius r centered on pixel (cx, cy), lit from the upper left,
            // with a 1-pixel dark rim, an outline ring and a small specular highlight. Port of the
            // reference ball() (without its ground shadow; call Shadow() for that).
            public void Sphere(int cx, int cy, int r, Color32[] ramp, Color32 outline)
            {
                float[] thresholds = { 0.05f, 0.38f, 0.72f };
                for (int dy = -r - 1; dy <= r + 1; dy++)
                    for (int dx = -r - 1; dx <= r + 1; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= r + 0.5f)
                        {
                            float nx = dx / (float)r, ny = dy / (float)r;
                            float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                            float light = nx * -0.5f + ny * -0.6f + nz * 0.62f;
                            Color32 c = RampShade(ramp, light, thresholds);
                            if (d > r - 0.8f) c = Mul(c, 0.62f);
                            Put(cx + dx, cy + dy, c);
                        }
                        else if (d <= r + 1.6f)
                        {
                            Put(cx + dx, cy + dy, outline);
                        }
                    }
                Color32 white = Rgb(255, 255, 255), cream = Rgb(255, 250, 225);
                Put(cx - r / 3, cy - r / 2, white);
                Put(cx - r / 3 + 1, cy - r / 2, cream);
                Put(cx - r / 3, cy - r / 2 + 1, cream);
            }

            // Filled rectangle, corners included (reference rect()).
            public void FillRect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        Put(x, y, c);
            }

            // Ellipse shaded with a 4-tone ramp, lit from the upper left (reference ell()).
            // Center and radii may be fractional; pixels are tested at their centers.
            public void ShadedEllipse(float cx, float cy, float rx, float ry, Color32[] ramp)
            {
                float[] thresholds = { 0f, 0.36f, 0.72f };
                for (int y = (int)(cy - ry - 1f); y < (int)(cy + ry + 2f); y++)
                    for (int x = (int)(cx - rx - 1f); x < (int)(cx + rx + 2f); x++)
                    {
                        float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                        if (nx * nx + ny * ny > 1f) continue;
                        float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                        float light = nx * -0.5f + ny * -0.6f + nz * 0.62f;
                        Put(x, y, RampShade(ramp, light, thresholds));
                    }
            }

            // One-pixel line (reference line()).
            public void Line(float x0, float y0, float x1, float y1, Color32 c)
            {
                int n = (int)Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2 + 1;
                for (int i = 0; i <= n; i++)
                    Put((int)(x0 + (x1 - x0) * i / n), (int)(y0 + (y1 - y0) * i / n), c);
            }

            // Polygon with its edge pixels included (the reference draws small shapes such as ears
            // and tusks this way; FillPolygon alone covers only the pixels whose center is inside).
            public void FillPolygonWithEdges(Vector2[] points, Color32 c)
            {
                FillPolygon(points, c);
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    Line(points[j].x, points[j].y, points[i].x, points[i].y, c);
            }

            // Outline pass: every empty pixel next to a filled one becomes 'c' (reference outline()).
            public void Outline(Color32 c)
            {
                var edge = new bool[Width * Height];
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        if (Get(x, y).a != 0) continue;
                        edge[y * Width + x] = Get(x + 1, y).a != 0 || Get(x - 1, y).a != 0
                                              || Get(x, y + 1).a != 0 || Get(x, y - 1).a != 0;
                    }
                for (int i = 0; i < edge.Length; i++)
                    if (edge[i]) { c.a = 255; pixels[i] = c; }
            }

            // First and last rows that hold any pixel (-1 when the canvas is empty).
            public void OpaqueRows(out int first, out int last)
            {
                first = last = -1;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        if (Get(x, y).a != 0)
                        {
                            if (first < 0) first = y;
                            last = y;
                            break;
                        }
            }

            // New canvas holding rows firstRow..lastRow of this one (same width).
            public PixelCanvas CropRows(int firstRow, int lastRow)
            {
                var cropped = new PixelCanvas(Width, lastRow - firstRow + 1);
                System.Array.Copy(pixels, firstRow * Width, cropped.pixels, 0, cropped.pixels.Length);
                return cropped;
            }

            // Calls 'paint(dx, dy, d)' for every pixel of a 2:1 diamond centered on (cx, cy)
            // with half sizes halfW x halfH; d is the diamond distance (0 center .. 1 edge).
            public void ForEachDiamondPixel(int cx, int cy, int halfW, int halfH, System.Action<int, int, float> paint)
            {
                for (int dy = -halfH; dy < halfH; dy++)
                    for (int dx = -halfW; dx < halfW; dx++)
                    {
                        float d = Mathf.Abs(dx + 0.5f) / halfW + Mathf.Abs(dy + 0.5f) / halfH;
                        if (d <= 1f) paint(dx, dy, d);
                    }
            }

            // Texture with Unity's bottom-up rows (the canvas is top-down).
            public Texture2D ToTexture()
            {
                Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                Color32[] flipped = new Color32[pixels.Length];
                for (int y = 0; y < Height; y++)
                    System.Array.Copy(pixels, y * Width, flipped, (Height - 1 - y) * Width, Width);
                tex.SetPixels32(flipped);
                tex.Apply();
                return tex;
            }
        }

        // ------------------------------------------------------------ Saving

        // Pivot at pixel (x, yFromTop) of a canvas, e.g. the feet of a standing object.
        public static Vector2 PivotAt(PixelCanvas canvas, float x, float yFromTop)
        {
            return new Vector2(x / canvas.Width, 1f - yFromTop / canvas.Height);
        }

        // Writes the canvas as Assets/Art/Generated/<name>.png and imports it as a pixel-art
        // sprite (see the class comment). Returns the imported sprite.
        // 'border' (left, bottom, right, top in pixels) makes the sprite 9-sliceable (UI panels,
        // the health bar frame); zero = a plain sprite.
        public static Sprite SaveSprite(PixelCanvas canvas, string name, Vector2 pivot, Vector4 border = default)
        {
            EnsureFolder(GeneratedFolder);
            string path = $"{GeneratedFolder}/{name}.png";
            Texture2D tex = canvas.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
