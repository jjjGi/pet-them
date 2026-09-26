using System;
using System.IO;
using System.Reflection;
using PetThem.Combat;
using PetThem.Game;
using UnityEditor;
using UnityEngine;

namespace PetThem.Editor
{
    /// <summary>Render fixtures only: does not enter Play or write player saves.</summary>
    public static class AdventureValidation
    {
        public static void Verify()
        {
            try
            {
                var camera = new GameObject("Adventure camera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 7; camera.aspect = 16f/9;
                camera.transform.position = new Vector3(5,0,-10);
                var map = new GameObject("Map").AddComponent<TravelMap>(); map.Follow(camera,0);
                var game = new GameObject("Adventure render fixture").AddComponent<PrototypeGame>();
                game.enabled = false;
                Set(game,"started",true);
                var actor = new GameObject("Player").AddComponent<SpriteRenderer>();
                actor.sprite = Art.Creature(Look.Player); actor.sortingOrder = 10;
                string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../experiments/adventure-preview"));
                Directory.CreateDirectory(output);
                foreach (WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                {
                    var world = new CombatWorld(new BalanceConfig { endlessWorld = true, adventureEnabled = true },0,true,weapon,PetId.Mochi);
                    Set(game,"world",world);
                    Call(game,"SynchronizeTreasure");
                    Call(game,"ShowAdventureEvent",new CombatEvent { type = "evolution_attack", x = 1, y = 0 });
                    var target = new RenderTexture(1280,720,24);
                    camera.targetTexture = target; camera.Render();
                    RenderTexture.active = target;
                    var texture = new Texture2D(1280,720,TextureFormat.RGBA32,false);
                    texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(output,weapon + ".png"),texture.EncodeToPNG());
                    RenderTexture.active = null; camera.targetTexture = null; target.Release();
                    UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
                    foreach (Transform child in game.transform)
                        if (child.name.StartsWith("Evolution")) child.gameObject.SetActive(false);
                }
                Debug.Log("ADVENTURE RENDER VERIFIED: treasure and three evolution attack fixtures; no gameplay/UI playtest.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        private static void Set(object instance,string name,object value) =>
            typeof(PrototypeGame).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(instance,value);
        private static void Call(object instance,string name,params object[] args) =>
            typeof(PrototypeGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,args);
    }
}
