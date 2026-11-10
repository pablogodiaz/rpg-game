using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ReadyCheck
{
    /// <summary>
    /// Generates all art at runtime in a top-down, factory-sim inspired pixel style:
    /// units seen from above that rotate to face their target, every part with its own dark outline,
    /// simple top-left lighting, tile-based metal floors and rock walls, glowing lamps and bullets.
    /// Everything shares one pixel density (<see cref="PPU"/>) so pixels line up across sprites.
    /// Real hand-made sprite sheets can replace these functions one by one later.
    /// </summary>
    public static class PixelArt
    {
        /// <summary>Pixels per world unit for every pixel-art sprite.</summary>
        public const float PPU = 12f;
        public const int TilePixels = 12;
        public const int ArenaTiles = 36;

        static readonly Color32 OutlineColor = new Color32(30, 26, 38, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Material spriteMaterial;

        /// <summary>Unity's default sprite material, reused for trails so no shader lookups are needed.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var tmp = new GameObject("tmp");
                    spriteMaterial = tmp.AddComponent<SpriteRenderer>().sharedMaterial;
                    Object.Destroy(tmp);
                }
                return spriteMaterial;
            }
        }

        // ----- Canvas ------------------------------------------------------------------------------

        /// <summary>A small pixel canvas. Shapes are drawn as "parts" that each get an outline and bevel.</summary>
        class PixelCanvas
        {
            public readonly int W, H;
            public readonly Color32[] Px;

            public PixelCanvas(int w, int h)
            {
                W = w;
                H = h;
                Px = new Color32[w * h];
            }

            bool In(bool[] mask, int x, int y) => x >= 0 && y >= 0 && x < W && y < H && mask[y * W + x];

            public void Part(Func<float, float, bool> inside, Color32 color, bool outline = true, bool bevel = true)
            {
                var mask = new bool[W * H];
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    mask[y * W + x] = inside(x + 0.5f, y + 0.5f);

                if (outline)
                {
                    for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (mask[y * W + x]) continue;
                        if (In(mask, x - 1, y) || In(mask, x + 1, y) || In(mask, x, y - 1) || In(mask, x, y + 1))
                            Px[y * W + x] = OutlineColor;
                    }
                }

                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!mask[y * W + x]) continue;
                    var c = color;
                    if (bevel)
                    {
                        bool lit = !In(mask, x, y + 1) || !In(mask, x - 1, y);
                        bool dark = !In(mask, x, y - 1) || !In(mask, x + 1, y);
                        if (lit) c = Shade(color, 1.22f, 12);
                        else if (dark) c = Shade(color, 0.72f, 0);
                    }
                    Px[y * W + x] = c;
                }
            }

            public Sprite ToSprite(float ppu, Vector2 pivot, FilterMode filter = FilterMode.Point)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.filterMode = filter;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.SetPixels32(Px);
                tex.Apply(false, false);
                return Sprite.Create(tex, new Rect(0, 0, W, H), pivot, ppu, 0, SpriteMeshType.FullRect);
            }
        }

        static Color32 Shade(Color32 c, float mul, int add) => new Color32(
            (byte)Mathf.Clamp(c.r * mul + add, 0, 255),
            (byte)Mathf.Clamp(c.g * mul + add, 0, 255),
            (byte)Mathf.Clamp(c.b * mul + add, 0, 255), c.a);

        static Func<float, float, bool> Circle(float cx, float cy, float r) =>
            (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        static Func<float, float, bool> Ellipse(float cx, float cy, float rx, float ry) =>
            (x, y) => ((x - cx) / rx) * ((x - cx) / rx) + ((y - cy) / ry) * ((y - cy) / ry) <= 1f;

        static Func<float, float, bool> Box(float x0, float y0, float x1, float y1) =>
            (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;

        static Func<float, float, bool> Ring(float cx, float cy, float r0, float r1) =>
            (x, y) =>
            {
                float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                return d2 >= r0 * r0 && d2 <= r1 * r1;
            };

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (!Cache.TryGetValue(key, out var sprite))
            {
                sprite = make();
                Cache[key] = sprite;
            }
            return sprite;
        }

        // ----- Party members (seen from above, facing up = +Z) ---------------------------------------

        public static Sprite Character(MemberProfile p) => Cached("char_" + p.Name, () => DrawCharacter(p));

        static Sprite DrawCharacter(MemberProfile p)
        {
            // Designed on an 18px grid around (9, 9), drawn at K x size so units read clearly next to the boss.
            const float k = 1.4f;
            const int s = 26;
            const float c = s / 2f;
            var cv = new PixelCanvas(s, s);
            var metal = new Color32(176, 184, 198, 255);
            var wood = new Color32(128, 88, 52, 255);

            Func<float, float, bool> Cir(float dx, float dy, float r) => Circle(c + dx * k, c + dy * k, r * k);
            Func<float, float, bool> Ell(float dx, float dy, float rx, float ry) => Ellipse(c + dx * k, c + dy * k, rx * k, ry * k);
            Func<float, float, bool> Rct(float dx0, float dy0, float dx1, float dy1) => Box(c + dx0 * k, c + dy0 * k, c + dx1 * k, c + dy1 * k);

            if (p.Class == ClassKind.Leader) cv.Part(Ell(0f, -3f, 5.4f, 3f), p.Secondary); // cape

            // hands, body, team-colored shoulder pads
            cv.Part(Cir(-4.9f, 1.2f, 1.7f), p.Skin);
            cv.Part(Cir(4.9f, 1.2f, 1.7f), p.Skin);
            cv.Part(Ell(0f, 0f, 5.2f, 3.5f), p.Primary);
            cv.Part(Rct(-4.6f, -0.6f, -3.4f, 0.6f), p.Accent, false, false);
            cv.Part(Rct(3.4f, -0.6f, 4.6f, 0.6f), p.Accent, false, false);

            // head
            switch (p.Class)
            {
                case ClassKind.Guardian:
                    cv.Part(Cir(0f, 0.8f, 2.9f), metal);
                    cv.Part(Rct(-0.5f, -1.6f, 0.5f, 3f), p.Accent, false, false); // plume
                    break;
                case ClassKind.Cleric:
                    cv.Part(Cir(0f, 0.8f, 2.9f), p.Primary); // hood
                    cv.Part(Ell(0f, 3.1f, 1.7f, 0.75f), p.Skin, false, false);
                    break;
                case ClassKind.Mage:
                    cv.Part(Cir(0f, 0.8f, 4.2f), p.Secondary); // hat brim
                    cv.Part(Cir(0f, 0.8f, 2.3f), p.Primary);
                    cv.Part(Cir(0f, 0.8f, 0.9f), p.Accent, false, false);
                    break;
                case ClassKind.Duelist:
                    cv.Part(Cir(0f, 0.8f, 2.9f), p.Hair);
                    cv.Part(Ell(0f, 3.1f, 1.7f, 0.75f), p.Secondary, false, false); // mask
                    break;
                default:
                    cv.Part(Cir(0f, 0.8f, 2.9f), p.Hair);
                    cv.Part(Ell(0f, 3.1f, 1.7f, 0.75f), p.Skin, false, false);
                    break;
            }

            // held gear
            switch (p.Class)
            {
                case ClassKind.Leader:
                    cv.Part((x, y) => Ring(c, c + 0.8f * k, 1.6f * k, 2.5f * k)(x, y), p.Accent, false, false); // crown
                    cv.Part(Rct(4.4f, 1f, 5.4f, 5.6f), wood);                                                  // scepter
                    cv.Part(Cir(4.9f, 6f, 1.1f), p.Accent);
                    break;
                case ClassKind.Guardian:
                    cv.Part(Rct(-5f, 4.2f, 5f, 6.2f), metal);                                                  // tower shield
                    cv.Part(Rct(-1f, 4.6f, 1f, 5.8f), p.Accent, false, false);
                    break;
                case ClassKind.Cleric:
                    cv.Part(Rct(4.4f, 0.5f, 5.4f, 6f), wood);                                                  // staff
                    cv.Part(Cir(4.9f, 6.4f, 1.1f), p.Accent);
                    break;
                case ClassKind.Duelist:
                    cv.Part(Rct(-5.4f, 2f, -4.4f, 6.2f), metal);                                               // daggers
                    cv.Part(Rct(4.4f, 2f, 5.4f, 6.2f), metal);
                    break;
                case ClassKind.Mage:
                    cv.Part(Cir(4.9f, 3.4f, 1.4f), p.Accent);                                                  // orb
                    break;
            }

            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        }

        /// <summary>A boot seen from above. Two of these step under every unit.</summary>
        public static Sprite Foot(Color32 color, bool big) => Cached($"foot_{color.r}_{color.g}_{color.b}_{big}", () =>
        {
            var cv = big ? new PixelCanvas(11, 13) : new PixelCanvas(8, 10);
            if (big) cv.Part(Ellipse(5.5f, 6.5f, 3.4f, 4.7f), color);
            else cv.Part(Ellipse(4f, 5f, 2.3f, 3.6f), color);
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });

        // ----- Rotgut, seen from above --------------------------------------------------------------

        public static Sprite Boss() => Cached("boss", () =>
        {
            const int s = 56;
            const float c = 28f;
            var cv = new PixelCanvas(s, s);
            var skin = new Color32(134, 162, 92, 255);
            var skinDark = new Color32(100, 126, 70, 255);
            var bib = new Color32(236, 230, 214, 255);
            var stain = new Color32(178, 112, 60, 255);
            var eye = new Color32(255, 72, 52, 255);
            var tusk = new Color32(250, 246, 222, 255);
            var meat = new Color32(170, 92, 58, 255);
            var cuff = new Color32(222, 70, 70, 255);

            cv.Part(Ellipse(7f, 30f, 4.2f, 7.5f), skin);              // arms
            cv.Part(Ellipse(49f, 30f, 4.2f, 7.5f), skin);
            cv.Part(Box(3.6f, 34f, 10.4f, 35.6f), cuff);              // enemy-colored cuffs
            cv.Part(Box(45.6f, 34f, 52.4f, 35.6f), cuff);
            cv.Part(Circle(7f, 38.8f, 3.6f), skinDark);               // fists
            cv.Part(Circle(49f, 38.8f, 3.6f), skinDark);
            cv.Part(Box(48.5f, 41f, 50f, 46.5f), tusk);               // drumstick bone
            cv.Part(Ellipse(49.2f, 48.6f, 3f, 3.6f), meat);
            cv.Part(Ellipse(c, 26f, 19.5f, 17f), skin);               // the belly
            foreach (var spot in new[] { new Vector2(20, 16), new Vector2(34, 13), new Vector2(26, 11), new Vector2(39, 22), new Vector2(16, 26) })
                cv.Part(Circle(spot.x, spot.y, 1.3f), skinDark, false, false);
            cv.Part(Ellipse(c, 36f, 9.5f, 5f), bib);                  // stained bib
            cv.Part(Circle(24f, 37f, 1f), stain, false, false);
            cv.Part(Circle(31f, 35f, 1.2f), stain, false, false);
            cv.Part(Circle(28.5f, 38.5f, 0.8f), stain, false, false);
            cv.Part(Circle(c, 42f, 8f), skin);                        // head
            cv.Part(Box(22f, 46.4f, 34f, 47.4f), skinDark, false, false); // brow
            cv.Part(Circle(24.5f, 45f, 1.3f), eye, false, false);
            cv.Part(Circle(31.5f, 45f, 1.3f), eye, false, false);
            cv.Part(Box(23.6f, 48.4f, 25f, 50.6f), tusk);             // tusks
            cv.Part(Box(31f, 48.4f, 32.4f, 50.6f), tusk);
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });

        // ----- Effects (smooth shapes, like vector effects) ------------------------------------------

        /// <summary>White ring with a faint fill, 2 world units across (so localScale = radius).</summary>
        public static Sprite RingSprite() => Cached("ring", () =>
        {
            const int size = 128;
            var cv = new PixelCanvas(size, size);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                byte a = 0;
                if (d <= c - 1f) a = (byte)(d >= c - 5f ? 255 : 40);
                cv.Px[y * size + x] = new Color32(255, 255, 255, a);
            }
            return cv.ToSprite(size / 2f, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        });

        /// <summary>Solid white disc, 2 world units across.</summary>
        public static Sprite Disc() => Cached("disc", () =>
        {
            const int size = 96;
            var cv = new PixelCanvas(size, size);
            cv.Part(Circle(size / 2f, size / 2f, size / 2f - 1f), new Color32(255, 255, 255, 255), false, false);
            return cv.ToSprite(size / 2f, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        });

        /// <summary>Soft radial glow, 2 world units across. Tint it for light pools and muzzle glows.</summary>
        public static Sprite Glow() => Cached("glow", () =>
        {
            const int size = 64;
            var cv = new PixelCanvas(size, size);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c);
                float a = (1f - d) * (1f - d);
                cv.Px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return cv.ToSprite(size / 2f, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        });

        /// <summary>A bullet: colored body with a white-hot core, pointing up.</summary>
        public static Sprite Bullet(Color color)
        {
            Color32 c32 = color;
            return Cached($"bullet_{c32.r}_{c32.g}_{c32.b}", () =>
            {
                var cv = new PixelCanvas(6, 10);
                cv.Part(Ellipse(3f, 5f, 1.6f, 3.8f), c32, false, false);
                cv.Part(Ellipse(3f, 5.8f, 0.75f, 2.4f), new Color32(255, 255, 250, 255), false, false);
                return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
            });
        }

        /// <summary>Scorch mark left where a party member went down.</summary>
        public static Sprite Scorch() => Cached("scorch", () =>
        {
            const int size = 20;
            var cv = new PixelCanvas(size, size);
            var rng = new System.Random(99);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                if (d < 0.55f + 0.4f * (float)rng.NextDouble())
                    cv.Px[y * size + x] = new Color32(14, 10, 16, (byte)(d < 0.5f ? 150 : 90));
            }
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });

        /// <summary>Grease fire frames, sized so the sprite is exactly 2 x radius across at PPU density.</summary>
        public static Sprite[] FireFrames(float radius)
        {
            const int frameCount = 3;
            var frames = new Sprite[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                int frame = f;
                frames[f] = Cached($"fire_{radius:0.00}_{frame}", () =>
                {
                    int size = Mathf.Max(8, Mathf.RoundToInt(radius * 2f * PPU));
                    var cv = new PixelCanvas(size, size);
                    var rng = new System.Random(1234 + frame * 77);
                    float c = size / 2f;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / (c - 0.5f);
                        if (d > 1f) continue;
                        double n = rng.NextDouble();
                        if (d > 0.6f && n < 0.45) continue;
                        Color32 col;
                        if (n > 0.95) col = new Color32(255, 236, 140, 230);       // flickering flame tips
                        else if (d < 0.25f) col = new Color32(255, 200, 90, 190);
                        else if (d < 0.6f) col = new Color32(240, 120, 40, 160);
                        else col = new Color32(170, 56, 30, 140);
                        cv.Px[y * size + x] = col;
                    }
                    return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
                });
            }
            return frames;
        }

        /// <summary>A 2x2 pixel square, the basic particle.</summary>
        public static Sprite Particle() => Cached("particle", () =>
        {
            var cv = new PixelCanvas(2, 2);
            for (int i = 0; i < 4; i++) cv.Px[i] = new Color32(255, 255, 255, 255);
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });

        /// <summary>A crescent slash arc (2 x 1 world units, opening downward), smooth like a vector effect.</summary>
        public static Sprite SlashArc() => Cached("slash", () =>
        {
            const int w = 64, h = 32;
            var cv = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float outer = ((px - 32f) / 31f) * ((px - 32f) / 31f) + (py / 30f) * (py / 30f);
                float inner = ((px - 32f) / 26f) * ((px - 32f) / 26f) + ((py + 4f) / 24f) * ((py + 4f) / 24f);
                if (outer <= 1f && inner > 1f)
                {
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(px - 32f) / 32f); // fade toward the tips
                    cv.Px[y * w + x] = new Color32(255, 255, 255, (byte)(255f * edge));
                }
            }
            return cv.ToSprite(32f, new Vector2(0.5f, 0.25f), FilterMode.Bilinear);
        });

        /// <summary>A flaming meteor rock seen from above.</summary>
        public static Sprite Meteor() => Cached("meteor", () =>
        {
            var cv = new PixelCanvas(16, 16);
            cv.Part(Circle(8f, 8f, 6f), new Color32(255, 140, 50, 255), false, false);
            cv.Part(Circle(8f, 8f, 4.6f), new Color32(96, 70, 64, 255));
            cv.Part(Circle(6.5f, 9.5f, 1.2f), new Color32(255, 196, 90, 255), false, false);
            cv.Part(Circle(9.8f, 6.8f, 1f), new Color32(255, 160, 70, 255), false, false);
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });

        // ----- Arena -------------------------------------------------------------------------------

        enum TileKind { Floor, Wall, Void }

        static TileKind KindAt(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= ArenaTiles || ty >= ArenaTiles) return TileKind.Void;
            float half = ArenaTiles / 2f;
            float wx = tx + 0.5f - half, wz = ty + 0.5f - half;
            float d = Mathf.Sqrt(wx * wx + wz * wz);
            if (d < 13.4f) return TileKind.Floor;
            if (d < 16.2f) return TileKind.Wall;
            return TileKind.Void;
        }

        /// <summary>The whole arena as one tile map texture: metal floor plates inside a ring of rock walls.</summary>
        public static Sprite ArenaFloor() => Cached("arena", () =>
        {
            const int tp = TilePixels;
            int size = ArenaTiles * tp;
            var cv = new PixelCanvas(size, size);
            float half = ArenaTiles / 2f;

            for (int ty = 0; ty < ArenaTiles; ty++)
            for (int tx = 0; tx < ArenaTiles; tx++)
            {
                var kind = KindAt(tx, ty);
                var rng = new System.Random(tx * 73856093 ^ ty * 19349663);
                float wx = tx + 0.5f - half, wz = ty + 0.5f - half;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                int variant = rng.Next(100);

                for (int ly = 0; ly < tp; ly++)
                for (int lx = 0; lx < tp; lx++)
                {
                    int noise = rng.Next(-3, 4);
                    Color32 col;
                    switch (kind)
                    {
                        case TileKind.Floor:
                            col = FloorPixel(lx, ly, tp, variant, dist);
                            if (KindAt(tx, ty + 1) == TileKind.Wall && ly >= tp - 4) col = Shade(col, 0.6f, 0);
                            else if (KindAt(tx - 1, ty) == TileKind.Wall && lx <= 2) col = Shade(col, 0.72f, 0);
                            break;
                        case TileKind.Wall:
                            noise = rng.Next(-7, 8);
                            col = new Color32(98, 90, 106, 255);
                            if (ly == 0 || lx == tp - 1) col = Shade(col, 0.9f, 0);
                            if (KindAt(tx, ty + 1) != TileKind.Wall && ly >= tp - 2) col = new Color32(128, 120, 138, 255);
                            else if (KindAt(tx, ty - 1) != TileKind.Wall && ly <= 1) col = new Color32(58, 52, 66, 255);
                            else if (KindAt(tx - 1, ty) != TileKind.Wall && lx == 0) col = new Color32(122, 114, 132, 255);
                            else if (KindAt(tx + 1, ty) != TileKind.Wall && lx == tp - 1) col = new Color32(62, 56, 70, 255);
                            break;
                        default:
                            col = rng.Next(60) == 0 ? new Color32(38, 34, 46, 255) : new Color32(22, 18, 28, 255);
                            noise = 0;
                            break;
                    }
                    col = new Color32((byte)Mathf.Clamp(col.r + noise, 0, 255), (byte)Mathf.Clamp(col.g + noise, 0, 255),
                        (byte)Mathf.Clamp(col.b + noise, 0, 255), 255);
                    cv.Px[(ty * tp + ly) * size + tx * tp + lx] = col;
                }
            }
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });

        static Color32 FloorPixel(int lx, int ly, int tp, int variant, float dist)
        {
            bool lightEdge = lx == 0 || ly == tp - 1;
            bool darkEdge = lx == tp - 1 || ly == 0;
            bool inset = lx >= 2 && lx <= tp - 3 && ly >= 2 && ly <= tp - 3;

            Color32 baseCol;
            if (variant < 8) baseCol = new Color32(46, 44, 56, 255);       // vents
            else if (variant < 26) baseCol = new Color32(58, 56, 70, 255); // dark panels
            else baseCol = new Color32(70, 68, 84, 255);                   // metal plates

            if (lightEdge) return Shade(baseCol, 1.2f, 6);
            if (darkEdge) return Shade(baseCol, 0.74f, 0);

            if (variant < 8 && inset && ly % 3 == 1) return new Color32(28, 26, 36, 255);
            if (variant >= 8 && variant < 26 && inset &&
                (lx == 2 || lx == tp - 3 || ly == 2 || ly == tp - 3))
                return Shade(baseCol, 1.25f, 4);
            if (variant >= 90 && (lx == 2 || lx == tp - 3) && (ly == 2 || ly == tp - 3)) return Shade(baseCol, 0.65f, 0); // rivets
            return baseCol;
        }

        /// <summary>A lamp block set into the wall.</summary>
        public static Sprite Lamp() => Cached("lamp", () =>
        {
            var cv = new PixelCanvas(14, 14);
            cv.Part(Box(1.5f, 1.5f, 12.5f, 12.5f), new Color32(88, 90, 104, 255));
            cv.Part(Circle(7f, 7f, 3f), new Color32(255, 186, 96, 255), true, false);
            cv.Part(Circle(7f, 7f, 1.4f), new Color32(255, 240, 190, 255), false, false);
            return cv.ToSprite(PPU, new Vector2(0.5f, 0.5f));
        });
    }
}
