using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Generates all pixel-art sprites at runtime so the first iteration needs no imported art.
    /// Characters use one body template per class plus palette swaps, as planned in the design doc.
    /// Replace these with real sprite sheets when an artist (or you) makes them.
    /// </summary>
    public static class PixelArt
    {
        public const float CharacterPPU = 10f;

        // ----- Character templates (16 x 16, top row first) ---------------------------------------
        // o outline, s skin, e eyes, h hair, p primary, q secondary, m metal, b boots,
        // g accent, w wood, l light/white, f flag

        static readonly string[] Body =
        {
            "......oooo......",
            ".....ohhhho.....",
            "....ohhhhhho....",
            "....ohssssho....",
            "....osesseso....",
            "....osssssso....",
            "...oppossoppo...",
            "..oppppppppppo..",
            "..opqppppppqpo..",
            "..osqppppppqso..",
            "..os.qqmmqq.so..",
            "....oqqqqqqo....",
            "....oqqooqqo....",
            "....oqqooqqo....",
            "....obboobbo....",
            "....oooooooo....",
        };

        static readonly string[] LeaderOverlay =
        {
            ".wfff.gggg......",
            ".wfff...........",
            ".wff............",
            ".w..............",
            ".w..............",
            ".w..............",
            ".w..............",
            ".w..............",
            ".w..............",
            ".w..............",
            ".w..............",
            ".w..............",
        };

        static readonly string[] GuardianOverlay =
        {
            "......mmmm......",
            ".....mmmmmm.....",
            "....mmmmmmmm....",
            "....m......m....",
            "................",
            "................",
            "................",
            "ooooo...........",
            "ommmo...........",
            "omgmo...........",
            "omgmo...........",
            "ommmo...........",
            ".omo............",
            "..o.............",
        };

        static readonly string[] ClericOverlay =
        {
            "..............g.",
            ".............ogo",
            "..............g.",
            "..............w.",
            "..............w.",
            "..............w.",
            "..............w.",
            ".......ll.....w.",
            "......llll....w.",
            ".......ll.....w.",
            "..............w.",
            "..............w.",
            "..............w.",
            "..............w.",
            "..............w.",
        };

        static readonly string[] DuelistOverlay =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "....oqqqqqqo....",
            "................",
            "................",
            "................",
            "................",
            "..m..........m..",
            "..m..........m..",
            "..m..........m..",
        };

        static readonly string[] MageOverlay =
        {
            ".......gg.......",
            "......gggg......",
            "...gggggggggg...",
            "................",
            "................",
            "................",
            "................",
            "................",
            ".............gg.",
            ".............gg.",
        };

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static Sprite ToSprite(int w, int h, Color32[] px, Vector2 pivot, float ppu)
        {
            var tex = NewTexture(w, h);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        static Sprite FromTemplate(string[] rows, string[] overlay, Dictionary<char, Color32> palette, float ppu)
        {
            int h = rows.Length;
            int w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            var px = new Color32[w * h];
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    char ch = c < rows[r].Length ? rows[r][c] : '.';
                    if (overlay != null && r < overlay.Length && c < overlay[r].Length && overlay[r][c] != '.')
                        ch = overlay[r][c];
                    Color32 col = new Color32(0, 0, 0, 0);
                    if (ch != '.' && palette.TryGetValue(ch, out var found)) col = found;
                    px[(h - 1 - r) * w + c] = col;
                }
            }
            return ToSprite(w, h, px, new Vector2(0.5f, 0f), ppu);
        }

        public static Sprite Character(MemberProfile p)
        {
            string key = "char_" + p.Name;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var palette = new Dictionary<char, Color32>
            {
                { 'o', new Color32(26, 20, 34, 255) },
                { 's', p.Skin },
                { 'e', new Color32(24, 20, 30, 255) },
                { 'h', p.Hair },
                { 'p', p.Primary },
                { 'q', p.Secondary },
                { 'm', new Color32(186, 196, 210, 255) },
                { 'b', new Color32(92, 62, 42, 255) },
                { 'g', p.Accent },
                { 'w', new Color32(124, 84, 48, 255) },
                { 'l', new Color32(250, 248, 236, 255) },
                { 'f', p.Accent },
            };

            string[] overlay = null;
            switch (p.Class)
            {
                case ClassKind.Leader: overlay = LeaderOverlay; break;
                case ClassKind.Guardian: overlay = GuardianOverlay; break;
                case ClassKind.Cleric: overlay = ClericOverlay; break;
                case ClassKind.Duelist: overlay = DuelistOverlay; break;
                case ClassKind.Mage: overlay = MageOverlay; break;
            }

            var sprite = FromTemplate(Body, overlay, palette, CharacterPPU);
            Cache[key] = sprite;
            return sprite;
        }

        // ----- Procedural shapes -------------------------------------------------------------------

        static void FillEllipse(Color32[] px, int w, int h, float cx, float cy, float rx, float ry, Color32 col)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            {
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) px[y * w + x] = col;
                }
            }
        }

        static void SetPixel(Color32[] px, int w, int h, int x, int y, Color32 col)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = col;
        }

        static void Outline(Color32[] px, int w, int h, Color32 col)
        {
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a != 0) continue;
                    bool edge = (x > 0 && copy[y * w + x - 1].a != 0) || (x < w - 1 && copy[y * w + x + 1].a != 0)
                                || (y > 0 && copy[(y - 1) * w + x].a != 0) || (y < h - 1 && copy[(y + 1) * w + x].a != 0);
                    if (edge) px[y * w + x] = col;
                }
            }
        }

        /// <summary>Rotgut the Hungry: a big round ogre wearing a stained bib.</summary>
        public static Sprite Boss()
        {
            if (Cache.TryGetValue("boss", out var cached)) return cached;
            const int w = 46, h = 42;
            var px = new Color32[w * h];
            var skin = new Color32(134, 162, 92, 255);
            var skinDark = new Color32(100, 126, 70, 255);
            var bib = new Color32(236, 230, 214, 255);
            var stain = new Color32(178, 112, 60, 255);
            var eye = new Color32(255, 72, 52, 255);
            var mouth = new Color32(62, 24, 30, 255);
            var tusk = new Color32(250, 246, 222, 255);
            var pants = new Color32(112, 72, 52, 255);
            var meat = new Color32(170, 90, 60, 255);

            FillEllipse(px, w, h, 15, 4, 5, 4, pants);                 // legs
            FillEllipse(px, w, h, 31, 4, 5, 4, pants);
            FillEllipse(px, w, h, 23, 16, 17, 12.5f, skin);            // belly
            FillEllipse(px, w, h, 23, 10, 14, 6, skinDark);            // belly shade
            FillEllipse(px, w, h, 5, 17, 4, 7, skin);                  // arms
            FillEllipse(px, w, h, 41, 17, 4, 7, skin);
            FillEllipse(px, w, h, 41, 26, 3.5f, 3.5f, meat);           // drumstick
            for (int y = 18; y < 24; y++) SetPixel(px, w, h, 41, y, tusk);
            FillEllipse(px, w, h, 23, 21, 10, 7, bib);                 // bib
            SetPixel(px, w, h, 19, 22, stain); SetPixel(px, w, h, 20, 22, stain); SetPixel(px, w, h, 20, 21, stain);
            SetPixel(px, w, h, 27, 18, stain); SetPixel(px, w, h, 26, 18, stain); SetPixel(px, w, h, 24, 24, stain);
            FillEllipse(px, w, h, 23, 32, 10, 8, skin);                // head
            FillEllipse(px, w, h, 18, 34, 1.6f, 1.6f, eye);            // eyes
            FillEllipse(px, w, h, 28, 34, 1.6f, 1.6f, eye);
            for (int x = 16; x <= 30; x++) SetPixel(px, w, h, x, 37, skinDark); // brow
            for (int x = 17; x <= 29; x++) SetPixel(px, w, h, x, 29, mouth);    // mouth
            for (int x = 18; x <= 28; x++) SetPixel(px, w, h, x, 28, mouth);
            SetPixel(px, w, h, 18, 30, tusk); SetPixel(px, w, h, 18, 31, tusk); // tusks
            SetPixel(px, w, h, 28, 30, tusk); SetPixel(px, w, h, 28, 31, tusk);
            Outline(px, w, h, new Color32(26, 20, 34, 255));

            var sprite = ToSprite(w, h, px, new Vector2(0.5f, 0f), CharacterPPU);
            Cache["boss"] = sprite;
            return sprite;
        }

        /// <summary>White ring + faint fill, 2 world units across (so scale = radius). Tint with SpriteRenderer.color.</summary>
        public static Sprite Ring()
        {
            if (Cache.TryGetValue("ring", out var cached)) return cached;
            const int size = 64;
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                byte a = 0;
                if (d <= c - 0.5f) a = (byte)(d >= c - 3.5f ? 255 : 46);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            var sprite = ToSprite(size, size, px, new Vector2(0.5f, 0.5f), size / 2f);
            Cache["ring"] = sprite;
            return sprite;
        }

        /// <summary>Solid white disc, 2 world units across.</summary>
        public static Sprite Disc()
        {
            if (Cache.TryGetValue("disc", out var cached)) return cached;
            const int size = 48;
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(d <= c - 0.5f ? 255 : 0));
            }
            var sprite = ToSprite(size, size, px, new Vector2(0.5f, 0.5f), size / 2f);
            Cache["disc"] = sprite;
            return sprite;
        }

        public static Sprite Shadow()
        {
            if (Cache.TryGetValue("shadow", out var cached)) return cached;
            const int w = 12, h = 6;
            var px = new Color32[w * h];
            FillEllipse(px, w, h, 6, 3, 6, 3, new Color32(0, 0, 0, 90));
            var sprite = ToSprite(w, h, px, new Vector2(0.5f, 0.5f), w);
            Cache["shadow"] = sprite;
            return sprite;
        }

        public static Sprite Bolt()
        {
            if (Cache.TryGetValue("bolt", out var cached)) return cached;
            const int s = 5;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int d = Mathf.Abs(x - 2) + Mathf.Abs(y - 2);
                px[y * s + x] = d == 0 ? new Color32(255, 255, 255, 255)
                    : d <= 2 ? new Color32(255, 255, 255, (byte)(d == 1 ? 230 : 140)) : new Color32(0, 0, 0, 0);
            }
            var sprite = ToSprite(s, s, px, new Vector2(0.5f, 0.5f), CharacterPPU);
            Cache["bolt"] = sprite;
            return sprite;
        }

        /// <summary>Two animation frames of a grease fire puddle, 2 world units across.</summary>
        public static Sprite[] FireFrames()
        {
            if (Cache.TryGetValue("fire0", out var f0) && Cache.TryGetValue("fire1", out var f1))
                return new[] { f0, f1 };
            var frames = new Sprite[2];
            for (int frame = 0; frame < 2; frame++)
            {
                const int size = 24;
                var rng = new System.Random(1234 + frame * 77);
                var px = new Color32[size * size];
                float c = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / (c - 0.5f);
                    if (d > 1f) continue;
                    double n = rng.NextDouble();
                    if (d > 0.7f && n < 0.3) continue;
                    Color32 col;
                    if (d < 0.35f || n > 0.93) col = new Color32(255, 226, 96, 230);
                    else if (d < 0.7f) col = new Color32(255, 142, 40, 215);
                    else col = new Color32(206, 62, 32, 200);
                    px[y * size + x] = col;
                }
                frames[frame] = ToSprite(size, size, px, new Vector2(0.5f, 0.5f), size / 2f);
                Cache["fire" + frame] = frames[frame];
            }
            return frames;
        }

        public static Texture2D FloorTexture()
        {
            const int s = 32;
            var tex = NewTexture(s, s);
            tex.wrapMode = TextureWrapMode.Repeat;
            var rng = new System.Random(42);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool mortar = y % 16 == 0 || (x + (y / 16 % 2 == 0 ? 0 : 8)) % 16 == 0;
                int v = rng.Next(-7, 8);
                px[y * s + x] = mortar
                    ? new Color32(44, 40, 54, 255)
                    : new Color32((byte)(78 + v), (byte)(72 + v), (byte)(88 + v), 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Texture2D WallTexture()
        {
            const int s = 16;
            var tex = NewTexture(s, s);
            tex.wrapMode = TextureWrapMode.Repeat;
            var rng = new System.Random(7);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool mortar = y % 8 == 0 || (x + (y / 8 % 2 == 0 ? 0 : 4)) % 8 == 0;
                int v = rng.Next(-6, 7);
                px[y * s + x] = mortar
                    ? new Color32(36, 32, 44, 255)
                    : new Color32((byte)(64 + v), (byte)(60 + v), (byte)(74 + v), 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
