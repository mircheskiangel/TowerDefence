using UnityEngine;
using static TowerDefense.EditorTools.PixelArt;

namespace TowerDefense.EditorTools
{
    // Editor-only: generates the isometric terrain skin (PNG files in Assets/Art/Generated/) by
    // porting the drawing code of Reference/skins_preview_reference.py: grass and dirt tiles,
    // cliff faces, trees, rocks, bushes, spawn portal, goal gate and the backdrop.
    // All randomness is seeded, so a rebuild writes identical files.
    //
    // Pixel coordinates are image coordinates (y DOWN), like the reference script. A 64 x 32
    // diamond tile is one cell (1 x 0.5 units at 64 pixels per unit); standing objects have their
    // pivot at the feet.
    public static class IsoArt
    {
        public const int TileW = 64, TileH = 32;
        public const int GrassPeriod = 4;      // grass variants repeat every 4 x 4 cells (16 files)
        public const int PathVariants = 3;
        public const int CliffHeight = 18;     // pixels below the tile edge
        public const int TreeVariants = 3, RockVariants = 2, BushVariants = 2;
        public const int Seed = 7;

        // World height of the cliff faces (for the camera framing).
        public const float CliffWorldHeight = CliffHeight / (float)PixelsPerUnit;

        public static void GenerateAll()
        {
            for (int j = 0; j < GrassPeriod; j++)
                for (int i = 0; i < GrassPeriod; i++)
                    Grass(i, j);
            for (int k = 0; k < PathVariants; k++) Path(k);
            Cliff(left: true);
            Cliff(left: false);
            float[] treeScales = { 0.9f, 1.05f, 1.2f };
            for (int k = 0; k < TreeVariants; k++) Tree(k, treeScales[k]);
            float[] rockScales = { 0.8f, 1.2f };
            for (int k = 0; k < RockVariants; k++) Rock(k, rockScales[k]);
            for (int k = 0; k < BushVariants; k++) Bush(k);
            Portal();
            Gate();
            Background();
        }

        // ------------------------------------------------------------ Tiles

        // Grass tile for cell class (i, j) = (x mod 4, y mod 4). The colour bands come from noise
        // in continuous GRID coordinates that repeats every 4 cells, so every tile joins its
        // neighbours seamlessly. 4 greens, speckles, tufts and flowers, a checker darkening on odd
        // cells and a slightly darker rim (reference tile()).
        private static void Grass(int i, int j)
        {
            var c = new PixelCanvas(TileW, TileH);
            bool odd = ((i + j) & 1) == 1;
            c.ForEachDiamondPixel(32, 16, 32, 16, (dx, dy, d) =>
            {
                int px = 32 + dx, py = 16 + dy;
                Vector2 g = new Vector2(i, j) + GridOffset(dx, dy);
                // Lattice steps per cell 2.5 and 5, so the periods are 4 cells = 10 and 20 steps.
                float n = 0.6f * PeriodicNoise(g.x * 2.5f, g.y * 2.5f, GrassPeriod * 5 / 2, 21)
                        + 0.4f * PeriodicNoise(g.x * 5f, g.y * 5f, GrassPeriod * 5, 22);
                float speck = Hash(Wrap(Mathf.FloorToInt(g.x * 10f), GrassPeriod * 10), Wrap(Mathf.FloorToInt(g.y * 10f), GrassPeriod * 10), 23);
                float r = n + 0.22f * (speck - 0.5f);
                int k = r < 0.40f ? 0 : r < 0.52f ? 1 : r < 0.64f ? 2 : 3;
                Color32 col = Palettes.Grass[k];
                if (Hash(px + i * 64, py + j * 32, 1) > 0.95f) col = Palettes.Grass[Mathf.Min(3, k + 1)];
                if (odd) col = Mul(col, 0.93f);
                if (d > 0.93f) col = Mul(col, 0.86f);
                c.Put(px, py, col);
            });

            var rng = Rng(Seed * 1000 + i + j * GrassPeriod);
            for (int t = 0; t < 7; t++)
            {
                int dx = rng.Next(-26, 27), dy = rng.Next(-11, 12);
                Color32 col = Palettes.GrassBlades[rng.Next(Palettes.GrassBlades.Length)];
                bool tall = rng.NextDouble() < 0.5;
                if (Mathf.Abs(dx) / 32f + Mathf.Abs(dy) / 16f >= 0.8f) continue;
                int x = 32 + dx, y = 16 + dy;
                c.Put(x, y, col);
                c.Put(x, y - 1, col);
                if (tall) c.Put(x + 1, y - 2, col);
            }
            if (rng.NextDouble() < 0.22)
            {
                int dx = rng.Next(-20, 21), dy = rng.Next(-8, 9);
                Color32 col = Palettes.Flowers[rng.Next(Palettes.Flowers.Length)];
                if (Mathf.Abs(dx) / 32f + Mathf.Abs(dy) / 16f < 0.7f)
                {
                    int x = 32 + dx, y = 16 + dy;
                    c.Put(x, y, col);
                    c.Put(x + 1, y, col);
                    c.Put(x, y - 1, Rgb(80, 150, 50));
                }
            }
            SaveSprite(c, $"Grass_{i + j * GrassPeriod}", PivotCenter);
        }

        // Dirt road overlay tile: 3 dirt tones in noise bands, speckles, pebbles, darker rim.
        private static void Path(int k)
        {
            var c = new PixelCanvas(TileW, TileH);
            c.ForEachDiamondPixel(32, 16, 32, 16, (dx, dy, d) =>
            {
                int px = 32 + dx, py = 16 + dy;
                float r = 0.55f * ValueNoise(px * 0.11f + k * 13f, py * 0.22f, 31 + k) + 0.45f * Hash(px / 3, py / 3, 32 + k);
                Color32 col = r < 0.40f ? Palettes.Dirt[1] : r < 0.66f ? Palettes.Dirt[0] : Palettes.Dirt[2];
                if (Hash(px, py, 33 + k) > 0.94f) col = Palettes.Dirt[2];
                if (d > 0.93f) col = Mul(col, 0.86f);
                c.Put(px, py, col);
            });
            var rng = Rng(Seed * 2000 + k);
            for (int p = 0; p < 4; p++)
            {
                int dx = rng.Next(-22, 23), dy = rng.Next(-9, 10);
                if (Mathf.Abs(dx) / 32f + Mathf.Abs(dy) / 16f >= 0.75f) continue;
                int x = 32 + dx, y = 16 + dy;
                for (int q = 0; q < 3; q++) c.Put(x + q, y, Palettes.Pebbles[0]);
                c.Put(x, y + 1, Palettes.Pebbles[1]);
                c.Put(x + 1, y + 1, Palettes.Pebbles[1]);
            }
            SaveSprite(c, $"Path_{k}", PivotCenter);
        }

        // Earth/rock face hanging under one front edge of a tile, with a grass lip (reference
        // cliff()). Left = under the lower-left edge (lit, 0.92), right = lower-right (0.72).
        // Same canvas layout as a tile plus CliffHeight pixels; pivot at the tile center.
        private static void Cliff(bool left)
        {
            var c = new PixelCanvas(TileW, TileH + CliffHeight);
            float shade = left ? 0.92f : 0.72f;
            Vector2[] face = left
                ? new[] { new Vector2(0, 16), new Vector2(32, 32), new Vector2(32, 32 + CliffHeight), new Vector2(0, 16 + CliffHeight) }
                : new[] { new Vector2(64, 16), new Vector2(32, 32), new Vector2(32, 32 + CliffHeight), new Vector2(64, 16 + CliffHeight) };
            var mask = new PixelCanvas(c.Width, c.Height);
            mask.FillPolygon(face, Rgb(255, 255, 255));
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    if (mask.Get(x, y).a == 0) continue;
                    float top = left ? 16f + x * 0.5f : 16f + (64f - x) * 0.5f;
                    float dd = y - top;
                    Color32 col = Palettes.Cliff[(int)(Hash(x / 2, y / 2, 5) * 4f) % 4];
                    if (dd < 3f) col = dd < 2f ? Rgb(66, 128, 42) : Rgb(52, 100, 36);
                    else if (Hash(x, y / 5, 9) > 0.86f) col = Rgb(150, 150, 142);
                    c.Put(x, y, Mul(col, shade * (1f - 0.25f * Mathf.Min(1f, dd / CliffHeight))));
                }
            SaveSprite(c, left ? "CliffLeft" : "CliffRight", PivotAt(c, 32, 16));
        }

        // ------------------------------------------------------------ Decoration

        private static void Tree(int variant, float s)
        {
            var c = new PixelCanvas(44, 44);
            int x = 22, y = 38;
            c.Shadow(x, y, Mathf.RoundToInt(13 * s), Mathf.RoundToInt(5 * s), 0.32f);
            for (int i = 0; i < (int)(7 * s); i++)
                for (int j = 0; j < 3; j++)
                    c.Put(x - 1 + j, y - i, j < 2 ? Rgb(98, 64, 38) : Rgb(72, 46, 28));
            int[,] blobs = { { 0, -14, 9 }, { -6, -10, 7 }, { 6, -10, 7 }, { 0, -21, 7 }, { -4, -17, 6 }, { 4, -17, 6 } };
            for (int b = 0; b < blobs.GetLength(0); b++)
            {
                int br = (int)(blobs[b, 2] * s);
                for (int dy = -br; dy <= br; dy++)
                    for (int dx = -br; dx <= br; dx++)
                    {
                        if (dx * dx + dy * dy > br * br) continue;
                        float l = (-dx * 0.55f - dy * 0.7f) / br;
                        Color32 col = l < -0.2f ? Palettes.Leaves[0] : l < 0.3f ? Palettes.Leaves[1] : Palettes.Leaves[2];
                        if (Hash(x + dx + variant * 50, y + dy, 3) > 0.9f) col = Mul(col, 1.15f);
                        c.Put(x + (int)(blobs[b, 0] * s) + dx, y + (int)(blobs[b, 1] * s) + dy, col);
                    }
            }
            SaveSprite(c, $"Tree_{variant}", PivotAt(c, x + 0.5f, y + 1));
        }

        private static void Rock(int variant, float s)
        {
            var c = new PixelCanvas(32, 24);
            int x = 16, y = 19;
            c.Shadow(x, y, Mathf.RoundToInt(11 * s), Mathf.RoundToInt(4 * s), 0.3f);
            Vector2[] p = new Vector2[7];
            int[,] pts = { { -9, 0 }, { -7, -8 }, { -1, -12 }, { 6, -9 }, { 9, -2 }, { 5, 2 }, { -5, 2 } };
            for (int k = 0; k < 7; k++) p[k] = new Vector2(x + (int)(pts[k, 0] * s), y + (int)(pts[k, 1] * s));
            c.FillPolygon(p, Palettes.Stone[1]);
            c.FillPolygon(new[] { p[1], p[2], p[3], new Vector2(x, y - 4) }, Palettes.Stone[2]);
            c.FillPolygon(new[] { p[3], p[4], p[5], new Vector2(x, y - 4) }, Palettes.Stone[0]);
            SaveSprite(c, $"Rock_{variant}", PivotAt(c, x + 0.5f, y + 1));
        }

        private static void Bush(int variant)
        {
            var c = new PixelCanvas(24, 16);
            int x = 12, y = 12;
            c.Shadow(x, y, 7, 3, 0.28f);
            int[,] blobs = variant == 0
                ? new[,] { { 0, -4, 5 }, { -5, -2, 4 }, { 5, -2, 4 } }
                : new[,] { { -2, -4, 5 }, { 4, -3, 4 }, { -6, -1, 3 } };
            for (int b = 0; b < blobs.GetLength(0); b++)
            {
                int br = blobs[b, 2];
                for (int dy = -br; dy <= br; dy++)
                    for (int dx = -br; dx <= br; dx++)
                        if (dx * dx + dy * dy <= br * br)
                            c.Put(x + blobs[b, 0] + dx, y + blobs[b, 1] + dy, dy > 0 ? Rgb(54, 122, 44) : Rgb(84, 158, 56));
            }
            SaveSprite(c, $"Bush_{variant}", PivotAt(c, x + 0.5f, y + 1));
        }

        // ------------------------------------------------------------ Spawn portal and goal gate

        // Two stone pillars with a lintel and a blue glow between them (reference portal()).
        private static void Portal()
        {
            var c = new PixelCanvas(40, 44);
            int x = 20, y = 38;
            c.Shadow(x, y, 16, 6, 0.35f);
            c.FillPolygon(Pts(x, y, -14, 0, -8, -26, -4, -26, -4, 0, -10, 3), Rgb(146, 142, 136));
            c.FillPolygon(Pts(x, y, 4, 0, 4, -26, 10, -26, 14, 0, 8, 3), Rgb(112, 110, 106));
            c.FillPolygon(Pts(x, y, -14, -26, 14, -26, 14, -31, -14, -31), Rgb(170, 166, 158));
            for (int dy = -24; dy < 0; dy++)
                for (int dx = -4; dx < 5; dx++)
                {
                    float d = Mathf.Sqrt((dx / 4.5f) * (dx / 4.5f) + ((dy + 12) / 13f) * ((dy + 12) / 13f));
                    if (d < 1f) c.Blend(x + dx, y + dy, Rgb(70, 170, 255), 0.95f - 0.5f * d);
                }
            for (int dy = -24; dy < 0; dy++)
                for (int dx = -9; dx < 10; dx++)
                {
                    float d = Mathf.Sqrt((dx / 9f) * (dx / 9f) + ((dy + 12) / 15f) * ((dy + 12) / 15f));
                    if (d < 1f) c.Blend(x + dx, y + dy, Rgb(60, 150, 255), 0.18f * (1f - d));
                }
            SaveSprite(c, "SpawnPortal", PivotAt(c, x, y + 1));
        }

        // Two stone pillars with a lintel and a red banner (reference gate()).
        private static void Gate()
        {
            var c = new PixelCanvas(40, 48);
            int x = 20, y = 42;
            c.Shadow(x, y, 16, 6, 0.35f);
            c.FillPolygon(Pts(x, y, -14, 0, -8, -30, -4, -30, -4, 0, -10, 3), Rgb(146, 142, 136));
            c.FillPolygon(Pts(x, y, 4, 0, 4, -30, 10, -30, 14, 0, 8, 3), Rgb(112, 110, 106));
            c.FillPolygon(Pts(x, y, -12, -30, 12, -30, 12, -34, -12, -34), Rgb(176, 172, 164));
            c.FillPolygon(Pts(x, y, -6, -30, 6, -30, 6, -10, 0, -5, -6, -10), Rgb(190, 36, 40));
            c.FillPolygon(Pts(x, y, -2, -22, 2, -22, 2, -18, -2, -18), Rgb(240, 236, 230));
            for (int dy = -6; dy < 0; dy++)
                for (int dx = -10; dx < 11; dx++)
                    c.Blend(x + dx, y + dy, Rgb(255, 60, 50), 0.12f);
            SaveSprite(c, "GoalGate", PivotAt(c, x, y + 1));
        }

        // ------------------------------------------------------------ Backdrop

        // Dark forest-teal backdrop with blocky noise and a soft vignette (reference background).
        // One pixel is drawn as 0.1 world units (BoardView scales it to cover the view).
        private static void Background()
        {
            const int w = 400, h = 260;
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 1f + 0.18f * Hash(x, y, 4) - 0.25f * Mathf.Abs(x - w / 2f) / w - 0.15f * Mathf.Abs(y - h / 2f) / h;
                    c.Put(x, y, Mul(Rgb(14, 38, 40), v));
                }
            SaveSprite(c, "Background", PivotCenter);
        }

        // ------------------------------------------------------------ Helpers

        // Grid offset (dcx, dcy) of tile pixel (dx, dy) from the tile center (dy image-down):
        // world (dx, -dy) / 64, then the isometric inverse cx = x + 2y, cy = 2y - x.
        private static Vector2 GridOffset(int dx, int dy)
        {
            float wx = (dx + 0.5f) / PixelsPerUnit, wy = -(dy + 0.5f) / PixelsPerUnit;
            return new Vector2(wx + 2f * wy, 2f * wy - wx);
        }

        private static Vector2[] Pts(int x, int y, params int[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int k = 0; k < p.Length; k++) p[k] = new Vector2(x + xy[2 * k], y + xy[2 * k + 1]);
            return p;
        }
    }
}
