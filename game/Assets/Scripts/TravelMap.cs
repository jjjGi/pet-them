using System.Collections.Generic;
using UnityEngine;

namespace PetThem.Game
{
    /// <summary>Camera-sized pool of decorative chunks. No collision or combat rules live here.</summary>
    public sealed class TravelMap : MonoBehaviour
    {
        private const float Size = 12;
        private readonly List<Transform> chunks = new List<Transform>();
        private int lastX = int.MinValue, lastY, columns, rows, lastTheme = -1;
        private readonly Sprite[] scenery = new Sprite[3];
        private static readonly Color[] Ground = { new Color32(49,78,64,255), new Color32(180,139,91,255), new Color32(173,205,213,255) };
        private static readonly Color[] Accent = { new Color32(111,156,87,255), new Color32(113,123,72,255), new Color32(103,155,186,255) };

        private Sprite Scenery(int theme)
        {
            if (scenery[theme] != null) return scenery[theme];
            const int resolution = 128;
            var texture = new Texture2D(resolution,resolution,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[resolution * resolution];
            for (int py = 0; py < resolution; py++)
                for (int px = 0; px < resolution; px++)
                {
                    float x = (px + .5f) / resolution - .5f, y = (py + .5f) / resolution - .5f;
                    Color c = Color.clear;
                    if (x*x/.17f + (y+.37f)*(y+.37f)/.006f < 1) c = new Color(0,0,0,.16f);
                    if (Mathf.Abs(x) < .07f && y > -.36f && y < .12f) c = new Color32(105,80,65,255);
                    bool leaf = false;
                    if (theme == 0)
                        leaf = x*x + (y-.12f)*(y-.12f) < .1f || (x+.2f)*(x+.2f) + (y+.02f)*(y+.02f) < .035f ||
                            (x-.2f)*(x-.2f) + (y+.02f)*(y+.02f) < .035f;
                    else if (theme == 1)
                        leaf = (Mathf.Abs(x)<.11f && y>-.33f && y<.32f) ||
                            (Mathf.Abs(x+.23f)<.065f && y>-.04f && y<.19f) ||
                            (x>-.28f && x<0 && y>-.08f && y<.01f) ||
                            (Mathf.Abs(x-.22f)<.065f && y>-.13f && y<.1f) ||
                            (x>0 && x<.27f && y>-.15f && y<-.06f);
                    else
                        leaf = y>-.24f && y<.4f && Mathf.Abs(x)<(.4f-y)*.53f;
                    if (leaf)
                    {
                        float light = Mathf.Clamp01(.55f - x*.7f + y*.45f);
                        c = Color.Lerp(new Color32(41,87,73,255), Accent[theme], light);
                        if (theme == 2 && (y > .2f || (y > -.06f && y < .04f))) c = new Color32(229,243,241,255);
                        if (theme == 1 && Mathf.Abs(Mathf.Sin(x*85)) < .13f) c *= .85f;
                        c.a = 1;
                    }
                    pixels[py*resolution+px] = c;
                }
            texture.SetPixels(pixels); texture.Apply(false,true);
            return scenery[theme] = Sprite.Create(texture,new Rect(0,0,resolution,resolution),new Vector2(.5f,.5f),resolution);
        }
        private void OnDestroy()
        {
            foreach (Sprite sprite in scenery)
                if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        }

        private SpriteRenderer Part(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = order;
            return renderer;
        }
        public void Follow(Camera camera, int theme)
        {
            theme = Mathf.Clamp(theme, 0, 2);
            int width = Mathf.CeilToInt(camera.orthographicSize * camera.aspect * 2 / Size) + 3;
            int height = Mathf.CeilToInt(camera.orthographicSize * 2 / Size) + 3;
            int x = Mathf.FloorToInt(camera.transform.position.x / Size);
            int y = Mathf.FloorToInt(camera.transform.position.y / Size);
            if (width == columns && height == rows && x == lastX && y == lastY && theme == lastTheme) return;
            while (chunks.Count < width * height)
            {
                var chunk = new GameObject("Travel chunk").transform;
                chunk.SetParent(transform, false);
                Part(chunk, "Ground", Art.Block, -20).transform.localScale = new Vector3(Size + .02f, Size + .02f, 1);
                for (int i = 0; i < 12; i++) Part(chunk, "Scenery", Art.SoftCircle, -18);
                chunks.Add(chunk);
            }
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].gameObject.SetActive(i < width * height);
                if (i >= width * height) continue;
                int cx = x + i % width - width / 2, cy = y + i / width - height / 2;
                Paint(chunks[i], cx, cy, theme);
            }
            columns = width; rows = height; lastX = x; lastY = y; lastTheme = theme;
            camera.backgroundColor = Ground[theme];
        }
        private void Paint(Transform chunk, int x, int y, int theme)
        {
            chunk.position = new Vector3(x * Size, y * Size, 0);
            // A coordinate hash, independent of combat RNG; returning to a place restores its scenery.
            var random = new System.Random(unchecked(x * 73856093 ^ y * 19349663 ^ theme * 83492791));
            chunk.GetChild(0).GetComponent<SpriteRenderer>().color = Ground[theme];
            for (int i = 1; i < chunk.childCount; i++)
            {
                Transform part = chunk.GetChild(i);
                var renderer = part.GetComponent<SpriteRenderer>();
                float px = (float)random.NextDouble() * 10 - 5;
                float py = (float)random.NextDouble() * 10 - 5;
                float scale = .3f + (float)random.NextDouble() * .7f;
                part.localPosition = new Vector3(px, py, 0);
                part.localRotation = Quaternion.Euler(0, 0, (float)random.NextDouble() * 180);
                bool landmark = i % 4 == 0;
                renderer.sprite = landmark ? Scenery(theme) : Art.SoftCircle;
                renderer.color = landmark ? Color.white : Color.Lerp(Ground[theme], Accent[theme], .3f);
                if (landmark) part.localRotation = Quaternion.identity;
                part.localScale = landmark ? Vector3.one * (2 + scale) : theme == 0 ? new Vector3(scale * .3f, scale * 1.6f, 1)
                    : theme == 1 ? new Vector3(scale * 1.8f, scale * .7f, 1)
                    : new Vector3(scale * .65f, scale * 1.3f, 1);
            }
        }
    }
}
