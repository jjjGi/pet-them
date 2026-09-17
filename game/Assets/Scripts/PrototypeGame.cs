using System;
using System.Collections.Generic;
using PetThem.Combat;
using UnityEngine;

namespace PetThem.Game
{
    public sealed class PrototypeGame : MonoBehaviour
    {
        private readonly Color ink = new Color32(19, 32, 44, 255);
        private readonly Color mint = new Color32(124, 239, 192, 255);
        private readonly Color paper = new Color32(238, 245, 221, 255);
        private readonly Color coral = new Color32(255, 119, 110, 255);
        private readonly Dictionary<int, GameObject> views = new Dictionary<int, GameObject>();
        private readonly List<GameObject> transient = new List<GameObject>();
        private readonly List<float> transientEnds = new List<float>();
        private readonly List<int> staleIds = new List<int>();
        private CombatWorld world;
        private BalanceConfig config;
        private RunRecorder recorder;
        private Camera gameCamera;
        private Sprite circle, square;
        private Transform player, pet;
        private readonly Dictionary<int, GameObject> arrowViews = new Dictionary<int, GameObject>();
        private readonly List<int> staleArrows = new List<int>();
        private GameObject beam;
        private WeaponId weapon = WeaponId.Punch;
        private PetId petChoice = PetId.Mochi;
        private PlayerProfile profile;
        private bool shopOpen;
        private int lastRunCoins;
        private Vec2 move, aim, queuedAim;
        private bool queuedPunch, holdAttack, paused, started;
        private int moveFinger = -1, attackFinger = -1;
        private Vector2 moveAnchor, attackAnchor, movePoint;
        private float accumulator, hurtFlash;
        private string recordingPath = "", recordingError = "";
        private GUIStyle title, heading, body, small, button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<PrototypeGame>() == null)
                new GameObject("PET THEM! Runtime").AddComponent<PrototypeGame>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.simulateMouseWithTouches = false;
            gameCamera = Camera.main;
            if (gameCamera == null) gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
            gameCamera.tag = "MainCamera";
            gameCamera.orthographic = true;
            gameCamera.transform.position = new Vector3(0, 0, -10);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = ink;
            circle = MakeCircle();
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f,.5f), 1);
            var asset = Resources.Load<TextAsset>("balance-default");
            if (asset == null) throw new InvalidOperationException("Missing balance-default.json.");
            config = JsonUtility.FromJson<BalanceConfig>(asset.text);
            config.Validate();
            profile = ProfileStore.Load();
            if (!profile.IsUnlocked(petChoice)) petChoice = PlayerProfile.StarterPet;
            CreateArena();
            player = CreateCreature("You", paper, 0.9f, 10, true).transform;
            pet = CreateCreature("Mochi", mint, 0.55f, 11, true).transform;
            beam = RectSprite("Laser beam", Vector2.zero, Vector2.one, coral, 9);
            beam.SetActive(false);
            world = new CombatWorld(config, 42, true, weapon, petChoice);
        }

        private void StartRun()
        {
            EndRecording(true);
            foreach (var view in views.Values) Destroy(view);
            views.Clear();
            foreach (var view in arrowViews.Values) Destroy(view);
            arrowViews.Clear();
            foreach (var view in transient) Destroy(view);
            transient.Clear(); transientEnds.Clear();
            beam.SetActive(false);
            // The pet is rebuilt because its colour is part of telling the three apart.
            if (pet != null) Destroy(pet.gameObject);
            pet = CreateCreature(PetName(petChoice), PetColor(petChoice), 0.55f, 11, true).transform;
            world = new CombatWorld(config, unchecked(Environment.TickCount), true, weapon, petChoice);
            started = true; paused = false; shopOpen = false; accumulator = 0; hurtFlash = 0;
            ClearInput();
            recordingError = "";
            try { recorder = new RunRecorder(world); recordingPath = recorder.FilePath; }
            catch (Exception ex) { recordingError = "Recording unavailable: " + ex.Message; Debug.LogWarning(recordingError); }
        }

        private void Update()
        {
            gameCamera.orthographicSize = Mathf.Max(config.arenaHalfHeight + 2.8f,
                (config.arenaHalfWidth + 1) / gameCamera.aspect);
            if (Input.GetKeyDown(KeyCode.Escape) && started && world.State == RunState.Playing) TogglePause();
            if (started && !paused && world.State == RunState.Playing && world.HasUpgradeChoice)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgrade(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgrade(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgrade(2);
            }
            else if (started && !paused && world.State == RunState.Playing)
            {
                ReadInput();
                accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                while (accumulator >= CombatWorld.StepSeconds)
                {
                    world.Step(new PlayerInput(move, queuedPunch ? queuedAim : aim, queuedPunch, holdAttack));
                    queuedPunch = false;
                    RecordEvents();
                    foreach (CombatEvent e in world.Events) ShowEvent(e);
                    accumulator -= CombatWorld.StepSeconds;
                    if (world.State != RunState.Playing) { EndRecording(false); ClearInput(); BankRun(); break; }
                    if (world.HasUpgradeChoice) { ClearInput(); FlushRecording(); break; }
                }
            }
            player.position = new Vector3(world.Position.x, world.Position.y, 0);
            float orbit = world.Time * 2;
            pet.position = player.position + new Vector3(Mathf.Cos(orbit) * 1.1f, Mathf.Sin(orbit) * 0.75f, 0);
            player.GetComponent<SpriteRenderer>().color = Time.unscaledTime < hurtFlash ? coral : paper;
            SynchronizeEnemies();
            SynchronizeArrows();
            DrawBeam();
            for (int i = transient.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= transientEnds[i])
                { Destroy(transient[i]); transient.RemoveAt(i); transientEnds.RemoveAt(i); }
        }

        private void ReadInput()
        {
            move = new Vec2();
            aim = new Vec2();
            float radius = Mathf.Max(40, Screen.height * 0.11f);
            if (Input.touchCount > 0)
            {
                bool moveSeen = false, attackSeen = false;
                foreach (Touch touch in Input.touches)
                {
                    Vector2 p = touch.position;
                    if (touch.phase == TouchPhase.Began && p.y < Screen.height * 0.84f)
                    {
                        if (p.x < Screen.width * .45f && moveFinger < 0)
                        { moveFinger = touch.fingerId; moveAnchor = p; }
                        else if (p.x > Screen.width * .55f && attackFinger < 0)
                        { attackFinger = touch.fingerId; attackAnchor = p; }
                    }
                    bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                    if (touch.fingerId == moveFinger)
                    {
                        moveSeen = true; movePoint = p;
                        Vector2 delta = Vector2.ClampMagnitude((p - moveAnchor) / radius, 1);
                        move = ended ? new Vec2() : new Vec2(delta.x, delta.y);
                        if (ended) moveFinger = -1;
                    }
                    if (touch.fingerId == attackFinger)
                    {
                        attackSeen = true;
                        Vector2 delta = p - attackAnchor;
                        aim = delta.magnitude > radius * .15f ? new Vec2(delta.x, delta.y).Normalized : new Vec2();
                        holdAttack = !ended;
                        if (ended)
                        {
                            // The arrow leaves on release, so queueing a shot here would fire it twice.
                            queuedPunch = touch.phase == TouchPhase.Ended && weapon == WeaponId.Punch;
                            queuedAim = aim; attackFinger = -1;
                        }
                    }
                }
                if (!moveSeen) moveFinger = -1;
                if (!attackSeen) { attackFinger = -1; holdAttack = false; }
                return;
            }
            moveFinger = attackFinger = -1;
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            move = new Vec2(x, y);
            holdAttack = false;
            if (Input.mousePosition.y < Screen.height * .84f)
            {
                Vector3 point = gameCamera.ScreenToWorldPoint(Input.mousePosition);
                aim = new Vec2(point.x - world.Position.x, point.y - world.Position.y);
                holdAttack = Input.GetMouseButton(0);
                // The punch fires on press. The arrow fires when the button comes back up, so that
                // dragging to aim first is possible; the laser burns for as long as it is held.
                if (Input.GetMouseButtonDown(0) && weapon == WeaponId.Punch) { queuedPunch = true; queuedAim = aim; }
            }
            if (Input.GetKey(KeyCode.Space)) holdAttack = true;
            if (Input.GetKeyDown(KeyCode.Space) && weapon != WeaponId.Laser)
            { aim = new Vec2(); queuedAim = aim; queuedPunch = weapon == WeaponId.Punch; }
        }

        private void ClearInput()
        {
            moveFinger = attackFinger = -1;
            move = aim = queuedAim = new Vec2(); queuedPunch = holdAttack = false; accumulator = 0;
            world?.CancelHeldAttack();
            if (beam != null) beam.SetActive(false);
        }
        private void SelectUpgrade(int index)
        {
            if (paused || index < 0 || index >= world.UpgradeChoices.Count) return;
            if (!world.ChooseUpgrade(world.UpgradeChoices[index].Id)) return;
            RecordEvents(); FlushRecording(); ClearInput();
        }
        /// <summary>
        /// Pays out a finished run. Abandoning pays nothing, so restarting is not a way to farm
        /// the first easy seconds over and over.
        /// </summary>
        private void BankRun()
        {
            if (world.State != RunState.Won && world.State != RunState.Lost) return;
            lastRunCoins = world.Coins;
            profile.AddRunReward(lastRunCoins);
            ProfileStore.Save(profile);
        }

        private void Buy(PetId target)
        {
            if (!profile.Unlock(target, config)) return;
            ProfileStore.Save(profile);
            petChoice = target;
        }

        private void TogglePause() { paused = !paused; ClearInput(); FlushRecording(); }
        private void OnApplicationFocus(bool focus) { if (!focus && started) { paused = true; ClearInput(); FlushRecording(); } }
        private void OnApplicationPause(bool pause) { if (pause && started) { paused = true; ClearInput(); FlushRecording(); } }
        private void RecordEvents()
        {
            try { recorder?.Capture(world); }
            catch (Exception ex) { DisableRecording(ex); }
        }
        private void FlushRecording()
        {
            try { recorder?.Flush(); }
            catch (Exception ex) { DisableRecording(ex); }
        }
        private void DisableRecording(Exception ex)
        {
            recordingError = "Recording stopped: " + ex.Message;
            Debug.LogWarning(recordingError);
            try { recorder?.Dispose(); } catch (Exception) { }
            recorder = null;
        }
        private void EndRecording(bool abandon)
        {
            if (world == null) return;
            if (abandon && world.State == RunState.Playing) { world.Abandon(); RecordEvents(); }
            try { recorder?.Dispose(); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            recorder = null;
        }
        private void OnDestroy()
        {
            EndRecording(true);
            if (circle != null) { Destroy(circle.texture); Destroy(circle); }
            if (square != null) Destroy(square);
        }

        private void CreateArena()
        {
            RectSprite("Arena", Vector2.zero, new Vector2(config.arenaHalfWidth * 2, config.arenaHalfHeight * 2),
                new Color32(34, 57, 65, 255), -10);
            for (int x = -(int)config.arenaHalfWidth; x <= config.arenaHalfWidth; x += 2)
                RectSprite("Grid", new Vector2(x, 0), new Vector2(.025f, config.arenaHalfHeight * 2),
                    new Color32(45, 73, 77, 255), -9);
            for (int y = -(int)config.arenaHalfHeight; y <= config.arenaHalfHeight; y += 2)
                RectSprite("Grid", new Vector2(0, y), new Vector2(config.arenaHalfWidth * 2, .025f),
                    new Color32(45, 73, 77, 255), -9);
        }
        private GameObject RectSprite(string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(pos.x,pos.y,0);
            go.transform.localScale = new Vector3(size.x,size.y,1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square; renderer.color = color; renderer.sortingOrder = order;
            return go;
        }
        private GameObject CreateCreature(string name, Color color, float size, int order, bool ears)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = circle; renderer.color = color; renderer.sortingOrder = order;
            go.transform.localScale = Vector3.one * size;
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new GameObject("Eye");
                eye.transform.SetParent(go.transform, false);
                eye.transform.localPosition = new Vector3(side * .18f,.10f,0);
                eye.transform.localScale = new Vector3(.09f,.14f,1);
                var er = eye.AddComponent<SpriteRenderer>(); er.sprite = circle; er.color = ink; er.sortingOrder = order + 1;
                if (ears)
                {
                    var ear = new GameObject("Ear");
                    ear.transform.SetParent(go.transform, false);
                    ear.transform.localPosition = new Vector3(side * .30f,.36f,0);
                    ear.transform.localScale = new Vector3(.32f,.43f,1);
                    var r = ear.AddComponent<SpriteRenderer>(); r.sprite = circle; r.color = color; r.sortingOrder = order - 1;
                }
            }
            return go;
        }
        private void SynchronizeEnemies()
        {
            staleIds.Clear();
            foreach (int id in views.Keys)
            {
                bool found = false;
                foreach (Enemy enemy in world.Enemies) if (enemy.Id == id) { found = true; break; }
                if (!found) staleIds.Add(id);
            }
            foreach (int id in staleIds) { Destroy(views[id]); views.Remove(id); }
            foreach (Enemy enemy in world.Enemies)
            {
                if (!views.TryGetValue(enemy.Id, out GameObject view))
                {
                    view = CreateCreature(enemy.Kind.ToString(), EnemyColor(enemy.Kind),
                        enemy.Radius * 2, enemy.Kind == EnemyKind.Boss ? 7 : 5, enemy.Kind == EnemyKind.Boss);
                    views.Add(enemy.Id, view);
                }
                view.transform.position = new Vector3(enemy.Position.x,enemy.Position.y,0);
                // A chilled enemy is tinted towards Bori's blue so the slow is visible.
                view.GetComponent<SpriteRenderer>().color = enemy.Slowed
                    ? Color.Lerp(EnemyColor(enemy.Kind), PetColor(PetId.Bori), .55f)
                    : EnemyColor(enemy.Kind);
            }
        }
        private Color PetColor(PetId id) =>
            id == PetId.Bori ? new Color32(138, 200, 255, 255) :
            id == PetId.Coco ? new Color32(255, 214, 150, 255) : mint;

        private static string PetName(PetId id) => id.ToString().ToUpperInvariant();

        private static string PetRole(PetId id) =>
            id == PetId.Bori ? "chills and shoves what it bites" :
            id == PetId.Coco ? "heals you and softens every hit" :
            "hits hardest, no tricks";

        private static Color EnemyColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return new Color32(255, 203, 115, 255);
                case EnemyKind.Brute: return new Color32(176, 124, 224, 255);
                case EnemyKind.Boss: return new Color32(255, 92, 140, 255);
                default: return new Color32(255, 119, 110, 255);
            }
        }

        private void SynchronizeArrows()
        {
            staleArrows.Clear();
            foreach (int id in arrowViews.Keys)
            {
                bool found = false;
                foreach (Projectile arrow in world.Projectiles) if (arrow.Id == id) { found = true; break; }
                if (!found) staleArrows.Add(id);
            }
            foreach (int id in staleArrows) { Destroy(arrowViews[id]); arrowViews.Remove(id); }
            foreach (Projectile arrow in world.Projectiles)
            {
                if (!arrowViews.TryGetValue(arrow.Id, out GameObject view))
                {
                    view = RectSprite("Arrow", Vector2.zero, new Vector2(.9f, .16f), paper, 9);
                    arrowViews.Add(arrow.Id, view);
                }
                view.transform.position = new Vector3(arrow.Position.x, arrow.Position.y, 0);
                view.transform.rotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(arrow.Velocity.y, arrow.Velocity.x) * Mathf.Rad2Deg);
            }
        }

        private void DrawBeam()
        {
            bool visible = started && !paused && world.LaserActive;
            beam.SetActive(visible);
            if (!visible) return;
            var from = new Vector2(world.Position.x, world.Position.y);
            var to = new Vector2(world.LaserEnd.x, world.LaserEnd.y);
            Vector2 delta = to - from;
            beam.transform.position = new Vector3((from.x + to.x) / 2, (from.y + to.y) / 2, 0);
            beam.transform.localScale = new Vector3(delta.magnitude, config.laserWidth, 1);
            beam.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            beam.GetComponent<SpriteRenderer>().color =
                new Color(1, .55f, .35f, Mathf.Lerp(.55f, .95f, world.Heat / 100f));
        }

        private void ShowEvent(CombatEvent e)
        {
            if (e.type == "hurt") hurtFlash = Time.unscaledTime + .15f;
            if (e.type == "heal")
            {
                var ring = new GameObject("Heal");
                ring.transform.SetParent(transform);
                ring.transform.position = new Vector3(e.x, e.y, 0);
                ring.transform.localScale = Vector3.one * 2.2f;
                var hr = ring.AddComponent<SpriteRenderer>();
                hr.sprite = circle;
                hr.color = new Color(PetColor(PetId.Coco).r, PetColor(PetId.Coco).g, PetColor(PetId.Coco).b, .35f);
                hr.sortingOrder = 9;
                transient.Add(ring); transientEnds.Add(Time.unscaledTime + .35f);
                return;
            }
            if (e.type != "attack") return;
            // The arrow and the beam are drawn from world state every frame, not as a one-off flash.
            if (e.source == "arrow" || e.source == "laser") return;
            GameObject fx;
            if (e.source == "punch")
            {
                fx = new GameObject("Punch");
                fx.transform.SetParent(transform);
                fx.transform.position = player.position + new Vector3(e.x,e.y,0) * world.PunchRange * .6f;
                fx.transform.localScale = Vector3.one * world.PunchRange;
                var r = fx.AddComponent<SpriteRenderer>(); r.sprite = circle;
                r.color = new Color(mint.r,mint.g,mint.b,.3f); r.sortingOrder = 8;
            }
            else
            {
                Vector2 from = new Vector2(pet.position.x,pet.position.y);
                Vector2 to = new Vector2(e.x,e.y), delta = to - from;
                fx = RectSprite("Pet beam", (from + to) / 2, new Vector2(delta.magnitude,.07f), mint, 9);
                fx.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x) * Mathf.Rad2Deg);
            }
            transient.Add(fx); transientEnds.Add(Time.unscaledTime + .12f);
        }
        private static Sprite MakeCircle()
        {
            const int size = 64;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(size/2f,size/2f));
                pixels[y * size+x] = new Color(1,1,1,Mathf.Clamp01(size/2f-distance));
            }
            texture.SetPixels(pixels); texture.Apply(); texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
        }

        private void InitStyles()
        {
            if (body != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 15 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 23, fontStyle = FontStyle.Bold };
            title.normal.textColor = heading.normal.textColor = body.normal.textColor = paper;
            small.normal.textColor = mint;
        }
        private void Panel(Rect rect, Color color)
        { Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }
        private void OnGUI()
        {
            InitStyles();
            float scale = Screen.height / 720f;
            float width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            Panel(new Rect(0,0,width,76),ink);
            GUI.Label(new Rect(28,15,260,45),"PET THEM!",heading);
            GUI.Label(new Rect(width/2-100,13,240,45),FormatTime(world.Time) + " / 03:00",heading);
            GUI.Label(new Rect(28,87,320,32),PetName(petChoice) + "  /  " + WeaponName(weapon),small);
            if (started)
            {
                Panel(new Rect(28,48,220,9),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,48,220 * world.Health / world.MaxHealth,9),mint);
                GUI.Label(new Rect(28,119,370,30),"LEVEL " + world.Level + (world.Level >= CombatWorld.MaxLevel ? "  /  MAX" :
                    "  /  " + world.Experience + " / " + world.ExperienceToNextLevel + " XP"),small);
                Panel(new Rect(28,153,220,7),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,153,220 * (world.Level >= CombatWorld.MaxLevel ? 1 :
                    Mathf.Clamp01((float)world.Experience / world.ExperienceToNextLevel)),7),mint);
                GUI.Label(new Rect(width-380,22,240,35),"WAVE " + world.Wave + "   /   " + world.Kills + " KOs",body);
                if (world.State == RunState.Playing && GUI.Button(new Rect(width-110,15,88,43),paused ? "PLAY" : "II",button)) TogglePause();
                if (world.Boss != null)
                {
                    GUI.Label(new Rect(width/2-220,600,440,30),"BIG ONE  /  " +
                        Mathf.CeilToInt(world.Boss.Health) + " HP",small);
                    Panel(new Rect(width/2-220,632,440,14),new Color(.3f,.35f,.38f));
                    Panel(new Rect(width/2-220,632,440 * world.Boss.Health / world.Boss.MaxHealth,14),
                        EnemyColor(EnemyKind.Boss));
                }
                else if (!world.BossDefeated && world.State == RunState.Playing)
                    GUI.Label(new Rect(width/2-140,600,300,30),
                        "BIG ONE IN " + Mathf.CeilToInt(world.SecondsToBoss) + "s",small);
                if (world.GuardReduction > 0)
                    GUI.Label(new Rect(28,188,360,30),
                        "COCO  /  -" + Mathf.RoundToInt(world.GuardReduction * 100) + "% contact   /   heal in " +
                        Mathf.CeilToInt(world.SecondsToPetHeal) + "s",small);
                GUI.Label(new Rect(28,666,400,30),"MOVE  /  WASD or left thumb",small);
                GUI.Label(new Rect(width-460,666,440,30),WeaponName(weapon) + "  /  " + WeaponHint(weapon),small);
                if (weapon == WeaponId.Laser)
                {
                    GUI.Label(new Rect(width-460,188,250,30),world.Overheated ? "OVERHEATED" : "HEAT",small);
                    Panel(new Rect(width-460,222,220,9),new Color(.3f,.35f,.38f));
                    Panel(new Rect(width-460,222,220 * world.Heat / 100f,9),
                        world.Overheated ? coral : new Color(1,.68f,.36f));
                }
                if (moveFinger >= 0)
                {
                    Vector2 anchor = new Vector2(moveAnchor.x / scale,(Screen.height-moveAnchor.y)/scale);
                    Vector2 stick = new Vector2(movePoint.x / scale,(Screen.height-movePoint.y)/scale);
                    GUI.color = new Color(mint.r,mint.g,mint.b,.2f);
                    GUI.DrawTexture(new Rect(anchor.x-64,anchor.y-64,128,128),circle.texture);
                    GUI.color = mint;
                    Vector2 delta = Vector2.ClampMagnitude(stick-anchor,64);
                    GUI.DrawTexture(new Rect(anchor.x+delta.x-22,anchor.y+delta.y-22,44,44),circle.texture);
                    GUI.color = Color.white;
                }
            }
            if (started && !paused && world.State == RunState.Playing && world.HasUpgradeChoice)
                DrawUpgradeChoices(width);
            bool onMenu = !started || paused || world.State != RunState.Playing;
            if (onMenu && shopOpen) { DrawShop(width); return; }
            if (onMenu)
            {
                Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.92f));
                float left = width/2-340;
                GUI.Label(new Rect(left,158,700,90),!started ? "PET THEM!" :
                    paused && world.State == RunState.Playing ? "TAKE A BREATHER." :
                    world.BossDefeated ? "BIG ONE DOWN!" :
                    world.State == RunState.Won ? "NICE PETTING." : "ONE MORE PAT?",title);
                GUI.Label(new Rect(left,265,680,90),!started ?
                    "Survive 3 minutes. A big one shows up at 2:00.\nPick a weapon and a buddy, then one of 3 upgrades per level." :
                    "Time " + FormatTime(world.Time) + "   /   " + world.Kills + " KOs   /   Wave " + world.Wave +
                    "\n+" + lastRunCoins + " COINS" + (world.BossDefeated ? " (big one bonus)" : "") +
                    "   /   " + profile.coins + " saved",heading);
                bool picking = !started || world.State != RunState.Playing;
                GUI.Label(new Rect(left,370,680,90),!started ?
                    "Move with your left thumb. The right side attacks.\nMochi attacks automatically.\nDesktop: WASD + mouse. SPACE auto-targets." :
                    "Mint = Mochi. Coral = chaser. Gold = runner. Purple = brute, slow but heavy.\nPink = the big one. Take it down to end the run early.",body);
                if (picking) DrawWeaponPicker(left);
                string label = !started ? "LET'S PLAY  >" : paused && world.State == RunState.Playing ? "KEEP GOING  >" : "TRY AGAIN  >";
                if (GUI.Button(new Rect(left,608,340,58),label,button))
                { if (started && paused && world.State == RunState.Playing) TogglePause(); else StartRun(); }
                if (started && paused && world.State == RunState.Playing &&
                    GUI.Button(new Rect(left+360,608,220,58),"RESTART",button)) StartRun();
                else if (picking && GUI.Button(new Rect(left+360,608,300,58),
                    "SHOP  /  " + profile.coins,button)) shopOpen = true;
                GUI.Label(new Rect(left,676,680,44),recordingError.Length > 0 ? recordingError :
                    started ? "Run log: " + recordingPath : "PROTOTYPE 0.6  /  WEAPONS + BUDDIES + BOSS + SHOP",small);
            }
        }
        /// <summary>
        /// The weapon is chosen before the run and cannot change during it, so the upgrades that
        /// appear later all belong to the same weapon.
        /// </summary>
        private void DrawWeaponPicker(float left)
        {
            GUI.Label(new Rect(left,434,680,30),"MAIN WEAPON  /  pick one for the whole run",small);
            var weapons = new[] { WeaponId.Punch, WeaponId.Arrow, WeaponId.Laser };
            for (int i = 0; i < weapons.Length; i++)
            {
                bool chosen = weapon == weapons[i];
                Color previous = GUI.color;
                GUI.color = chosen ? mint : new Color(1,1,1,.55f);
                if (GUI.Button(new Rect(left + i * 190,464,178,44),
                    (chosen ? "> " : "") + WeaponName(weapons[i]),button)) weapon = weapons[i];
                GUI.color = previous;
            }

            GUI.Label(new Rect(left,516,680,30),"BUDDY  /  " + PetRole(petChoice),small);
            var pets = new[] { PetId.Mochi, PetId.Bori, PetId.Coco };
            for (int i = 0; i < pets.Length; i++)
            {
                bool owned = profile.IsUnlocked(pets[i]);
                bool chosen = petChoice == pets[i];
                Color previous = GUI.color;
                GUI.color = !owned ? new Color(1,1,1,.3f) : chosen ? PetColor(pets[i]) : new Color(1,1,1,.55f);
                string label = owned ? (chosen ? "> " : "") + PetName(pets[i])
                    : PetName(pets[i]) + "  " + PlayerProfile.PriceOf(pets[i], config);
                if (GUI.Button(new Rect(left + i * 190,546,178,44),label,button))
                {
                    // A locked buddy sends the player to the shop rather than silently doing nothing.
                    if (owned) petChoice = pets[i]; else shopOpen = true;
                }
                GUI.color = previous;
            }
        }

        /// <summary>
        /// The shop is the only place coins are spent. It is deliberately a separate screen so a
        /// mis-tap on the start screen cannot buy anything.
        /// </summary>
        private void DrawShop(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.97f));
            float left = 32;
            GUI.Label(new Rect(left,120,width-64,50),"BUDDY SHOP",heading);
            GUI.Label(new Rect(left,176,width-64,40),
                profile.coins + " COINS   /   " + profile.runsFinished + " runs finished",body);
            GUI.Label(new Rect(left,220,width-64,34),
                "Coins come from KOs, time survived and taking down the big one.",small);

            var pets = new[] { PetId.Mochi, PetId.Bori, PetId.Coco };
            float gap = 16, cardWidth = (width - 64 - gap * 2) / 3;
            for (int i = 0; i < pets.Length; i++)
            {
                PetId id = pets[i];
                bool owned = profile.IsUnlocked(id);
                int price = PlayerProfile.PriceOf(id, config);
                float x = left + i * (cardWidth + gap);
                Panel(new Rect(x,280,cardWidth,280),new Color32(34,57,65,255));

                Color previous = GUI.color;
                GUI.color = PetColor(id);
                GUI.Label(new Rect(x+16,296,cardWidth-32,44),PetName(id),heading);
                GUI.color = previous;
                GUI.Label(new Rect(x+16,344,cardWidth-32,110),PetRole(id),body);

                if (owned)
                    GUI.Label(new Rect(x+16,470,cardWidth-32,40),
                        id == PlayerProfile.StarterPet ? "YOURS FROM THE START" : "OWNED",small);
                else if (profile.coins < price)
                    GUI.Label(new Rect(x+16,470,cardWidth-32,40),
                        price + " COINS  /  " + (price - profile.coins) + " short",small);
                else if (GUI.Button(new Rect(x+16,462,cardWidth-32,46),"UNLOCK  " + price,button))
                    Buy(id);
            }

            if (GUI.Button(new Rect(left,596,300,58),"< BACK",button)) shopOpen = false;
            GUI.Label(new Rect(left,668,width-64,40),
                ProfileStore.Status.Length > 0 ? ProfileStore.Status : "Save: " + ProfileStore.FilePath,small);
        }

        private static string WeaponName(WeaponId id) =>
            id == WeaponId.Arrow ? "ARROW" : id == WeaponId.Laser ? "LASER" : "PUNCH";

        private static string WeaponHint(WeaponId id) =>
            id == WeaponId.Arrow ? "drag to aim, release to loose" :
            id == WeaponId.Laser ? "hold to burn, watch the heat" :
            "tap to swing, drag to aim";

        private void DrawUpgradeChoices(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.97f));
            float left = 32, gap = 16, cardWidth = (width - 64 - gap * 2) / 3;
            GUI.Label(new Rect(left,130,width-64,50),"LEVEL UP  /  CHOOSE YOUR NEXT PAT",heading);
            GUI.Label(new Rect(left,187,width-64,50),"Combat is paused. Pick one card. Keyboard: 1 / 2 / 3.",body);
            for (int i = 0; i < world.UpgradeChoices.Count; i++)
            {
                UpgradeChoice choice = world.UpgradeChoices[i];
                float x = left + i * (cardWidth + gap);
                Panel(new Rect(x,260,cardWidth,310),new Color32(34,57,65,255));
                GUI.Label(new Rect(x+16,280,cardWidth-32,32),"OPTION " + (i+1) + "  /  RANK " + choice.Rank,small);
                GUI.Label(new Rect(x+16,326,cardWidth-32,66),choice.Title,heading);
                GUI.Label(new Rect(x+16,396,cardWidth-32,100),choice.Description,body);
                if (GUI.Button(new Rect(x+16,508,cardWidth-32,46),"PICK " + (i+1),button))
                { SelectUpgrade(i); break; }
            }
        }
        private static string FormatTime(float time) => ((int)time / 60).ToString("00") + ":" + ((int)time % 60).ToString("00");
    }
}
