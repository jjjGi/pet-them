using UnityEngine;

namespace PetThem.Game
{
    /// <summary>Independent feet and forepaws layered around the existing illustrated body.</summary>
    public sealed class CreatureMotion : MonoBehaviour
    {
        private readonly Transform[] limbs = new Transform[4];
        private Vector3 previous;
        private float previousTime = -1;
        public void Build(Color tint, int order)
        {
            for (int i = 0; i < limbs.Length; i++)
            {
                var limb = new GameObject(i < 2 ? "Walking foot" : "Swinging paw");
                limbs[i] = limb.transform;
                limbs[i].SetParent(transform, false);
                var r = limb.AddComponent<SpriteRenderer>();
                r.sprite = Art.SoftCircle; r.color = tint; r.sortingOrder = order - 1;
            }
        }
        public void Pose(float time, float phase, float attack = 0)
        {
            if (time == previousTime) return;
            float dt = time - previousTime;
            float speed = previousTime < 0 || dt <= 0 ? 0 : Mathf.Clamp01((transform.position - previous).magnitude / dt / 2);
            previous = transform.position; previousTime = time;
            float step = Mathf.Sin(time * 16 + phase) * speed;
            for (int i = 0; i < limbs.Length; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                bool foot = i < 2;
                float swing = step * side;
                limbs[i].localPosition = new Vector3(side * (foot ? .18f : .3f),
                    (foot ? -.32f : -.08f) + swing * (foot ? .11f : .08f) + (foot ? 0 : attack * .16f), 0);
                limbs[i].localRotation = Quaternion.Euler(0, 0, swing * 28 + side * attack * 35);
                limbs[i].localScale = new Vector3(foot ? .21f : .18f, foot ? .15f : .25f, 1);
            }
        }
    }
}
