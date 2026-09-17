using System;
using System.IO;
using PetThem.Game;
using UnityEditor;
using UnityEngine;

namespace PetThem.Editor
{
    /// <summary>Batch import/render verification. Run in an isolated copy, not an open playtest project.</summary>
    public static class ClayArtValidation
    {
        public static void Verify()
        {
            try
            {
                string output = Path.Combine(Application.dataPath, "..", "ArtVerification");
                Directory.CreateDirectory(output);
                foreach (Look look in Enum.GetValues(typeof(Look)))
                {
                    Sprite sprite = Art.Creature(look);
                    string path = AssetDatabase.GetAssetPath(sprite.texture);
                    if (!path.StartsWith("Assets/Resources/Art/Clay/", StringComparison.Ordinal))
                        throw new Exception("Fallback used for " + look);
                    if (sprite.texture.width > 512 || sprite.texture.height > 512)
                        throw new Exception("Oversized imported creature: " + look);
                    if (Math.Abs(sprite.bounds.size.x - 1) > .01f || Math.Abs(sprite.bounds.size.y - 1) > .01f)
                        throw new Exception("Unexpected sprite bounds: " + look);
                    Debug.Log("ART VERIFIED " + look + " " + sprite.texture.width + "x" + sprite.texture.height);
                }
                if (Art.Arena == null) throw new Exception("Missing arena.");
                var cameraObject = new GameObject("Art verification camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 11;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(40, 65, 57, 255);
                var field = new GameObject("Arena");
                var background = field.AddComponent<SpriteRenderer>();
                background.sprite = Art.Arena; background.sortingOrder = -10;
                field.transform.localScale = new Vector3(28 / .88f / Art.Arena.bounds.size.x, 16 / .88f / Art.Arena.bounds.size.y, 1);
                Put(Look.Player, 0, 0, 1, 10);
                Put(Look.Mochi, 1.1f, .3f, .62f, 11);
                Put(Look.Bori, -1.1f, .3f, .62f, 11);
                Put(Look.Coco, 0, -1, .62f, 11);
                Put(Look.Grunt, -5, 2, 1.125f, 5);
                Put(Look.Grunt, -3, -2, 1.125f, 5);
                Put(Look.Runner, 4, -2, .775f, 5);
                Put(Look.Brute, -7, -3, 1.55f, 5);
                Put(Look.Boss, 8, 3, 4.75f, 7);
                Capture(camera, Path.Combine(output, "arena-preview.png"), 1400, 880);
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                camera.orthographicSize = 2.4f;
                camera.backgroundColor = new Color32(34, 51, 55, 255);
                int index = 0;
                foreach (Look look in Enum.GetValues(typeof(Look)))
                {
                    Put(look, (index % 4 - 1.5f) * 2.1f, index < 4 ? 1.1f : -1.1f, 2, 5);
                    index++;
                }
                Capture(camera, Path.Combine(output, "character-lineup.png"), 1600, 880);
                Debug.Log("ART VERIFICATION PASSED: " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Put(Look look, float x, float y, float size, int order)
        {
            var go = new GameObject(look.ToString());
            go.transform.position = new Vector3(x, y, 0);
            go.transform.localScale = Vector3.one * size;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Art.Creature(look); renderer.sortingOrder = order;
        }

        private static void Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.ReadPixels(new Rect(0, 0, width, height), 0, 0); output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(output);
        }
    }
}
