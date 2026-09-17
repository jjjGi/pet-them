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
        private Transform player, pet;
        private readonly Dictionary<int, GameObject> arrowViews = new Dictionary<int, GameObject>();
        private readonly List<int> staleArrows = new List<int>();
        private readonly List<GameObject> droneViews = new List<GameObject>();
        private readonly List<GameObject> orbViews = new List<GameObject>();
        private GameObject beam;
        private WeaponId weapon = WeaponId.Punch;
        private PetId petChoice = PetId.Mochi;
        private PlayerProfile profile;
        private bool shopOpen;
        private int lastRunCoins;
        private Vec2 move, aim, queuedAim;
        private bool queuedPunch, holdAttack, paused, started;
        private int moveFinger = -1, attackFinger = -1;
        private Vector2 moveAnchor, attackAnchor, movePoint, attackPoint;
        private float drawAmount;
        private float accumulator, hurtFlash;
        private string recordingPath = "", recordingError = "";
        private GUIStyle title, heading, body, small, button, menuTitle, menuHeading, menuButton, diagnostic;
        private string fontReport = "";
        private bool koreanUnavailable;

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
            var asset = Resources.Load<TextAsset>("balance-default");
            if (asset == null) throw new InvalidOperationException("Missing balance-default.json.");
            config = JsonUtility.FromJson<BalanceConfig>(asset.text);
            config.Validate();
            Texts.UseSystemLanguage(Application.systemLanguage == SystemLanguage.Korean);
            profile = ProfileStore.Load();
            if (!profile.IsUnlocked(petChoice)) petChoice = PlayerProfile.StarterPet;
            CreateArena();
            player = Creature("You", Look.Player, 1.0f, 10).transform;
            pet = Creature("Mochi", Look.Mochi, 0.62f, 11).transform;
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
            foreach (var view in droneViews) Destroy(view);
            droneViews.Clear();
            foreach (var view in orbViews) Destroy(view);
            orbViews.Clear();
            foreach (var view in transient) Destroy(view);
            transient.Clear(); transientEnds.Clear();
            beam.SetActive(false);
            // The pet is rebuilt because its colour is part of telling the three apart.
            if (pet != null) Destroy(pet.gameObject);
            pet = Creature(petChoice.ToString(), LookOf(petChoice), 0.62f, 11).transform;
            world = new CombatWorld(config, unchecked(Environment.TickCount), true, weapon, petChoice);
            started = true; paused = false; shopOpen = false; accumulator = 0; hurtFlash = 0;
            ClearInput();
            recordingError = "";
            try { recorder = new RunRecorder(world); recordingPath = recorder.FilePath; }
            catch (Exception ex) { recordingError = Texts.RecordingUnavailable(ex.Message); Debug.LogWarning(recordingError); }
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
                    world.Step(new PlayerInput(move, queuedPunch ? queuedAim : aim, queuedPunch, holdAttack, drawAmount));
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
            // The sprite carries its own colour, so this tints rather than replaces it.
            player.GetComponent<SpriteRenderer>().color = Time.unscaledTime < hurtFlash ? coral : Color.white;
            SynchronizeEnemies();
            SynchronizeArrows();
            SynchronizeCompanions();
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
                        attackPoint = p;
                        Vector2 delta = p - attackAnchor;
                        // The bow is pulled backwards and the arrow leaves forwards, so its aim is
                        // the opposite of the drag. The punch and the laser point where you drag.
                        Vector2 heading = weapon == WeaponId.Arrow ? -delta : delta;
                        aim = delta.magnitude > radius * .15f
                            ? new Vec2(heading.x, heading.y).Normalized : new Vec2();
                        drawAmount = Mathf.Clamp01(delta.magnitude / radius);
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
                if (Input.GetMouseButtonDown(0)) { attackAnchor = Input.mousePosition; }
                attackPoint = Input.mousePosition;
                holdAttack = Input.GetMouseButton(0);

                if (weapon == WeaponId.Arrow)
                {
                    // The bow is drawn from wherever the button went down, the same gesture as the
                    // movement stick. Pointing at the cursor would make it a click again, and the
                    // arrow leaves the opposite way to the pull, like letting go of a bowstring.
                    Vector2 pull = (Vector2)Input.mousePosition - attackAnchor;
                    aim = pull.magnitude > radius * .15f ? new Vec2(-pull.x, -pull.y).Normalized : new Vec2();
                    drawAmount = Mathf.Clamp01(pull.magnitude / radius);
                }
                else
                {
                    Vector3 point = gameCamera.ScreenToWorldPoint(Input.mousePosition);
                    aim = new Vec2(point.x - world.Position.x, point.y - world.Position.y);
                    // The punch swings on press; the laser burns for as long as it is held.
                    if (Input.GetMouseButtonDown(0) && weapon == WeaponId.Punch)
                    { queuedPunch = true; queuedAim = aim; }
                }
            }
            if (Input.GetKey(KeyCode.Space))
            {
                holdAttack = true;
                // SPACE has nothing to pull, so it stands for a full draw at the nearest enemy.
                if (weapon == WeaponId.Arrow) { aim = new Vec2(); drawAmount = 1; }
            }
            if (Input.GetKeyDown(KeyCode.Space) && weapon == WeaponId.Punch)
            { aim = new Vec2(); queuedAim = aim; queuedPunch = true; }
        }

        private void ClearInput()
        {
            moveFinger = attackFinger = -1;
            move = aim = queuedAim = new Vec2(); queuedPunch = holdAttack = false; drawAmount = 0; accumulator = 0;
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
            recordingError = Texts.RecordingStopped(ex.Message);
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
            Art.Release();

        }

        private void CreateArena()
        {
            Sprite artwork = Art.Arena;
            if (artwork != null)
            {
                var arena = new GameObject("Forest playground");
                arena.transform.SetParent(transform);
                // Put the decorative border outside the unchanged combat rectangle.
                arena.transform.localScale = new Vector3(config.arenaHalfWidth * 2 / .88f / artwork.bounds.size.x,
                    config.arenaHalfHeight * 2 / .88f / artwork.bounds.size.y, 1);
                var renderer = arena.AddComponent<SpriteRenderer>();
                renderer.sprite = artwork;
                renderer.sortingOrder = -10;
                gameCamera.backgroundColor = new Color32(40, 65, 57, 255);
                return;
            }
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
            renderer.sprite = Art.Block; renderer.color = color; renderer.sortingOrder = order;
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
                    view = Creature(enemy.Kind.ToString(), LookOf(enemy.Kind),
                        enemy.Radius * 2.5f, enemy.Kind == EnemyKind.Boss ? 7 : 5);
                    views.Add(enemy.Id, view);
                }
                view.transform.position = new Vector3(enemy.Position.x,enemy.Position.y,0);
                // A chilled enemy is tinted towards Bori's blue so the slow is visible.
                // The sprite is already coloured, so a chilled enemy is tinted rather than recoloured.
                view.GetComponent<SpriteRenderer>().color = enemy.Slowed
                    ? Color.Lerp(Color.white, Art.ColorOf(Look.Bori), .5f)
                    : Color.white;
            }
        }
        /// <summary>
        /// One GameObject per creature. The face is baked into the sprite, so a hundred enemies
        /// cost a hundred renderers instead of five hundred.
        /// </summary>
        private GameObject Creature(string name, Look look, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * size;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Art.Creature(look);
            renderer.sortingOrder = order;
            return go;
        }

        private static Look LookOf(PetId id) =>
            id == PetId.Bori ? Look.Bori : id == PetId.Coco ? Look.Coco : Look.Mochi;

        private static Look LookOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return Look.Runner;
                case EnemyKind.Brute: return Look.Brute;
                case EnemyKind.Boss: return Look.Boss;
                default: return Look.Grunt;
            }
        }

        private Color PetColor(PetId id) => Art.ColorOf(LookOf(id));

        private static Color EnemyColor(EnemyKind kind) => Art.ColorOf(LookOf(kind));

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

        /// <summary>
        /// Keeps one object per drone and per orb. They are created as ranks are taken and never
        /// destroyed mid-run, because a run only ever gains them.
        /// </summary>
        private void SynchronizeCompanions()
        {
            while (droneViews.Count < world.Drones.Count)
                droneViews.Add(RectSprite("Drone", Vector2.zero, new Vector2(.42f,.42f),
                    KindColor(UpgradeKind.Friend), 8));
            while (orbViews.Count < world.Orbs.Count)
                orbViews.Add(RectSprite("Orb", Vector2.zero, new Vector2(.34f,.34f),
                    KindColor(UpgradeKind.Trigger), 8));

            for (int i = 0; i < droneViews.Count; i++)
            {
                bool live = i < world.Drones.Count;
                droneViews[i].SetActive(live);
                if (!live) continue;
                Drone drone = world.Drones[i];
                droneViews[i].transform.position = new Vector3(drone.Position.x, drone.Position.y, 0);
                // Spin while it works, and flash on the step it fired.
                droneViews[i].transform.rotation = Quaternion.Euler(0, 0, world.Time * 220);
                droneViews[i].GetComponent<SpriteRenderer>().color =
                    drone.Fired ? paper : KindColor(UpgradeKind.Friend);
                if (drone.Fired) ShowDroneShot(drone);
            }
            for (int i = 0; i < orbViews.Count; i++)
            {
                bool live = i < world.Orbs.Count;
                orbViews[i].SetActive(live);
                if (!live) continue;
                Orb orb = world.Orbs[i];
                orbViews[i].transform.position = new Vector3(orb.Position.x, orb.Position.y, 0);
                orbViews[i].transform.rotation = Quaternion.Euler(0, 0, world.Time * 160);
            }
        }

        /// <summary>Draws the drone's shot in the shape of the weapon it is copying.</summary>
        private void ShowDroneShot(Drone drone)
        {
            // The arrow drone launches a real projectile, which is already drawn on its own.
            if (weapon == WeaponId.Arrow) return;
            var from = new Vector2(drone.Position.x, drone.Position.y);
            var to = new Vector2(drone.LastTarget.x, drone.LastTarget.y);
            Vector2 delta = to - from;
            float thickness = weapon == WeaponId.Laser ? .16f : .09f;
            GameObject fx = RectSprite("Drone shot", (from + to) / 2,
                new Vector2(delta.magnitude, thickness), KindColor(UpgradeKind.Friend), 9);
            fx.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            transient.Add(fx);
            transientEnds.Add(Time.unscaledTime + (weapon == WeaponId.Laser ? .14f : .09f));
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
                ring.transform.localScale = Vector3.one * 3.5f;
                var hr = ring.AddComponent<SpriteRenderer>();
                hr.sprite = Art.Ring;
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
                fx.transform.position = player.position + new Vector3(e.x,e.y,0) * world.PunchRange * .5f;
                // The ring sits at 0.4 of the sprite, so this draws it at the real punch reach.
                fx.transform.localScale = Vector3.one * (world.PunchRange / .4f);
                var r = fx.AddComponent<SpriteRenderer>(); r.sprite = Art.Ring;
                r.color = new Color(mint.r,mint.g,mint.b,.45f); r.sortingOrder = 8;
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
        /// <summary>
        /// Builds the GUI styles, including a font that can actually draw the current language.
        /// </summary>
        /// <remarks>
        /// IMGUI's built-in font has no Hangul, so Korean renders as empty boxes with it. A dynamic
        /// OS font is used instead: the list is tried in order and the first one present wins, which
        /// covers Windows, macOS and Android without shipping a font file we would have to license.
        /// If none of them exist the built-in font is kept, and the game is readable in English.
        /// </remarks>
        private void InitStyles()
        {
            if (body != null) return;
            Font glyphs = ResolveFont();

            title = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 15 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 23, fontStyle = FontStyle.Bold };
            if (glyphs != null)
                title.font = heading.font = body.font = small.font = button.font = glyphs;
            title.normal.textColor = heading.normal.textColor = body.normal.textColor = paper;
            small.normal.textColor = mint;
            button.normal.textColor = button.hover.textColor = button.active.textColor = paper;
            menuTitle = new GUIStyle(title) { fontSize = 44, wordWrap = true };
            menuHeading = new GUIStyle(heading) { fontSize = 24, wordWrap = true };
            menuButton = new GUIStyle(button) { wordWrap = true };

            // Deliberately keeps the built-in font: if the chosen one cannot draw, this line is
            // the only thing on screen that can say so.
            diagnostic = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            diagnostic.normal.textColor = new Color(1, 1, 1, .5f);
        }

        /// <summary>
        /// Picks a font that can actually draw the text, or gives up and keeps the built-in one.
        /// </summary>
        /// <remarks>
        /// The first attempt at this handed a list of font names to the OS and used whatever came
        /// back. On Android none of those names existed, the call still returned a Font object,
        /// and assigning it wiped every label in the game -- English included. So: ask the device
        /// what it has, and prove the result can draw an "A" and a Hangul syllable before using it.
        ///
        /// Returning null is a valid outcome. The built-in font has no Hangul but it does have
        /// Latin, and English that renders beats Korean that does not.
        /// </remarks>
        private Font ResolveFont()
        {
            string[] wanted =
            {
                "Malgun Gothic", "맑은 고딕",              // Windows
                "Apple SD Gothic Neo", "AppleGothic",       // macOS and iOS
                "Noto Sans CJK", "Noto Sans KR", "NotoSansKR",
                "NanumGothic", "Nanum Gothic", "NanumBarunGothic",
                "Droid Sans Fallback", "DroidSansFallback", // older Android
                "Noto Sans", "Roboto", "Arial",             // Latin-only last resorts
            };

            string[] installed;
            try { installed = Font.GetOSInstalledFontNames() ?? Array.Empty<string>(); }
            catch (Exception) { installed = Array.Empty<string>(); }

            Font latinOnly = null;
            string latinName = "";
            foreach (string want in wanted)
            {
                foreach (string have in installed)
                {
                    if (have == null || have.IndexOf(want, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Font candidate = TryFont(have);
                    if (candidate == null) continue;
                    if (candidate.HasCharacter('가'))
                    {
                        fontReport = "font: " + have + " (Hangul ok)";
                        return candidate;
                    }
                    if (latinOnly == null) { latinOnly = candidate; latinName = have; }
                }
            }

            // Some Android builds report no installed fonts at all yet still create one from a
            // name. Worth a try, but only because the result is verified before it is used.
            foreach (string want in wanted)
            {
                Font candidate = TryFont(want);
                if (candidate == null || !candidate.HasCharacter('가')) continue;
                fontReport = "font: " + want + " (unlisted, Hangul ok)";
                return candidate;
            }

            // A sample of what the device reported, so the next fix is not another guess.
            string sample = installed.Length == 0 ? "none"
                : string.Join(", ", installed, 0, Math.Min(6, installed.Length));

            if (latinOnly != null)
            {
                // Korean would come out blank in this font, so do not offer a language that cannot
                // be read. English in a font that works is the better failure.
                koreanUnavailable = true;
                Texts.Use(Language.English);
                fontReport = $"font: {latinName}, no Hangul. Korean off. "
                    + $"{installed.Length} OS fonts: {sample}";
                return latinOnly;
            }

            koreanUnavailable = true;
            Texts.Use(Language.English);
            fontReport = $"font: built-in fallback, nothing usable. Korean off. "
                + $"{installed.Length} OS fonts: {sample}";
            return null;
        }

        private static Font TryFont(string name)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(name, 32);
                // A font that cannot draw a plain "A" is not usable, whatever the OS reported.
                return font != null && font.HasCharacter('A') ? font : null;
            }
            catch (Exception)
            {
                return null;
            }
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
            GUI.Label(new Rect(28,15,260,45),Texts.GameTitle,heading);
            GUI.Label(new Rect(width/2-100,13,240,45),FormatTime(world.Time) + " / 03:00",heading);
            GUI.Label(new Rect(28,87,360,32),Texts.Pet(petChoice) + "  /  " + Texts.Weapon(weapon),small);
            if (started)
            {
                Panel(new Rect(28,48,220,9),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,48,220 * world.Health / world.MaxHealth,9),mint);
                GUI.Label(new Rect(28,119,370,30),Texts.Level(world.Level, world.Level >= CombatWorld.MaxLevel,
                    world.Experience, world.ExperienceToNextLevel),small);
                Panel(new Rect(28,153,220,7),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,153,220 * (world.Level >= CombatWorld.MaxLevel ? 1 :
                    Mathf.Clamp01((float)world.Experience / world.ExperienceToNextLevel)),7),mint);
                GUI.Label(new Rect(width-380,22,240,35),Texts.WaveAndKills(world.Wave, world.Kills),body);
                if (world.State == RunState.Playing && GUI.Button(new Rect(width-110,15,88,43),paused ? Texts.Resume : Texts.Pause,button)) TogglePause();
                if (world.Boss != null)
                {
                    GUI.Label(new Rect(width/2-220,600,440,30),Texts.BossHealth(Mathf.CeilToInt(world.Boss.Health)),small);
                    Panel(new Rect(width/2-220,632,440,14),new Color(.3f,.35f,.38f));
                    Panel(new Rect(width/2-220,632,440 * world.Boss.Health / world.Boss.MaxHealth,14),
                        EnemyColor(EnemyKind.Boss));
                }
                else if (!world.BossDefeated && world.State == RunState.Playing)
                    GUI.Label(new Rect(width/2-140,600,300,30),
                        Texts.BossIncoming(Mathf.CeilToInt(world.SecondsToBoss)),small);
                if (world.GuardReduction > 0)
                    GUI.Label(new Rect(28,188,360,30),
                        Texts.GuardStatus(Mathf.RoundToInt(world.GuardReduction * 100), Mathf.CeilToInt(world.SecondsToPetHeal)),small);
                GUI.Label(new Rect(28,666,400,30),Texts.MoveHint,small);
                GUI.Label(new Rect(width-460,666,440,30),Texts.Weapon(weapon) + "  /  " + Texts.WeaponHint(weapon),small);
                if (weapon == WeaponId.Laser)
                {
                    GUI.Label(new Rect(width-460,188,250,30),world.Overheated ? Texts.Overheated : Texts.Heat,small);
                    Panel(new Rect(width-460,222,220,9),new Color(.3f,.35f,.38f));
                    Panel(new Rect(width-460,222,220 * world.Heat / 100f,9),
                        world.Overheated ? coral : new Color(1,.68f,.36f));
                }
                if (moveFinger >= 0)
                {
                    Vector2 anchor = new Vector2(moveAnchor.x / scale,(Screen.height-moveAnchor.y)/scale);
                    Vector2 stick = new Vector2(movePoint.x / scale,(Screen.height-movePoint.y)/scale);
                    GUI.color = new Color(mint.r,mint.g,mint.b,.2f);
                    GUI.DrawTexture(new Rect(anchor.x-64,anchor.y-64,128,128),Art.SoftCircle.texture);
                    GUI.color = mint;
                    Vector2 delta = Vector2.ClampMagnitude(stick-anchor,64);
                    GUI.DrawTexture(new Rect(anchor.x+delta.x-22,anchor.y+delta.y-22,44,44),Art.SoftCircle.texture);
                    GUI.color = Color.white;
                }
                if (weapon == WeaponId.Arrow && holdAttack) DrawBowGauge(scale);
            }
            if (started && !paused && world.State == RunState.Playing && world.HasUpgradeChoice)
                DrawUpgradeChoices(width);
            bool onMenu = !started || paused || world.State != RunState.Playing;
            if (onMenu && shopOpen) { DrawShop(width); return; }
            if (onMenu) DrawMainMenu(width);
        }
        /// <summary>
        /// Shows the bow being drawn: where the pull started, how far it has come, and how strong
        /// the shot would be if let go now.
        /// </summary>
        /// <remarks>
        /// The strength comes from CombatWorld.DrawPower, the same function that fires the arrow,
        /// so the gauge cannot disagree with the shot. Below the minimum it reads as empty and the
        /// line goes grey, which is what tells the player a tap is not a shot.
        /// </remarks>
        private void DrawBowGauge(float scale)
        {
            Vector2 anchor = new Vector2(attackAnchor.x / scale, (Screen.height - attackAnchor.y) / scale);
            Vector2 pulled = new Vector2(attackPoint.x / scale, (Screen.height - attackPoint.y) / scale);
            Vector2 delta = Vector2.ClampMagnitude(pulled - anchor, 64);
            float power = world.DrawPower(drawAmount);
            Color tint = power > 0 ? Color.Lerp(paper, coral, power) : new Color(.6f, .64f, .68f);

            GUI.color = new Color(tint.r, tint.g, tint.b, .18f);
            GUI.DrawTexture(new Rect(anchor.x - 64, anchor.y - 64, 128, 128), Art.SoftCircle.texture);

            // Where the finger is: the string drawn back. Dimmer, because it is not the answer to
            // "where will this go".
            GUI.color = new Color(tint.r, tint.g, tint.b, .35f);
            GUI.DrawTexture(new Rect(anchor.x + delta.x - 13, anchor.y + delta.y - 13, 26, 26),
                Art.SoftCircle.texture);

            // Dots the other way, growing outward: the arrow leaves opposite the pull, and this is
            // the only thing on screen that says so.
            GUI.color = new Color(tint.r, tint.g, tint.b, .8f);
            for (int i = 1; i <= 5; i++)
            {
                Vector2 at = anchor - delta * (i / 5f);
                float dot = 4 + i * 1.8f;
                GUI.DrawTexture(new Rect(at.x - dot, at.y - dot, dot * 2, dot * 2), Art.SoftCircle.texture);
            }

            GUI.color = new Color(.25f, .28f, .31f, .85f);
            GUI.DrawTexture(new Rect(anchor.x - 62, anchor.y + 74, 124, 12), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.DrawTexture(new Rect(anchor.x - 62, anchor.y + 74, 124 * power, 12), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>
        /// The weapon is chosen before the run and cannot change during it, so the upgrades that
        /// appear later all belong to the same weapon.
        /// </summary>
        private Vector2 menuScroll;

        private void DrawMainMenu(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.92f));
            bool portraits = width >= 1100;
            float contentWidth = Mathf.Min(760, width - (portraits ? 280 : 64));
            float left = (width - contentWidth - (portraits ? 210 : 0)) / 2 + (portraits ? 210 : 0);
            if (portraits)
            {
                GUI.DrawTexture(new Rect(left-210,150,180,180),Art.Creature(Look.Player).texture,ScaleMode.ScaleToFit,true);
                GUI.DrawTexture(new Rect(left-176,355,120,120),Art.Creature(LookOf(petChoice)).texture,ScaleMode.ScaleToFit,true);
            }
            // GUILayout measures each wrapped label with the active font. No following control
            // can begin until the preceding text has consumed its actual height.
            GUILayout.BeginArea(new Rect(left,100,contentWidth,600));
            menuScroll = GUILayout.BeginScrollView(menuScroll, false, false);
            GUILayout.Label(!started ? Texts.StartHeadline :
                paused && world.State == RunState.Playing ? Texts.PausedHeadline :
                world.BossDefeated ? Texts.BossDownHeadline :
                world.State == RunState.Won ? Texts.WonHeadline : Texts.LostHeadline,menuTitle);
            GUILayout.Space(12);
            GUILayout.Label(!started ? Texts.StartBlurb :
                Texts.RunResult(FormatTime(world.Time),world.Kills,world.Wave) + "\n" +
                Texts.CoinsEarned(lastRunCoins,world.BossDefeated,profile.coins),menuHeading);
            GUILayout.Space(12);
            GUILayout.Label(!started ? Texts.StartControls : Texts.EnemyLegend,body);
            GUILayout.Space(14);
            // Once a run is under way, what was picked matters more than the control reminder.
            if (started) { DrawBuild(); GUILayout.Space(14); }
            bool picking = !started || world.State != RunState.Playing;
            if (picking) DrawWeaponPicker();
            GUILayout.Space(14);
            GUILayout.BeginHorizontal();
            string label = !started ? Texts.Play :
                paused && world.State == RunState.Playing ? Texts.KeepGoing : Texts.TryAgain;
            if (GUILayout.Button(label,menuButton,GUILayout.MinHeight(52)))
            { if (started && paused && world.State == RunState.Playing) TogglePause(); else StartRun(); }
            GUILayout.Space(12);
            if (started && paused && world.State == RunState.Playing)
            {
                if (GUILayout.Button(Texts.Restart,menuButton,GUILayout.MinHeight(52))) StartRun();
            }
            else if (picking && GUILayout.Button(Texts.Shop(profile.coins),menuButton,GUILayout.MinHeight(52))) shopOpen = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            // The language button has its own row instead of covering the shop on narrow views.
            if (!koreanUnavailable &&
                GUILayout.Button(Texts.LanguageName(Texts.Next),menuButton,GUILayout.MinHeight(40)))
                Texts.Use(Texts.Next);
            GUILayout.Space(8);
            GUILayout.Label(recordingError.Length > 0 ? recordingError :
                started ? Texts.RunLog(recordingPath) : Texts.Build,small);
            // Drawn with the built-in font on purpose, so it survives a font that cannot draw.
            GUILayout.Label(fontReport,diagnostic);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawWeaponPicker()
        {
            GUILayout.Label(Texts.WeaponPickerLabel,small);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            var weapons = new[] { WeaponId.Punch, WeaponId.Arrow, WeaponId.Laser };
            for (int i = 0; i < weapons.Length; i++)
            {
                if (i > 0) GUILayout.Space(12);
                bool chosen = weapon == weapons[i];
                Color previous = GUI.color;
                GUI.color = chosen ? mint : new Color(1,1,1,.55f);
                if (GUILayout.Button((chosen ? "> " : "") + Texts.Weapon(weapons[i]),button,
                    GUILayout.MinHeight(44),GUILayout.ExpandWidth(true))) weapon = weapons[i];
                GUI.color = previous;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(Texts.PetPickerLabel(petChoice),small);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            var pets = new[] { PetId.Mochi, PetId.Bori, PetId.Coco };
            for (int i = 0; i < pets.Length; i++)
            {
                if (i > 0) GUILayout.Space(12);
                bool owned = profile.IsUnlocked(pets[i]);
                bool chosen = petChoice == pets[i];
                Color previous = GUI.color;
                GUI.color = !owned ? new Color(1,1,1,.3f) : chosen ? PetColor(pets[i]) : new Color(1,1,1,.55f);
                string label = owned ? (chosen ? "> " : "") + Texts.Pet(pets[i])
                    : Texts.Pet(pets[i]) + "  " + PlayerProfile.PriceOf(pets[i],config);
                if (GUILayout.Button(label,button,GUILayout.MinHeight(44),GUILayout.ExpandWidth(true)))
                { if (owned) petChoice = pets[i]; else shopOpen = true; }
                GUI.color = previous;
            }
            GUILayout.EndHorizontal();
        }
        /// <summary>
        /// The shop is the only place coins are spent. It is deliberately a separate screen so a
        /// mis-tap on the start screen cannot buy anything.
        /// </summary>
        private void DrawShop(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.97f));
            float left = 32;
            GUI.Label(new Rect(left,120,width-64,50),Texts.ShopHeadline,heading);
            GUI.Label(new Rect(left,176,width-64,40),
                Texts.ShopWallet(profile.coins, profile.runsFinished),body);
            GUI.Label(new Rect(left,220,width-64,34),
                Texts.ShopHint,small);

            var pets = new[] { PetId.Mochi, PetId.Bori, PetId.Coco };
            float gap = 16, cardWidth = (width - 64 - gap * 2) / 3;
            for (int i = 0; i < pets.Length; i++)
            {
                PetId id = pets[i];
                bool owned = profile.IsUnlocked(id);
                int price = PlayerProfile.PriceOf(id, config);
                float x = left + i * (cardWidth + gap);
                Panel(new Rect(x,266,cardWidth,310),new Color32(34,57,65,255));
                GUI.DrawTexture(new Rect(x+cardWidth/2-50,278,100,100),Art.Creature(LookOf(id)).texture,ScaleMode.ScaleToFit,true);

                Color previous = GUI.color;
                GUI.color = PetColor(id);
                GUI.Label(new Rect(x+16,380,cardWidth-32,38),Texts.Pet(id),heading);
                GUI.color = previous;
                GUI.Label(new Rect(x+16,422,cardWidth-32,80),Texts.PetRole(id),body);

                if (owned)
                    GUI.Label(new Rect(x+16,516,cardWidth-32,46),
                        id == PlayerProfile.StarterPet ? Texts.Starter : Texts.Owned,small);
                else if (profile.coins < price)
                    GUI.Label(new Rect(x+16,516,cardWidth-32,46),
                        Texts.Short(price, price - profile.coins),small);
                else if (GUI.Button(new Rect(x+16,516,cardWidth-32,46),Texts.Unlock(price),button))
                    Buy(id);
            }

            if (GUI.Button(new Rect(left,596,300,58),Texts.Back,button)) shopOpen = false;
            GUI.Label(new Rect(left,668,width-64,40),
                ProfileStore.Status.Length > 0 ? ProfileStore.Status : Texts.SavePath(ProfileStore.FilePath),small);
        }

        private void DrawUpgradeChoices(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.97f));
            float left = 32, gap = 16, cardWidth = (width - 64 - gap * 2) / 3;
            GUI.Label(new Rect(left,130,width-64,50),Texts.LevelUpHeadline,heading);
            GUI.Label(new Rect(left,187,width-64,50),Texts.LevelUpHint,body);
            for (int i = 0; i < world.UpgradeChoices.Count; i++)
            {
                UpgradeChoice choice = world.UpgradeChoices[i];
                UpgradeKind kind = CombatWorld.KindOf(choice.Id);
                Color accent = KindColor(kind);
                float x = left + i * (cardWidth + gap);

                Panel(new Rect(x,260,cardWidth,310),new Color32(34,57,65,255));
                // A colour strip down the side, so the three cards read as three kinds of choice
                // before any of the text has been read.
                Panel(new Rect(x,260,6,310),accent);

                Color previous = GUI.color;
                GUI.color = accent;
                GUI.Label(new Rect(x+20,278,cardWidth-36,30),
                    Texts.UpgradeKindName(kind) + "   " + Texts.RankDots(choice.Rank),small);
                GUI.color = previous;

                GUI.Label(new Rect(x+20,320,cardWidth-36,66),Texts.UpgradeTitle(choice.Id),heading);
                GUI.Label(new Rect(x+20,386,cardWidth-36,120),Texts.UpgradeDescription(choice.Id),body);
                if (GUI.Button(new Rect(x+20,508,cardWidth-40,46),Texts.PickOption(i+1),button))
                { SelectUpgrade(i); break; }
            }
        }
        private static Color KindColor(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Weapon: return new Color32(255, 168, 120, 255);
                case UpgradeKind.Pet: return new Color32(124, 239, 192, 255);
                case UpgradeKind.Friend: return new Color32(138, 200, 255, 255);
                case UpgradeKind.Trigger: return new Color32(255, 212, 108, 255);
                default: return new Color32(206, 176, 255, 255);
            }
        }

        /// <summary>
        /// The run so far: weapon, pet, level, and every upgrade taken, grouped by family.
        /// </summary>
        /// <remarks>
        /// Shown on the pause screen because by the tenth level nobody remembers what they picked,
        /// and the picks are the whole point of the level-up screen. Grouping keeps it readable as
        /// the list grows; the ranks are dots rather than numbers so a glance is enough.
        /// </remarks>
        private void DrawBuild()
        {
            GUILayout.Label(Texts.BuildHeadline,small);
            GUILayout.Label(Texts.BuildSummary(Texts.Weapon(weapon), Texts.Pet(petChoice), world.Level),
                menuHeading);
            GUILayout.Space(6);

            var taken = world.TakenUpgrades();
            if (taken.Count == 0) { GUILayout.Label(Texts.BuildEmpty,body); return; }

            // One row per family. A run with two picks stays short; a long one still reads as a
            // build rather than a list, because related picks sit together.
            Color previous = GUI.color;
            foreach (UpgradeKind kind in (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind)))
            {
                var line = "";
                foreach (UpgradeChoice choice in taken)
                {
                    if (CombatWorld.KindOf(choice.Id) != kind) continue;
                    if (line.Length > 0) line += "    ";
                    line += Texts.UpgradeTitle(choice.Id) + " " + Texts.RankDots(choice.Rank);
                }
                if (line.Length == 0) continue;

                GUILayout.BeginHorizontal();
                GUI.color = KindColor(kind);
                GUILayout.Label(Texts.UpgradeKindName(kind),small,GUILayout.Width(70));
                GUI.color = previous;
                GUILayout.Label(line,body);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
        }

        private static string FormatTime(float time) => ((int)time / 60).ToString("00") + ":" + ((int)time % 60).ToString("00");
    }
}
