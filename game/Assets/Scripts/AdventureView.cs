using PetThem.Combat;
using UnityEngine;

namespace PetThem.Game
{
    public sealed partial class PrototypeGame
    {
        private GameObject treasureView;
        private string adventureNotice = "";
        private float adventureNoticeUntil;

        private void SynchronizeTreasure()
        {
            bool visible = started && world.State == RunState.Playing && world.TreasureAvailable;
            if (treasureView == null && visible)
            {
                treasureView = RectSprite("Treasure chest", Vector2.zero, new Vector2(1.2f,.85f), new Color32(168,103,42,255), 6);
                var band = RectSprite("Gold band", Vector2.zero, new Vector2(.22f,.9f), new Color32(255,218,100,255), 7);
                band.transform.SetParent(treasureView.transform, false);
                // RectSprite scales its root, so keep the child's world size predictable.
                band.transform.localScale = new Vector3(.22f / 1.2f, .9f / .85f, 1);
            }
            if (treasureView == null) return;
            treasureView.SetActive(visible);
            if (visible) treasureView.transform.position = new Vector3(world.TreasurePosition.x,world.TreasurePosition.y,0);
        }

        private void DrawAdventureHud(float width)
        {
            if (!world.AdventureEnabled) return;
            GUI.Label(new Rect(28,228,width-56,30),Texts.EvolutionProgress(world),small);
            if (world.TreasureAvailable && world.State == RunState.Playing)
            {
                Vec2 delta = world.TreasurePosition - world.Position;
                string direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? "→" : "←") : (delta.y > 0 ? "↑" : "↓");
                Vector3 point = gameCamera.WorldToViewportPoint(new Vector3(world.TreasurePosition.x,world.TreasurePosition.y,0));
                float x = Mathf.Clamp(point.x * width, 110, width - 110);
                float y = Mathf.Clamp((1-point.y) * 720, 285, 525);
                var box = new Rect(x-100,y-24,200,48);
                Panel(box,new Color(.08f,.12f,.15f,.88f));
                GUI.Label(box,Texts.TreasureGuide(direction,Mathf.CeilToInt(delta.Length)),coaching);
            }
            if (Time.unscaledTime < adventureNoticeUntil)
                GUI.Label(new Rect(width/2-360,280,720,70),adventureNotice,coaching);
        }

        private void ShowAdventureEvent(CombatEvent e)
        {
            if (e.type == "treasure" || e.type == "evolution")
            {
                adventureNotice = e.type == "treasure" ? Texts.TreasureReward : Texts.EvolutionReady(world);
                adventureNoticeUntil = Time.unscaledTime + 3;
                gameAudio.Sound("upgrade");
            }
            if (e.type != "evolution_attack") return;
            Color color = PetColor(world.Pet); color.a = .65f;
            if (world.Weapon == WeaponId.Punch)
            {
                var ring = new GameObject("Evolution shockwave");
                ring.transform.SetParent(transform);
                ring.transform.position = new Vector3(world.Position.x,world.Position.y,0);
                ring.transform.localScale = Vector3.one * (CombatWorld.EvolutionRadius / .4f);
                var renderer = ring.AddComponent<SpriteRenderer>();
                renderer.sprite = Art.Ring; renderer.color = color; renderer.sortingOrder = 9;
                transient.Add(ring); transientEnds.Add(Time.unscaledTime + .3f);
                return;
            }
            Vector2 direction = new Vector2(e.x,e.y);
            EvolutionBeam(direction,color);
            if (world.Weapon == WeaponId.Arrow)
            {
                Vector2 side = new Vector2(-direction.y,direction.x);
                EvolutionBeam((direction+side*.4f).normalized,color);
                EvolutionBeam((direction-side*.4f).normalized,color);
            }
        }

        private void EvolutionBeam(Vector2 direction,Color color)
        {
            Vector2 origin = new Vector2(world.Position.x,world.Position.y);
            var ray = RectSprite("Evolution ray",origin+direction*(CombatWorld.EvolutionRange*.5f),
                new Vector2(CombatWorld.EvolutionRange,world.Weapon == WeaponId.Laser ? 1.3f : .44f),color,9);
            ray.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            transient.Add(ray); transientEnds.Add(Time.unscaledTime + .22f);
        }
    }
}
