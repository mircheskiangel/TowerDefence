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
        public const int GrassDetailVariants = 8; // tuft/flower overlays, picked per cell by a mixing hash
        // Enemy size relative to the reference (Grunt radius 6 px, Tank 10 px at scale 1).
        // 1.5: at scale 1 a Grunt is only ~16 screen pixels at 1280 x 720, too small to read.
        public const float EnemyScale = 1.5f;
        public const int GruntRadius = 6, TankRadius = 10;
        // Health bar fill width in pixels (visual only), narrower for the Grunt.
        public static int GruntBarPixels => Mathf.RoundToInt(12 * EnemyScale);
        public static int TankBarPixels => Mathf.RoundToInt(18 * EnemyScale);
        public const int Seed = 7;

        // World height of the cliff faces (for the camera framing).
        public const float CliffWorldHeight = CliffHeight / (float)PixelsPerUnit;

        public static void GenerateAll()
        {
            for (int j = 0; j < GrassPeriod; j++)
                for (int i = 0; i < GrassPeriod; i++)
                    Grass(i, j);
            for (int k = 0; k < GrassDetailVariants; k++) GrassDetail(k);
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
            Tower();
            TowerShadow();
            GenerateEnemies(EnemyScale);
            GenerateCharacters();
            Projectile();
            PortalGlow();
            HealthBarFrame();
            Puff();
            HudPanel();
            HudButton();
            IconCoin();
            IconHeart();
            IconArrow();
        }

        // Goblin (Grunt) and ogre (Tank): 2 walk frames each, facing right, and a ground shadow
        // per type. Native pixel size (64 px = 1 unit), the same pixel grid as the terrain.
        public static void GenerateCharacters()
        {
            SaveWalkFrames("GoblinWalk", Goblin(0), Goblin(1), GoblinPivotX);
            SaveWalkFrames("OgreWalk", Ogre(0), Ogre(1), OgrePivotX);
            EllipseShadow("GoblinShadow", 9, 4);
            EllipseShadow("OgreShadow", 15, 5);
        }

        // Grunt and Tank balls with their contact shadows; 'scale' multiplies the reference radii.
        public static void GenerateEnemies(float scale)
        {
            int rg = Mathf.RoundToInt(GruntRadius * scale), rt = Mathf.RoundToInt(TankRadius * scale);
            Ball("Grunt", rg, Palettes.Yellow, Rgb(62, 38, 6));
            Ball("Tank", rt, Palettes.Purple, Rgb(30, 14, 50));
            BallShadow("GruntShadow", rg);
            BallShadow("TankShadow", rt);
        }

        // ------------------------------------------------------------ Tiles

        // Grass tile for cell class (i, j) = (x mod 4, y mod 4). The colour bands come from noise
        // in continuous GRID coordinates that repeats every 4 cells, so every tile joins its
        // neighbours seamlessly. 4 greens, speckles, a checker darkening on odd cells and a
        // slightly darker rim (reference tile()). Tufts and flowers are separate overlays
        // (GrassDetail), chosen per cell by a non-periodic hash, so they never repeat every 4 cells.
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

            SaveSprite(c, $"Grass_{i + j * GrassPeriod}", PivotCenter);
        }

        // Transparent tile-sized overlay with 7 grass tufts and sometimes a flower (reference tile()).
        private static void GrassDetail(int k)
        {
            var c = new PixelCanvas(TileW, TileH);
            var rng = Rng(Seed * 3000 + k);
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
            if (rng.NextDouble() < 0.35)
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
            SaveSprite(c, $"GrassDetail_{k}", PivotCenter);
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
            var c = new PixelCanvas(26, 18);
            int x = 13, y = 13;
            c.Shadow(x, y, 7, 3, 0.28f);
            int[,] blobs = variant == 0
                ? new[,] { { 0, -4, 5 }, { -5, -2, 4 }, { 5, -2, 4 } }
                : new[,] { { -2, -4, 5 }, { 4, -3, 4 }, { -6, -1, 3 } };
            // Leaves of all blobs as one shape (mask), a dark outline only around the OUTSIDE of
            // that shape (no dark seams between blobs), and one soft highlight per blob toward the
            // upper left, so the bush reads against the grass without a "face".
            bool[,] leaf = new bool[c.Width, c.Height];
            int owner(int px, int py)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int b = 0; b < blobs.GetLength(0); b++)
                {
                    float ddx = px - (x + blobs[b, 0]), ddy = py - (y + blobs[b, 1]);
                    float d = (ddx * ddx + ddy * ddy) / (blobs[b, 2] * blobs[b, 2]);
                    if (d <= 1f && d < bestD) { bestD = d; best = b; }
                }
                return best;
            }
            for (int py = 0; py < c.Height; py++)
                for (int px = 0; px < c.Width; px++)
                    leaf[px, py] = owner(px, py) >= 0;
            for (int py = 0; py < c.Height; py++)
                for (int px = 0; px < c.Width; px++)
                {
                    if (leaf[px, py]) continue;
                    bool edge = (px > 0 && leaf[px - 1, py]) || (px < c.Width - 1 && leaf[px + 1, py])
                             || (py > 0 && leaf[px, py - 1]) || (py < c.Height - 1 && leaf[px, py + 1]);
                    if (edge) c.Put(px, py, Rgb(30, 70, 26));
                }
            for (int py = 0; py < c.Height; py++)
                for (int px = 0; px < c.Width; px++)
                {
                    int b = owner(px, py);
                    if (b < 0) continue;
                    float br = blobs[b, 2];
                    float hx = px - (x + blobs[b, 0] - br * 0.35f), hy = py - (y + blobs[b, 1] - br * 0.4f);
                    float h = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / (br * 0.55f));
                    Color32 col = py - (y + blobs[b, 1]) > 0 ? Rgb(54, 122, 44) : Rgb(84, 158, 56);
                    if (h > 0.45f) col = Rgb(120, 188, 76);
                    else if (h > 0.05f) col = Rgb(100, 172, 64);
                    c.Put(px, py, col);
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
            SaveSprite(c, "SpawnPortal", PivotAt(c, x, y + 1));
        }

        // The portal's soft blue halo (the reference's outer glow) as its own sprite, laid over the
        // portal with the same canvas and pivot, so it can pulse (GlowPulse) without the stones.
        private static void PortalGlow()
        {
            var c = new PixelCanvas(40, 44);
            int x = 20, y = 38;
            for (int dy = -26; dy < 2; dy++)
                for (int dx = -11; dx < 12; dx++)
                {
                    float d = Mathf.Sqrt((dx / 11f) * (dx / 11f) + ((dy + 12) / 16f) * ((dy + 12) / 16f));
                    if (d < 1f) c.Blend(x + dx, y + dy, Rgb(80, 170, 255), 0.4f * (1f - d));
                }
            SaveSprite(c, "SpawnPortalGlow", PivotAt(c, x, y + 1));
        }

        // ------------------------------------------------------------ Health bar, puff, HUD

        // Health bar frame: 1 px dark border around a dark-red track (the empty part of the bar).
        // 9-sliced (1 px border), so one sprite fits the Grunt and the Tank bar widths.
        private static void HealthBarFrame()
        {
            var c = new PixelCanvas(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    c.Put(x, y, (x == 0 || y == 0 || x == 3 || y == 3) ? Rgb(20, 14, 14) : Rgb(70, 20, 20));
            SaveSprite(c, "HealthBarFrame", PivotCenter, new Vector4(1, 1, 1, 1));
        }

        // Soft dust particle for the tower placement puff (tinted and faded by PuffEffect).
        private static void Puff()
        {
            var c = new PixelCanvas(6, 6);
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                {
                    float d = Mathf.Sqrt((x - 2.5f) * (x - 2.5f) + (y - 2.5f) * (y - 2.5f)) / 3f;
                    if (d < 1f) c.Blend(x, y, Rgb(236, 228, 210), 1f - d * d);
                }
            SaveSprite(c, "Puff", PivotCenter);
        }

        // Dark metal HUD panel (reference style): rounded 1 px dark outline, 2 px metal frame lit
        // from the upper left, dark inner field with an inner shadow along its top and left.
        // 24 x 24 px, 9-sliced with a 6 px border.
        private static void HudPanel()
        {
            var c = new PixelCanvas(24, 24);
            MetalBox(c, 24, 24, Rgb(26, 30, 38), Rgb(16, 18, 24));
            SaveSprite(c, "HudPanel", PivotCenter, new Vector4(6, 6, 6, 6));
        }

        // Button: same frame, light grey field (tinted by the Image colour), 16 x 16, 5 px border.
        private static void HudButton()
        {
            var c = new PixelCanvas(16, 16);
            MetalBox(c, 16, 16, Rgb(206, 210, 218), Rgb(168, 172, 182));
            SaveSprite(c, "HudButton", PivotCenter, new Vector4(5, 5, 5, 5));
        }

        private static void MetalBox(PixelCanvas c, int w, int h, Color32 field, Color32 innerShadow)
        {
            Color32 outline = Rgb(8, 9, 12), metal = Rgb(98, 104, 116), lit = Rgb(150, 156, 168), dark = Rgb(62, 66, 76);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                    if (corner) continue; // rounded: corner pixels stay transparent
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    Color32 col;
                    if (edge == 0) col = outline;
                    else if (edge <= 2) col = (x <= 2 || y <= 2) ? ((x <= 2 && y <= 2) || x < w - 3 && y < h - 3 ? lit : metal) : dark;
                    else if (edge == 3) col = outline;
                    else col = (x == 4 || y == 4) ? innerShadow : field;
                    c.Put(x, y, col);
                }
            // Inner corner of the frame is also rounded.
            c.Put(1, 1, outline); c.Put(w - 2, 1, outline); c.Put(1, h - 2, outline); c.Put(w - 2, h - 2, outline);
        }

        // Gold coin: outlined disc, lighter rim highlight upper left, a darker inner ring.
        private static void IconCoin()
        {
            var c = new PixelCanvas(12, 12);
            Color32[] gold = { Rgb(150, 92, 10), Rgb(214, 150, 22), Rgb(244, 200, 48), Rgb(255, 236, 120) };
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    float dx = x - 5.5f, dy = y - 5.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 5.6f) continue;
                    Color32 col = d > 4.7f ? Rgb(90, 52, 6) : d > 3.3f && d < 4.1f ? gold[1] : (dx + dy < -2f ? gold[3] : gold[2]);
                    c.Put(x, y, col);
                }
            c.Put(4, 3, Rgb(255, 255, 255));
            SaveSprite(c, "IconCoin", PivotCenter);
        }

        // Heart: two lobes and a point, dark outline, light highlight on the left lobe.
        private static void IconHeart()
        {
            var c = new PixelCanvas(13, 12);
            bool In(float x, float y)
            {
                float l = (x - 3.6f) * (x - 3.6f) + (y - 3.6f) * (y - 3.6f), r = (x - 8.4f) * (x - 8.4f) + (y - 3.6f) * (y - 3.6f);
                bool lobes = l <= 9.6f || r <= 9.6f;
                bool point = y >= 3.6f && y <= 10.6f && Mathf.Abs(x - 6f) <= (10.6f - y) * 0.85f;
                return lobes || point;
            }
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 13; x++)
                {
                    float fx = x + 0.5f - 0.5f, fy = y + 0.5f;
                    if (!In(fx, fy)) continue;
                    bool edge = !In(fx - 1, fy) || !In(fx + 1, fy) || !In(fx, fy - 1) || !In(fx, fy + 1);
                    c.Put(x, y, edge ? Rgb(70, 10, 18) : Rgb(220, 48, 64));
                }
            c.Put(3, 3, Rgb(255, 170, 176)); c.Put(4, 3, Rgb(255, 170, 176)); c.Put(3, 4, Rgb(255, 170, 176));
            SaveSprite(c, "IconHeart", PivotCenter);
        }

        // Left-pointing arrow (the right one is the same image mirrored), light with a dark outline.
        private static void IconArrow()
        {
            var c = new PixelCanvas(9, 11);
            for (int y = 0; y < 11; y++)
                for (int x = 0; x < 9; x++)
                {
                    float half = (x + 0.5f) * 0.62f; // widens to the right
                    float dy = Mathf.Abs(y + 0.5f - 5.5f);
                    if (dy > half + 0.01f) continue;
                    bool edge = dy > half - 1.05f || x == 8;
                    c.Put(x, y, edge ? Rgb(12, 14, 18) : Rgb(232, 228, 214));
                }
            SaveSprite(c, "IconArrow", PivotCenter);
        }

        // Two stone pillars with a lintel and a red banner (reference gate()). The red glow lies
        // only on the ground in front of it (the reference also tinted the pillars).
        private static void Gate()
        {
            var c = new PixelCanvas(40, 48);
            int x = 20, y = 42;
            c.Shadow(x, y, 16, 6, 0.35f);
            // Red glow on the ground in front of the gate only; the pillars are drawn over it.
            for (int dy = -3; dy <= 4; dy++)
                for (int dx = -12; dx <= 12; dx++)
                {
                    float d = (dx / 12f) * (dx / 12f) + (dy / 4f) * (dy / 4f);
                    if (d <= 1f) c.Blend(x + dx, y + 1 + dy, Rgb(255, 60, 50), 0.22f * (1f - d));
                }
            c.FillPolygon(Pts(x, y, -14, 0, -8, -30, -4, -30, -4, 0, -10, 3), Rgb(146, 142, 136));
            c.FillPolygon(Pts(x, y, 4, 0, 4, -30, 10, -30, 14, 0, 8, 3), Rgb(112, 110, 106));
            c.FillPolygon(Pts(x, y, -12, -30, 12, -30, 12, -34, -12, -34), Rgb(176, 172, 164));
            c.FillPolygon(Pts(x, y, -6, -30, 6, -30, 6, -10, 0, -5, -6, -10), Rgb(190, 36, 40));
            c.FillPolygon(Pts(x, y, -2, -22, 2, -22, 2, -18, -2, -18), Rgb(240, 236, 230));
            SaveSprite(c, "GoalGate", PivotAt(c, x, y + 1));
        }

        // ------------------------------------------------------------ Characters
        // Ported from Reference/enemy_skins_reference.py: shapes, 4-tone ramps and a dark outline
        // pass. The legs alternate between the frames and the body bobs 1 px. Both face right
        // (pupils, dagger and shield on the right); SpriteRenderer.flipX mirrors them.

        private const int GoblinPivotX = 13, OgrePivotX = 20; // Body center, so a flip stays in place.

        // Crops both frames to the same rows, from the head of the higher frame down to the feet,
        // so the sprite's top is the top of the head (the health bar sits right above it) and
        // does not change between frames. Pivot at the feet (bottom edge, body center).
        private static void SaveWalkFrames(string name, PixelCanvas frame0, PixelCanvas frame1, int pivotX)
        {
            frame0.OpaqueRows(out int first0, out int last0);
            frame1.OpaqueRows(out int first1, out int last1);
            int first = Mathf.Min(first0, first1), last = Mathf.Max(last0, last1);
            PixelCanvas a = frame0.CropRows(first, last), b = frame1.CropRows(first, last);
            Vector2 pivot = PivotAt(a, pivotX, a.Height);
            SaveSprite(a, $"{name}_0", pivot);
            SaveSprite(b, $"{name}_1", pivot);
        }

        private static PixelCanvas Goblin(int frame)
        {
            Color32[] skin = { Rgb(150, 92, 10), Rgb(214, 150, 22), Rgb(244, 200, 48), Rgb(255, 232, 110) };
            Color32[] tunic = { Rgb(70, 42, 26), Rgb(104, 66, 40), Rgb(132, 88, 54), Rgb(160, 112, 70) };
            Color32[] steel = { Rgb(60, 62, 72), Rgb(98, 102, 114), Rgb(140, 146, 158), Rgb(190, 196, 208) };
            Color32 outline = Rgb(40, 24, 10), shoe = Rgb(50, 34, 22);
            var c = new PixelCanvas(26, 30);
            int bob = frame == 1 ? 1 : 0, liftLeft = frame == 0 ? 0 : 1, liftRight = frame == 1 ? 0 : 1;
            // Legs: the lifted one is a pixel shorter.
            c.FillRect(9, 22, 11, 25 - liftLeft, tunic[0]);
            c.FillRect(9, 25 - liftLeft, 12, 26 - liftLeft, shoe);
            c.FillRect(14, 22, 16, 25 - liftRight, tunic[1]);
            c.FillRect(14, 25 - liftRight, 17, 26 - liftRight, shoe);
            // Back arm, body, belt with buckle.
            c.ShadedEllipse(7, 18 - bob, 2.2f, 3f, skin);
            c.ShadedEllipse(13, 19 - bob, 5.5f, 5.2f, tunic);
            c.FillRect(9, 21 - bob, 17, 21 - bob, Rgb(40, 30, 20));
            c.Put(13, 21 - bob, Rgb(220, 190, 70));
            // Front arm with the dagger.
            c.Line(19, 17 - bob, 23, 11 - bob, steel[2]);
            c.Line(19, 16 - bob, 23, 10 - bob, steel[3]);
            c.FillRect(18, 17 - bob, 19, 17 - bob, Rgb(90, 60, 40));
            c.ShadedEllipse(19, 18 - bob, 2.2f, 2.6f, skin);
            // Pointed ears, head.
            c.FillPolygonWithEdges(new[] { new Vector2(7, 11 - bob), new Vector2(1, 7 - bob), new Vector2(7, 14 - bob) }, skin[2]);
            c.FillPolygonWithEdges(new[] { new Vector2(19, 11 - bob), new Vector2(25, 7 - bob), new Vector2(19, 14 - bob) }, skin[1]);
            c.ShadedEllipse(13, 11 - bob, 6.5f, 5.6f, skin);
            // Face: eyes looking right, brows, mouth with two teeth.
            c.FillRect(10, 10 - bob, 11, 11 - bob, Rgb(255, 255, 255));
            c.FillRect(15, 10 - bob, 16, 11 - bob, Rgb(255, 255, 255));
            c.Put(11, 11 - bob, Rgb(30, 10, 10));
            c.Put(16, 11 - bob, Rgb(30, 10, 10));
            c.Line(9, 8 - bob, 12, 9 - bob, outline);
            c.Line(17, 8 - bob, 14, 9 - bob, outline);
            c.FillRect(11, 14 - bob, 15, 14 - bob, Rgb(70, 30, 20));
            c.Put(12, 15 - bob, Rgb(250, 250, 240));
            c.Put(14, 15 - bob, Rgb(250, 250, 240));
            c.Outline(outline);
            return c;
        }

        private static PixelCanvas Ogre(int frame)
        {
            Color32[] skin = { Rgb(64, 32, 104), Rgb(106, 60, 152), Rgb(146, 92, 198), Rgb(190, 146, 238) };
            Color32[] steel = { Rgb(60, 62, 72), Rgb(98, 102, 114), Rgb(140, 146, 158), Rgb(190, 196, 208) };
            Color32[] wood = { Rgb(70, 44, 26), Rgb(104, 70, 42), Rgb(134, 94, 58), Rgb(160, 118, 78) };
            Color32 boot = Rgb(48, 40, 36), tusk = Rgb(250, 248, 236);
            var c = new PixelCanvas(40, 44);
            int bob = frame == 1 ? 1 : 0, liftLeft = frame == 0 ? 1 : 0, liftRight = frame == 1 ? 1 : 0;
            // Legs with boots: the lifted one is a pixel shorter.
            c.FillRect(11, 32, 16, 38 - liftLeft, skin[0]);
            c.FillRect(10, 38 - liftLeft, 17, 40 - liftLeft, boot);
            c.FillRect(23, 32, 28, 38 - liftRight, skin[1]);
            c.FillRect(22, 38 - liftRight, 29, 40 - liftRight, boot);
            // Back arm fist, body, belt with buckle.
            c.ShadedEllipse(7, 29 - bob, 3.6f, 5f, skin);
            c.ShadedEllipse(20, 27 - bob, 11f, 9f, skin);
            c.FillRect(11, 30 - bob, 29, 32 - bob, Rgb(70, 66, 62));
            c.FillRect(18, 30 - bob, 22, 32 - bob, Rgb(210, 170, 60));
            // Round shield on the front arm.
            c.ShadedEllipse(32, 29 - bob, 6f, 6.4f, wood);
            c.ShadedEllipse(32, 29 - bob, 2.2f, 2.4f, steel);
            c.ShadedEllipse(33, 27 - bob, 3f, 3f, skin);
            // Steel shoulder plates with rivets.
            c.ShadedEllipse(10, 20 - bob, 5f, 3.6f, steel);
            c.ShadedEllipse(30, 20 - bob, 5f, 3.6f, steel);
            foreach (int px in new[] { 8, 12, 28, 32 })
            {
                c.Put(px, 16 - bob, steel[3]);
                c.Put(px, 17 - bob, steel[2]);
            }
            // Head: yellow eyes looking right, brows, mouth and two tusks.
            c.ShadedEllipse(20, 14 - bob, 6.8f, 6f, skin);
            c.FillRect(16, 12 - bob, 18, 13 - bob, Rgb(255, 230, 120));
            c.FillRect(22, 12 - bob, 24, 13 - bob, Rgb(255, 230, 120));
            c.Put(17, 13 - bob, Rgb(120, 10, 10));
            c.Put(23, 13 - bob, Rgb(120, 10, 10));
            c.Line(15, 10 - bob, 19, 12 - bob, Rgb(30, 10, 40));
            c.Line(25, 10 - bob, 21, 12 - bob, Rgb(30, 10, 40));
            c.FillRect(17, 18 - bob, 23, 18 - bob, Rgb(50, 20, 60));
            c.FillPolygonWithEdges(new[] { new Vector2(17, 18 - bob), new Vector2(16, 15 - bob), new Vector2(18, 18 - bob) }, tusk);
            c.FillPolygonWithEdges(new[] { new Vector2(23, 18 - bob), new Vector2(24, 15 - bob), new Vector2(22, 18 - bob) }, tusk);
            c.Outline(Rgb(26, 12, 40));
            return c;
        }

        // Soft ground shadow with explicit radii, centered on the feet (per enemy type).
        private static void EllipseShadow(string name, int rx, int ry)
        {
            var c = new PixelCanvas(2 * rx + 4, 2 * ry + 4);
            c.Shadow(rx + 2, ry + 2, rx, ry, 0.4f);
            SaveSprite(c, name, PivotAt(c, rx + 2.5f, ry + 2.5f));
        }

        // ------------------------------------------------------------ Tower, enemies, projectile

        // Wooden tower on a stone base band, with a blue banner on the left face, a dark door, a
        // battlement with 4 merlons and a small cannon (reference tower()). Left face lighter,
        // right face darker (light from the upper left). One tile wide, pivot at the feet (the
        // center of its footprint). The ground shadow is a separate sprite (TowerShadow), so the
        // fire kick, which scales only this body, never scales the shadow.
        // Fix vs the reference: its 4th merlon sat 3 px above the back corner of the battlement
        // (a stray block floating behind the cannon). All 4 merlons now stand on the corners of
        // the battlement, and the cannon sits 2 px further forward so the back merlon stays
        // visible behind it, attached to the rim.
        private static void Tower()
        {
            var c = new PixelCanvas(TileW, 52);
            int x = 32, y = 44;
            const int w = 12, h = 6, H = 28;
            Color32 wood = Rgb(150, 104, 66), woodDark = Rgb(106, 72, 48), plank = Rgb(112, 76, 48);
            c.FillPolygon(Pts(x, y, -w, 0, 0, h, 0, h - H, -w, -H), wood);
            c.FillPolygon(Pts(x, y, 0, h, w, 0, w, -H, 0, h - H), woodDark);
            // Stone base band (9 rows), chequered, on both faces.
            for (int face = 0; face < 2; face++)
            {
                bool left = face == 0;
                float shade = left ? 1f : 0.72f;
                for (int k = 0; k < 9; k++)
                    for (int i = 0; i < w; i++)
                    {
                        int yy = (int)(y + h * (1f - i / (float)w)) - k;
                        Color32 stone = ((k / 3 + i / 4) % 2 == 0) ? Rgb(158, 154, 146) : Rgb(138, 134, 128);
                        c.Put(left ? x - i - 1 : x + i, yy, Mul(stone, shade));
                    }
            }
            // Plank lines every 4 rows above the stone band.
            for (int k = 10; k < H; k += 4)
                for (int i = 0; i < w; i++)
                {
                    int yy = (int)(y + h * (1f - i / (float)w)) - k;
                    c.Put(x - i - 1, yy, plank);
                    c.Put(x + i, yy, Mul(plank, 0.72f));
                }
            // Blue banner on the left face, dark door near the front corner.
            for (int dy = 0; dy < 10; dy++)
                for (int dx = 0; dx < 5; dx++)
                    c.Put(x - w + 4 + dx, (int)(y + h * (1f - (w - 4 - dx) / (float)w)) - 16 - dy, Rgb(50, 92, 200));
            for (int dy = 0; dy < 8; dy++)
                for (int dx = 0; dx < 4; dx++)
                    c.Put(x - 4 + dx, (int)(y + h * (1f - (4 - dx) / (float)w)) - dy, Rgb(40, 28, 22));
            // Battlement top and its inner floor.
            c.FillPolygon(Pts(x, y, -w, -H, 0, h - H, w, -H, 0, -h - H), Rgb(176, 172, 164));
            c.FillPolygon(Pts(x, y, -w + 4, -H, 0, h - H - 2, w - 4, -H, 0, -h - H + 2), Rgb(104, 94, 86));
            // Merlons on the 4 corners: back first, then the cannon, then left, right and front.
            Merlon(c, x - 2, y - h - H + 1);
            int cy = y - H + 1;
            c.FillPolygon(Pts(x, cy, -3, -1, 3, -1, 3, -5, -3, -5), Rgb(70, 70, 78));
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 3; j++)
                    c.Put(x + i, cy - 6 - i / 2 + j, j < 2 ? Rgb(54, 54, 60) : Rgb(92, 92, 100));
            Merlon(c, x - w + 1, y - H);
            Merlon(c, x + w - 5, y - H);
            Merlon(c, x - 2, y + h - H - 1);
            SaveSprite(c, "TowerBody", PivotAt(c, x, y));
        }

        private static void Merlon(PixelCanvas c, int mx, int my)
        {
            c.FillPolygon(Pts(mx, my, 0, -3, 4, -3, 4, 1, 0, 1), Rgb(186, 182, 174));
            c.FillPolygon(Pts(mx, my, 0, -3, 4, -3, 4, -2, 0, -2), Rgb(208, 204, 196));
        }

        // Soft elliptical ground shadow of the tower, offset 3 px right and down from the feet
        // like the reference (separate child, not scaled by the fire kick).
        // M8.4: darker (0.58) and a little larger (19 x 7 px) so it reads on the grass; a soft
        // 2-pixel edge keeps it from looking like a hard disc.
        private static void TowerShadow()
        {
            var c = new PixelCanvas(48, 18);
            c.Shadow(24, 9, 19, 7, 0.32f);
            c.Shadow(24, 9, 17, 6, 0.38f);
            SaveSprite(c, "TowerShadow", PivotAt(c, 21, 6));
        }

        // Glossy ball: 4-tone shaded sphere lit from the upper left, darker rim, dark outline and a
        // white specular highlight (reference ball()). Pivot at the feet, 1 px below the sphere.
        // Its contact shadow is a separate sprite (BallShadow) so the walking bob moves only the ball.
        private static void Ball(string name, int r, Color32[] ramp, Color32 outline)
        {
            int size = 2 * r + 6;
            var c = new PixelCanvas(size, size);
            int feetX = r + 3, feetY = size - 2;
            c.Sphere(feetX, feetY - r - 1, r, ramp, outline);
            SaveSprite(c, name, PivotAt(c, feetX + 0.5f, feetY + 0.5f));
        }

        private static void BallShadow(string name, int r)
        {
            int rx = (int)(r * 1.1f) + 1, ry = Mathf.Max(2, r / 2);
            var c = new PixelCanvas(2 * rx + 4, 2 * ry + 4);
            c.Shadow(rx + 2, ry + 2, rx, ry, 0.4f);
            SaveSprite(c, name, PivotAt(c, rx + 2.5f, ry + 2.5f));
        }

        // Small glowing orange/yellow ball: soft orange glow, a bright core with an outline and a
        // white highlight. Pivot at the center (it flies at the existing flight height).
        private static void Projectile()
        {
            var c = new PixelCanvas(14, 14);
            for (int y = 0; y < 14; y++)
                for (int x = 0; x < 14; x++)
                {
                    float d = Mathf.Sqrt((x - 6.5f) * (x - 6.5f) + (y - 6.5f) * (y - 6.5f));
                    if (d < 6.5f) c.Blend(x, y, Rgb(255, 150, 40), 0.5f * (1f - d / 6.5f));
                }
            Color32[] core = { Rgb(230, 120, 20), Rgb(250, 170, 40), Rgb(255, 215, 80), Rgb(255, 245, 170) };
            c.Sphere(7, 7, 3, core, Rgb(150, 64, 10));
            SaveSprite(c, "Projectile", PivotAt(c, 7.5f, 7.5f));
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
