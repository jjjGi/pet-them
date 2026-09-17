using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetThem.Game
{
    /// <summary>Who a sprite is for. Each one gets its own silhouette.</summary>
    public enum Look { Player, Mochi, Bori, Coco, Grunt, Runner, Brute, Boss }

    /// <summary>
    /// Loads the clay art set. Procedural creatures remain a fallback for a missing asset.
    /// </summary>
    /// <remarks>
    /// Imported RGBA illustrations supply faces and silhouettes in one renderer per creature.
    /// They are 2D sprites with a sculpted appearance, not rigged 3D meshes. Imported textures
    /// belong to Unity; only the runtime Sprite wrappers and procedural fallback textures are destroyed.
    /// </remarks>
    public static class Art
    {
        private const int Size = 128;
        private const float Outline = 3.2f;

        private static readonly Color Ink = new Color32(19, 32, 44, 255);
        private static readonly Dictionary<Look, Sprite> cache = new Dictionary<Look, Sprite>();
        private static readonly HashSet<Sprite> importedSprites = new HashSet<Sprite>();
        private static Sprite softCircle, block, ring, arena;

        public static Color ColorOf(Look look)
        {
            switch (look)
            {
                case Look.Player: return new Color32(238, 245, 221, 255);
                case Look.Mochi: return new Color32(124, 239, 192, 255);
                case Look.Bori: return new Color32(138, 200, 255, 255);
                case Look.Coco: return new Color32(255, 214, 150, 255);
                case Look.Runner: return new Color32(255, 203, 115, 255);
                case Look.Brute: return new Color32(176, 124, 224, 255);
                case Look.Boss: return new Color32(255, 92, 140, 255);
                default: return new Color32(255, 119, 110, 255);
            }
        }

        /// <summary>A whole creature baked into one sprite: body, face and all.</summary>
        public static Sprite Creature(Look look)
        {
            if (cache.TryGetValue(look, out Sprite existing)) return existing;
            Sprite made = LoadArtwork(look.ToString());
            if (made == null)
            {
                Debug.LogWarning("Missing clay artwork: " + look + ". Using procedural fallback.");
                made = Sprite.Create(Bake(look), new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
            }
            made.name = look + " sprite";
            cache[look] = made;
            return made;
        }

        public static Sprite Arena => arena != null ? arena : arena = LoadArtwork("Arena");

        private static Sprite LoadArtwork(string name)
        {
            Texture2D texture = Resources.Load<Texture2D>("Art/Clay/" + name);
            if (texture == null) return null;
            // Normalize after import resizing: every creature retains a one-unit canvas.
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), Mathf.Max(texture.width, texture.height), 0, SpriteMeshType.FullRect);
            sprite.name = name + " clay artwork";
            importedSprites.Add(sprite);
            return sprite;
        }

        /// <summary>A soft dot, used for effects and the on-screen thumbstick.</summary>
        public static Sprite SoftCircle => softCircle ??= Build("Soft circle", (x, y) =>
        {
            float distance = Distance(x, y, 0, 0);
            return new Color(1, 1, 1, Mathf.Clamp01((0.5f - distance) * Size * 0.5f));
        });

        /// <summary>A plain rectangle for the arena, grid lines, bars and the beam.</summary>
        public static Sprite Block => block ??= Build("Block", (x, y) => Color.white);

        /// <summary>A hollow ring for the punch and the heal pulse.</summary>
        public static Sprite Ring => ring ??= Build("Ring", (x, y) =>
        {
            float distance = Distance(x, y, 0, 0);
            float edge = Mathf.Abs(distance - 0.40f);
            return new Color(1, 1, 1, Mathf.Clamp01((0.075f - edge) * Size * 0.4f));
        });

        public static void Release()
        {
            foreach (Sprite sprite in cache.Values) Destroy(sprite);
            cache.Clear();
            Destroy(softCircle); softCircle = null;
            Destroy(block); block = null;
            Destroy(ring); ring = null;
            Destroy(arena); arena = null;
            importedSprites.Clear();
        }

        private static void Destroy(Sprite sprite)
        {
            if (sprite == null) return;
            // Resources textures are imported assets, shared with menus and previews.
            if (!importedSprites.Contains(sprite) && sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }

        private static Sprite Build(string name, Func<float, float, Color> shade)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = shade((x + .5f) / Size - .5f, (y + .5f) / Size - .5f);
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
            sprite.name = name;
            return sprite;
        }

        private static Texture2D Bake(Look look)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            texture.SetPixels(Pixels(look, Size));
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Paints one creature. The body comes from a signed distance: negative inside, zero on the
        /// edge. That gives a clean outline and a soft edge for free, whatever the shape is.
        /// </summary>
        /// <remarks>
        /// Returns raw pixels rather than a texture so the same code can be rendered to a file and
        /// looked at outside the editor. What is previewed is what ships.
        /// </remarks>
        public static Color[] Pixels(Look look, int size)
        {
            Color body = ColorOf(look);
            Color top = Color.Lerp(body, Color.white, .22f);
            Color bottom = Color.Lerp(body, Ink, .18f);
            float outline = Outline * size / Size;
            var pixels = new Color[size * size];

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + .5f) / size - .5f;
                    float y = (py + .5f) / size - .5f;
                    float pixelsFromEdge = -BodyDistance(look, x, y) * size;

                    Color pixel;
                    if (pixelsFromEdge <= 0) pixel = new Color(0, 0, 0, 0);
                    else if (pixelsFromEdge <= outline)
                        pixel = new Color(Ink.r, Ink.g, Ink.b, Mathf.Clamp01(pixelsFromEdge));
                    else
                    {
                        // Lighter at the top so a flat shape still reads as round.
                        Color fill = Color.Lerp(bottom, top, Mathf.Clamp01(y * 1.6f + .5f));
                        pixel = Face(look, x, y, fill);
                        pixel.a = Mathf.Clamp01(pixelsFromEdge - outline + 1);
                    }
                    pixels[py * size + px] = pixel;
                }
            }
            return pixels;
        }

        /// <summary>Negative inside the body, positive outside, in sprite units where 0.5 is the edge.</summary>
        private static float BodyDistance(Look look, float x, float y)
        {
            switch (look)
            {
                case Look.Runner:
                    // Tapers to a point, so it reads as quick even standing still.
                    float taper = 1 - Mathf.Clamp01(y * 2.2f) * .62f;
                    return Distance(x / taper, y, 0, -.04f) - .33f;

                case Look.Brute:
                    // Wide and low, with shoulders. Heavy on its feet.
                    return RoundedBox(x, y - .03f, .40f, .30f, .16f);

                case Look.Boss:
                    // Big, with a spiked crown so it is unmistakable in a crowd.
                    float spikes = Mathf.Cos(Mathf.Atan2(y, x) * 7) * .052f * Mathf.Clamp01(y * 3.2f);
                    return Distance(x, y, 0, -.02f) - (.44f + spikes);

                case Look.Player:
                    return RoundedBox(x, y, .30f, .34f, .22f);

                case Look.Mochi:
                case Look.Bori:
                case Look.Coco:
                    // Ears on top, blended into the head rather than stuck on.
                    float head = Distance(x, y - .04f, 0, 0) - .34f;
                    float left = Distance(x, y, -.24f, .30f) - .15f;
                    float right = Distance(x, y, .24f, .30f) - .15f;
                    return Smooth(Smooth(head, left, .07f), right, .07f);

                default:
                    return Distance(x, y, 0, 0) - .40f;
            }
        }

        /// <summary>Eyes, and a mouth for the ones that need to look like they mean it.</summary>
        private static Color Face(Look look, float x, float y, Color fill)
        {
            float eyeY = look == Look.Brute ? .07f : .09f;
            float eyeX = look == Look.Brute ? .15f : .12f;
            float eyeSize = look == Look.Boss ? .055f : .042f;

            float eyes = Mathf.Min(
                Distance(x, y, -eyeX, eyeY) - eyeSize,
                Distance(x, y, eyeX, eyeY) - eyeSize);
            if (eyes < 0) return Blend(fill, Ink, Mathf.Clamp01(-eyes * Size));

            bool angry = look == Look.Boss || look == Look.Brute || look == Look.Runner || look == Look.Grunt;

            // Both mouths are an arc cut from a ring. A frown keeps the top of a ring centred
            // below the mouth; a smile keeps the bottom of one centred above it.
            float centre = angry ? -.34f : -.01f;
            float band = Mathf.Abs(Distance(x, y, 0, centre) - .155f) - .021f;
            bool onArc = angry ? y > centre + .05f : y < centre - .05f;
            if (band < 0 && onArc) return Blend(fill, Ink, Mathf.Clamp01(-band * Size));
            return fill;
        }

        private static Color Blend(Color under, Color over, float amount)
        {
            Color mixed = Color.Lerp(under, over, Mathf.Clamp01(amount));
            mixed.a = 1;
            return mixed;
        }

        private static float Distance(float x, float y, float cx, float cy)
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float RoundedBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float dx = Mathf.Abs(x) - (halfWidth - radius);
            float dy = Mathf.Abs(y) - (halfHeight - radius);
            float outside = Distance(Mathf.Max(dx, 0), Mathf.Max(dy, 0), 0, 0);
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
        }

        /// <summary>Merges two shapes without leaving a crease where they meet.</summary>
        private static float Smooth(float a, float b, float softness)
        {
            float h = Mathf.Clamp01(.5f + .5f * (b - a) / softness);
            return Mathf.Lerp(b, a, h) - softness * h * (1 - h);
        }
    }
}
