using System;
using System.IO;
using PetThem.Game;
using UnityEditor;
using UnityEngine;

namespace PetThem.Editor
{
    /// <summary>Run only when the project is closed. Renders fixtures, not a live playtest.</summary>
    public static class TravelMapValidation
    {
        public static void Verify()
        {
            try
            {
                var camera = new GameObject("Map fixture camera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 7; camera.aspect = 16f/9;
                camera.transform.position = new Vector3(0,0,-10);
                var map = new GameObject("Map fixture").AddComponent<TravelMap>();
                map.Follow(camera,0);
                int count = map.transform.childCount;
                Vector3 original = map.transform.GetChild(0).GetChild(4).localPosition;
                for (int i = 0; i < 200; i++)
                {
                    camera.transform.position = new Vector3(i*13,-i*4,-10);
                    map.Follow(camera,0);
                    if (map.transform.childCount != count) throw new Exception("Chunk pool grew during travel.");
                }
                camera.transform.position = new Vector3(0,0,-10); map.Follow(camera,0);
                if (map.transform.GetChild(0).GetChild(4).localPosition != original) throw new Exception("Scenery changed on revisit.");
                var actor = new GameObject("Motion fixture");
                var renderer = actor.AddComponent<SpriteRenderer>();
                renderer.sprite = Art.Creature(Look.Player); renderer.sortingOrder = 10;
                var motion = actor.AddComponent<CreatureMotion>(); motion.Build(Art.ColorOf(Look.Player),10);
                motion.Pose(0,0); Vector3 before = actor.transform.GetChild(0).localPosition;
                actor.transform.position = new Vector3(.3f,0,0); motion.Pose(.1f,0,1);
                Vector3 after = actor.transform.GetChild(0).localPosition;
                if (before == after) throw new Exception("Feet did not animate.");
                motion.Pose(.1f,0,1);
                if (actor.transform.GetChild(0).localPosition != after) throw new Exception("Frozen pose moved.");
                string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../experiments/travel-preview"));
                Directory.CreateDirectory(output);
                for (int theme = 0; theme < 3; theme++)
                {
                    map.Follow(camera,theme);
                    Capture(camera,Path.Combine(output,"map-"+theme+".png"));
                }
                Debug.Log("TRAVEL VERIFIED: " + count + " chunks stable over 200 moves; revisit stable; limbs move and freeze; 3 renders.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        private static void Capture(Camera camera,string path)
        {
            var target = new RenderTexture(1280,720,24);
            camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(1280,720,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
