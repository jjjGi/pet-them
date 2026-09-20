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
        private GameObject beam, bossWarning;
        private WeaponId weapon = WeaponId.Punch;
        private PetId petChoice = PetId.Mochi;
        private PlayerProfile profile;
        private HomeTab homeTab = HomeTab.Home;
        private int lastRunCoins;
        private Vec2 move, aim, queuedAim;
        private bool queuedPunch, holdAttack, paused, started;
        private int moveFinger = -1, attackFinger = -1;
        private Vector2 moveAnchor, attackAnchor, movePoint, attackPoint;
        private float drawAmount;
        private float accumulator, hurtFlash;
        private GameAudio gameAudio;
        private ServerLink link;
        private Coach coach;
        private Vec2 coachPosition;
        private int coachLearned;
        private const string CoachKey = "coach.learned";
        private Transform attackPaw;
        private float playerStrike = -10, petStrike = -10, lastVisualTime = -1;
        private Vector3 strikeDirection, petStrikeDirection, lastVisualPosition;

        // Render poses only: collision, aim and damage remain owned by CombatWorld.
        private void AnimateCreatures()
        {
            float dt = world.Time - lastVisualTime;
            if (dt <= 0) return; // Cards and pause freeze the pose too.
            Vector3 position = new Vector3(world.Position.x, world.Position.y, 0);
            float moving = Mathf.Clamp01((position - lastVisualPosition).magnitude / dt / 2);
            float gait = Mathf.Sin(world.Time * 15) * moving;
            float breath = Mathf.Sin(world.Time * 3) * .018f;
            float age = world.Time - playerStrike;
            float strike = age >= 0 && age < .24f ? Mathf.Sin(age / .24f * Mathf.PI) : 0;
            float pull = weapon == WeaponId.Arrow && holdAttack ? Mathf.Clamp01(drawAmount) : 0;
            Vector3 facing = new Vector3(world.Facing.x, world.Facing.y, 0);
            float laser = world.LaserActive ? .035f + .015f * Mathf.Sin(world.Time * 65) : 0;
            player.position = position + strikeDirection * strike * (weapon == WeaponId.Punch ? .24f : -.16f)
                - facing * (pull * .12f + laser) + Vector3.up * Mathf.Abs(gait) * .055f;
            player.localScale = new Vector3(1 + breath + strike * .1f + pull * .08f,
                1 - breath - strike * .08f - pull * .06f, 1);
            player.rotation = Quaternion.Euler(0, 0, -gait * 5 - strikeDirection.x * strike * 13 + facing.x * pull * 9);
            // A separate paw makes the attack readable even on a small phone screen.
            float extension = weapon == WeaponId.Punch ? strike * .75f : -pull * .22f - strike * .12f;
            attackPaw.gameObject.SetActive(started);
            Vector3 pawDirection = strike > 0 ? strikeDirection : facing;
            attackPaw.position = position + pawDirection * (.48f + extension) + Vector3.up * .08f;
            attackPaw.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(pawDirection.y, pawDirection.x) * Mathf.Rad2Deg - 90);
            attackPaw.localScale = Vector3.one * (.55f + strike * .18f);
            float petAge = world.Time - petStrike;
            float hop = petAge >= 0 && petAge < .32f ? Mathf.Sin(petAge / .32f * Mathf.PI) : 0;
            float orbit = world.Time * 2;
            pet.position = position + new Vector3(Mathf.Cos(orbit) * 1.1f,
                Mathf.Sin(orbit) * .75f + Mathf.Sin(world.Time * 5) * .06f, 0) + petStrikeDirection * hop * .3f;
            pet.localScale = new Vector3(.62f * (1 + hop * .15f), .62f * (1 - hop * .1f), 1);
            pet.rotation = Quaternion.Euler(0, 0, Mathf.Sin(world.Time * 5) * 5 - petStrikeDirection.x * hop * 18);
            lastVisualPosition = position;
            lastVisualTime = world.Time;
        }
        private string recordingPath = "", recordingError = "", runId = "";
        private GUIStyle title, heading, body, small, button, menuTitle, menuHeading, menuButton, diagnostic, coaching;
        private GUIStyle homeTitle, bigButton, tabButton, tabButtonOn, field;
        private string serverDraft;
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
            if (FindFirstObjectByType<AudioListener>() == null) gameCamera.gameObject.AddComponent<AudioListener>();
            gameAudio = gameObject.AddComponent<GameAudio>();
            link = gameObject.AddComponent<ServerLink>();
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
            coachLearned = PlayerPrefs.GetInt(CoachKey, 0);
            coach = new Coach(coachLearned);
            profile = ProfileStore.Load();
            if (!profile.IsUnlocked(petChoice)) petChoice = PlayerProfile.StarterPet;
            CreateArena();
            player = Creature("You", Look.Player, 1.0f, 10).transform;
            pet = Creature("Mochi", Look.Mochi, 0.62f, 11).transform;
            attackPaw = new GameObject("Animated attack paw").transform;
            attackPaw.SetParent(transform);
            var palm = RectSprite("Palm", Vector2.zero, new Vector2(.7f,.7f), paper, 13);
            palm.GetComponent<SpriteRenderer>().sprite = Art.SoftCircle;
            palm.transform.SetParent(attackPaw, false);
            for (int i = 0; i < 3; i++)
            {
                var toe = RectSprite("Toe", new Vector2((i-1)*.25f,.34f), new Vector2(.3f,.34f), mint, 14);
                toe.GetComponent<SpriteRenderer>().sprite = Art.SoftCircle;
                toe.transform.SetParent(attackPaw, false);
            }
            attackPaw.gameObject.SetActive(false);
            beam = RectSprite("Laser beam", Vector2.zero, Vector2.one, coral, 9);
            beam.SetActive(false);
            // Under the creatures, so the warning never hides what is standing on it.
            bossWarning = RectSprite("Boss warning", Vector2.zero, Vector2.one, coral, 2);
            bossWarning.SetActive(false);
            world = new CombatWorld(config, 42, true, weapon, petChoice, true);
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
            bossWarning.SetActive(false);
            // The pet is rebuilt because its colour is part of telling the three apart.
            if (pet != null) Destroy(pet.gameObject);
            pet = Creature(petChoice.ToString(), LookOf(petChoice), 0.62f, 11).transform;
            world = new CombatWorld(config, unchecked(Environment.TickCount), true, weapon, petChoice, true);
            coach.Begin(weapon);
            coachPosition = world.Position;
            started = true; paused = false; accumulator = 0; hurtFlash = 0;
            playerStrike = petStrike = -10;
            lastVisualTime = -1;
            lastVisualPosition = Vector3.zero;
            ClearInput();
            recordingError = "";
            runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 8);
            try { recorder = new RunRecorder(world, runId); recordingPath = recorder.FilePath; }
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
                    TeachFromStep();
                    RecordEvents();
                    foreach (CombatEvent e in world.Events) ShowEvent(e);
                    accumulator -= CombatWorld.StepSeconds;
                    if (world.State != RunState.Playing) { EndRecording(false); ClearInput(); BankRun(); break; }
                    if (world.HasUpgradeChoice) { ClearInput(); FlushRecording(); break; }
                }
            }
            AnimateCreatures();
            // The sprite carries its own colour, so this tints rather than replaces it.
            player.GetComponent<SpriteRenderer>().color = Time.unscaledTime < hurtFlash ? coral : Color.white;
            SynchronizeEnemies();
            SynchronizeArrows();
            SynchronizeCompanions();
            DrawBeam();
            DrawBossWarning();
            gameAudio.SetCombat(started && !paused && !world.HasUpgradeChoice && world.State == RunState.Playing,
                world.LaserActive, world.Boss != null);
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
                            // A pull too short to fire is the bow's one trap: nothing happens and
                            // nothing on screen says why. DrawPower is the function that fires the
                            // arrow, so this reads the same verdict the shot will get.
                            if (weapon == WeaponId.Arrow && world.DrawPower(drawAmount) <= 0) coach.ShortDraw();
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
                    if (Input.GetMouseButtonUp(0) && world.DrawPower(drawAmount) <= 0) coach.ShortDraw();
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

        /// <summary>
        /// Hands the coach one step of the world and writes down anything it just learned.
        /// </summary>
        /// <remarks>
        /// Saved the moment a lesson clears rather than when the run ends, because the player who
        /// puts the phone down halfway through the first fight is exactly the one who should not be
        /// taught how to walk all over again.
        ///
        /// Everything it reads is a value the HUD is already printing, so the coach can never be
        /// teaching one game while the screen shows another.
        /// </remarks>
        private void TeachFromStep()
        {
            if (coach.Finished) return;
            Vec2 was = coachPosition;
            coachPosition = world.Position;
            coach.Observe(world.Time, (coachPosition - was).Length, world.Kills,
                world.SecondsToBoss, world.Boss != null || world.BossDefeated);
            if (coach.Learned == coachLearned) return;
            coachLearned = coach.Learned;
            PlayerPrefs.SetInt(CoachKey, coachLearned);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets the lessons, for a player who wants them again or is handing the phone over.</summary>
        private void ForgetCoaching()
        {
            coach.Reset();
            coachLearned = 0;
            PlayerPrefs.SetInt(CoachKey, 0);
            PlayerPrefs.Save();
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
            coach.UpgradeTaken(world.Time);
            gameAudio.Sound("upgrade");
            RecordEvents(); FlushRecording(); ClearInput();
        }
        /// <summary>
        /// Pays out a finished run. Abandoning pays nothing, so restarting is not a way to farm
        /// the first easy seconds over and over.
        /// </summary>
        private void BankRun()
        {
            if (world.State != RunState.Won && world.State != RunState.Lost) return;
            gameAudio.Sound(world.State == RunState.Won ? "win" : "lose");
            lastRunCoins = world.Coins;
            profile.AddRunReward(lastRunCoins);
            ProfileStore.Save(profile);
            // Filed at the same moment, from the same place. The upload is the server's copy of
            // a decision already made locally, not a thing the player has to wait for.
            link.Record(world, runId);
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
                float stride = Mathf.Sin(world.Time * (enemy.Kind == EnemyKind.Runner ? 19 : 10) + enemy.Id * 1.7f);
                float size = enemy.Radius * 2.5f;
                float squash = stride * .035f;
                if (enemy.Kind == EnemyKind.Boss && world.BossState == BossAction.Telegraph)
                    squash = .16f * world.BossTelegraph;
                view.transform.localScale = new Vector3(size * (1 + squash), size * (1 - squash), size);
                view.transform.rotation = Quaternion.Euler(0, 0, stride * (enemy.Kind == EnemyKind.Boss ? 2 : 5));
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

        /// <summary>
        /// Draws what the boss is about to do, while it is winding up.
        /// </summary>
        /// <remarks>
        /// The core gives the player most of a second of warning before every boss move. If the
        /// warning is not on screen that second is worth nothing, so this is not decoration: a
        /// charge shows the lane it will run down, a slam shows the circle it will cover, and both
        /// fill up as the wind-up finishes.
        /// </remarks>
        private void DrawBossWarning()
        {
            bool winding = world.Boss != null && world.BossState == BossAction.Telegraph;
            bossWarning.SetActive(winding);
            if (!winding) return;

            var at = new Vector2(world.Boss.Position.x, world.Boss.Position.y);
            float grow = Mathf.Clamp01(world.BossTelegraph);
            var renderer = bossWarning.GetComponent<SpriteRenderer>();
            Color danger = EnemyColor(EnemyKind.Boss);
            renderer.color = new Color(danger.r, danger.g, danger.b, Mathf.Lerp(.15f, .5f, grow));

            if (world.BossMove == BossAction.Charge)
            {
                // The lane it will cover, reaching further the closer it is to launching.
                float reach = config.bossChargeSpeed * config.bossChargeSeconds;
                var heading = new Vector2(world.BossHeading.x, world.BossHeading.y);
                renderer.sprite = Art.Block;
                bossWarning.transform.position = at + heading * (reach * .5f);
                bossWarning.transform.localScale =
                    new Vector3(reach * grow, config.bossRadius * 1.6f, 1);
                bossWarning.transform.rotation =
                    Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                return;
            }

            // Slam and summon both go off around the boss, so both show a circle.
            float radius = world.BossMove == BossAction.Slam
                ? config.bossSlamRadius : config.bossRadius + 1.6f;
            renderer.sprite = Art.SoftCircle;
            bossWarning.transform.position = at;
            bossWarning.transform.localScale = Vector3.one * (radius * 2 * Mathf.Lerp(.35f, 1, grow));
            bossWarning.transform.rotation = Quaternion.identity;
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
            if (e.type == "hurt") gameAudio.Sound("hurt");
            if (e.type == "boss_telegraph") gameAudio.Sound("boss");
            if (e.type == "attack") gameAudio.Sound(e.source);
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
            if (e.source == "drone")
            {
                // Consume each simulation step's event once, even across slow or fast render frames.
                foreach (Drone drone in world.Drones) if (drone.Fired) ShowDroneShot(drone);
                return;
            }
            if (e.source == "arrow") coach.ArrowFired();
            if (e.source == "punch" || e.source == "arrow")
            {
                playerStrike = world.Time;
                strikeDirection = new Vector3(e.x,e.y,0).normalized;
            }
            if (e.source == "pet")
            {
                petStrike = world.Time;
                petStrikeDirection = (new Vector3(e.x,e.y,0) - pet.position).normalized;
            }
            // The arrow and the beam are drawn from world state every frame, not as a one-off flash.
            if (e.source == "arrow" || e.source == "laser") return;
            GameObject fx;
            if (e.source == "punch")
            {
                fx = new GameObject("Punch");
                fx.transform.SetParent(transform);
                fx.transform.position = new Vector3(world.Position.x,world.Position.y,0) + new Vector3(e.x,e.y,0) * world.PunchRange * .5f;
                // The ring sits at 0.4 of the sprite, so this draws it at the real punch reach.
                fx.transform.localScale = Vector3.one * (world.PunchRange / .4f);
                var r = fx.AddComponent<SpriteRenderer>(); r.sprite = Art.Ring;
                r.color = new Color(mint.r,mint.g,mint.b,.45f); r.sortingOrder = 8;
            }
            else if (e.source == "pet")
            {
                Vector2 from = new Vector2(pet.position.x,pet.position.y);
                Vector2 to = new Vector2(e.x,e.y), delta = to - from;
                fx = RectSprite("Pet beam", (from + to) / 2, new Vector2(delta.magnitude,.07f), mint, 9);
                fx.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x) * Mathf.Rad2Deg);
            }
            else return;
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
            coaching = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            homeTitle = new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter };
            bigButton = new GUIStyle(button) { fontSize = 30 };
            // The selected tab is not just tinted: on a phone in sunlight a colour difference
            // alone is easy to miss, so the current one is also the only bold one.
            tabButton = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
            tabButtonOn = new GUIStyle(tabButton) { fontStyle = FontStyle.Bold };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 20 };
            if (glyphs != null) field.font = glyphs;
            if (glyphs != null) { tabButton.font = tabButtonOn.font = glyphs; }
            tabButton.normal.textColor = new Color(1,1,1,.45f);
            tabButtonOn.normal.textColor = mint;

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
            if (started)
            {
                Panel(new Rect(0,0,width,76),ink);
                GUI.Label(new Rect(28,15,260,45),Texts.GameTitle,heading);
                GUI.Label(new Rect(width/2-100,13,240,45),FormatTime(world.Time) + " / 03:00",heading);
                GUI.Label(new Rect(28,87,360,32),Texts.Loadout(weapon, petChoice),small);
                Panel(new Rect(28,48,220,9),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,48,220 * world.Health / world.MaxHealth,9),mint);
                GUI.Label(new Rect(28,119,370,30),Texts.Level(world.Level, world.Level >= CombatWorld.MaxLevel,
                    world.Experience, world.ExperienceToNextLevel),small);
                Panel(new Rect(28,153,220,7),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,153,220 * (world.Level >= CombatWorld.MaxLevel ? 1 :
                    Mathf.Clamp01((float)world.Experience / world.ExperienceToNextLevel)),7),mint);
                GUI.Label(new Rect(width-380,22,240,35),Texts.WaveAndKills(world.Wave, world.Kills),body);
                // The condition is announced at the top of the run and then gets out of the way.
                // It fades rather than cutting, so it does not read as a thing that needs dismissing.
                if (world.Time < 5)
                {
                    float fade = Mathf.Clamp01(5 - world.Time);
                    Color was = GUI.color;
                    GUI.color = new Color(1,1,1,fade);
                    GUI.Label(new Rect(width/2-300,96,600,36),Texts.TwistTitle(world.Twist),body);
                    GUI.Label(new Rect(width/2-300,134,600,30),Texts.TwistDescription(world.Twist),small);
                    GUI.color = was;
                }
                if (world.State == RunState.Playing && GUI.Button(new Rect(width-110,15,88,43),paused ? Texts.Resume : Texts.Pause,button)) TogglePause();
                if (world.Boss != null)
                {
                    string bossLabel = Texts.BossHealth(Mathf.CeilToInt(world.Boss.Health));
                    if (world.BossEnraged) bossLabel += "  /  " + Texts.BossEnraged;
                    GUI.Label(new Rect(width/2-220,600,440,30),bossLabel,small);
                    Panel(new Rect(width/2-220,632,440,14),new Color(.3f,.35f,.38f));
                    Panel(new Rect(width/2-220,632,440 * world.Boss.Health / world.Boss.MaxHealth,14),
                        EnemyColor(EnemyKind.Boss));
                    // The shape on the ground says where; this says what, for the second it winds up.
                    if (world.BossState == BossAction.Telegraph)
                        GUI.Label(new Rect(width/2-220,558,440,34),Texts.BossMove(world.BossMove),body);
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
                DrawCoaching(width);
            }
            if (started && !paused && world.State == RunState.Playing && world.HasUpgradeChoice)
                DrawUpgradeChoices(width);
            if (!started) { DrawHome(width); return; }
            if (world.State != RunState.Playing) { DrawResult(width); return; }
            if (paused) DrawPause(width);
        }
        /// <summary>
        /// One line of coaching over the arena, for as long as the lesson behind it is unlearned.
        /// </summary>
        /// <remarks>
        /// Not a modal sequence of steps: the run does not stop, nothing has to be dismissed, and a
        /// player who already knows the controls clears every lesson by playing and never reads a
        /// word of it. That is the whole design. A tutorial that has to be sat through is a tutorial
        /// the second player on the same phone has to sit through again.
        ///
        /// It sits below the middle of the screen, between the twist banner at the top and the boss
        /// bar underneath, so it never covers either.
        /// </remarks>
        private void DrawCoaching(float width)
        {
            if (coach.Finished || paused || world.State != RunState.Playing) return;
            CoachLesson lesson = coach.Lesson;
            if (lesson == CoachLesson.None) return;
            // Two lines tall, because the English attack lessons are long enough to wrap and
            // MiddleCenter keeps a one-line message looking the same either way.
            var box = new Rect(width / 2 - 340, 474, 680, 58);
            Panel(box, new Color(ink.r, ink.g, ink.b, .82f));
            GUI.Label(box, Texts.CoachLine(lesson, weapon), coaching);
        }

        /// <summary>
        /// Shows the bow being drawn: where the pull started, how far it has come, and how strong
        /// the shot would be if let go now.
        /// </summary>
        /// <remarks>
        /// The strength comes from CombatWorld.DrawPower, the same function that fires the arrow,
        /// so the gauge cannot disagree with the shot. Below the minimum it reads as empty and the
        /// line goes grey, which is what tells the player a tap is not a shot.
        ///
        /// Split in two on purpose. The ring, the drawn string and the power bar are the control,
        /// so they stay under the thumb that is holding them. The aim preview is not a control: it
        /// answers "where does the arrow go", and the arrow leaves the character, so it is drawn
        /// from the character. Walking while drawing used to leave the preview behind at the spot
        /// the thumb first landed, pointing from a place the arrow was never going to start.
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

            // Dots from the character, growing outward: the arrow leaves opposite the pull, and this
            // is the only thing on screen that says so. Taken from world.Facing rather than from the
            // pull, because Facing is what the shot itself uses -- the preview cannot drift off it.
            Vector3 onScreen = gameCamera.WorldToScreenPoint(new Vector3(world.Position.x, world.Position.y, 0));
            Vector2 from = new Vector2(onScreen.x / scale, (Screen.height - onScreen.y) / scale);
            Vector2 heading = new Vector2(world.Facing.x, -world.Facing.y);
            float reach = 34 + 92 * power;
            GUI.color = new Color(tint.r, tint.g, tint.b, .8f);
            for (int i = 1; i <= 5; i++)
            {
                Vector2 at = from + heading * (reach * i / 5f);
                float dot = 3 + i * 1.6f;
                GUI.DrawTexture(new Rect(at.x - dot, at.y - dot, dot * 2, dot * 2), Art.SoftCircle.texture);
            }

            GUI.color = new Color(.25f, .28f, .31f, .85f);
            GUI.DrawTexture(new Rect(anchor.x - 62, anchor.y + 74, 124, 12), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.DrawTexture(new Rect(anchor.x - 62, anchor.y + 74, 124 * power, 12), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private Vector2 menuScroll, settingsScroll;

        /// <summary>
        /// The home screen: what the player sees before a run and comes back to after one.
        /// </summary>
        /// <remarks>
        /// Laid out the way mobile survivor games lay it out, because that is what this is and
        /// players arrive already knowing where things go: money along the top where it stays in
        /// view, what you are taking into the fight in the middle, one large start button, and the
        /// rest of the game behind a tab bar down where the thumbs already are.
        ///
        /// What this replaced was a single scrolling column that was also the pause screen and the
        /// results screen. Those are three different moments and they have three screens now.
        ///
        /// The tab bar is the growth plan. Shop and settings are what there is to put in it today;
        /// another section is one enum case and one method away, and the bar re-spaces itself.
        /// </remarks>
        private void DrawHome(float width)
        {
            Panel(new Rect(0,0,width,720),ink);
            Panel(new Rect(0,0,width,76),new Color32(11,20,28,255));
            GUI.Label(new Rect(28,17,320,42),Texts.Wallet(profile.coins),heading);
            GUI.Label(new Rect(width/2-200,17,400,42),Texts.GameTitle,homeTitle);
            GUI.Label(new Rect(width-200,26,172,30),Texts.Version,small);

            switch (homeTab)
            {
                case HomeTab.Shop: DrawShopTab(width); break;
                case HomeTab.Settings: DrawSettingsTab(width); break;
                default: DrawPlayTab(width); break;
            }
            DrawTabBar(width);
        }

        /// <summary>The tab that starts a run: who is going, carrying what, and the button.</summary>
        private void DrawPlayTab(float width)
        {
            float contentWidth = Mathf.Min(940, width - 56);
            float left = (width - contentWidth) / 2;

            // The pair about to be taken into the fight, drawn at the size of a decision rather
            // than as two words in a list.
            Panel(new Rect(left,96,300,500),new Color32(24,41,49,255));
            GUI.DrawTexture(new Rect(left+60,116,180,180),Art.Creature(Look.Player).texture,ScaleMode.ScaleToFit,true);
            GUI.DrawTexture(new Rect(left+95,312,110,110),Art.Creature(LookOf(petChoice)).texture,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(left+16,442,268,36),Texts.Loadout(weapon,petChoice),homeTitle);
            GUI.Label(new Rect(left+16,486,268,64),Texts.StartHeadline,coaching);

            float right = left + 328, rightWidth = contentWidth - 328;
            DrawWeaponRow(right,100,rightWidth);
            DrawPetRow(right,222,rightWidth);
            if (GUI.Button(new Rect(right,368,rightWidth,86),Texts.Play,bigButton)) StartRun();

            // What a run is, in the two lines it takes. The coach teaches the controls in the
            // fight now, so this no longer has to.
            GUILayout.BeginArea(new Rect(right,474,rightWidth,122));
            GUILayout.Label(Texts.StartBlurb,body);
            GUILayout.EndArea();
        }

        /// <summary>The three weapons as one row. The chosen one is the lit one.</summary>
        private void DrawWeaponRow(float x, float y, float w)
        {
            GUI.Label(new Rect(x,y,w,28),Texts.WeaponPickerLabel,small);
            var weapons = new[] { WeaponId.Punch, WeaponId.Arrow, WeaponId.Laser };
            float gap = 12, cell = (w - gap * (weapons.Length - 1)) / weapons.Length;
            for (int i = 0; i < weapons.Length; i++)
            {
                bool chosen = weapon == weapons[i];
                Color previous = GUI.color;
                GUI.color = chosen ? mint : new Color(1,1,1,.55f);
                if (GUI.Button(new Rect(x + i * (cell + gap),y + 32,cell,58),
                    (chosen ? "> " : "") + Texts.Weapon(weapons[i]),button)) weapon = weapons[i];
                GUI.color = previous;
            }
        }

        /// <summary>
        /// The three buddies. A locked one shows its price and opens the shop, which is the only
        /// useful thing a locked button can do.
        /// </summary>
        private void DrawPetRow(float x, float y, float w)
        {
            GUI.Label(new Rect(x,y,w,28),Texts.PetPickerLabel(petChoice),small);
            var pets = new[] { PetId.Mochi, PetId.Bori, PetId.Coco };
            float gap = 12, cell = (w - gap * (pets.Length - 1)) / pets.Length;
            for (int i = 0; i < pets.Length; i++)
            {
                bool owned = profile.IsUnlocked(pets[i]);
                bool chosen = petChoice == pets[i];
                Color previous = GUI.color;
                GUI.color = !owned ? new Color(1,1,1,.3f) : chosen ? PetColor(pets[i]) : new Color(1,1,1,.55f);
                string label = owned ? (chosen ? "> " : "") + Texts.Pet(pets[i])
                    : Texts.Pet(pets[i]) + "  " + PlayerProfile.PriceOf(pets[i],config);
                if (GUI.Button(new Rect(x + i * (cell + gap),y + 32,cell,58),label,button))
                { if (owned) petChoice = pets[i]; else homeTab = HomeTab.Shop; }
                GUI.color = previous;
            }
        }

        /// <summary>
        /// The only place coins are spent. It stays a tab of its own rather than a button beside
        /// the start button, so a mis-tap on the way into a run cannot buy anything.
        /// </summary>
        private void DrawShopTab(float width)
        {
            float left = 32;
            GUI.Label(new Rect(left,94,width-64,40),Texts.ShopHeadline,heading);
            GUI.Label(new Rect(left,138,width-64,32),
                Texts.ShopWallet(profile.coins, profile.runsFinished),body);
            GUI.Label(new Rect(left,174,width-64,28),Texts.ShopHint,small);

            var pets = new[] { PetId.Mochi, PetId.Bori, PetId.Coco };
            float gap = 16, cardWidth = (width - 64 - gap * 2) / 3;
            for (int i = 0; i < pets.Length; i++)
            {
                PetId id = pets[i];
                bool owned = profile.IsUnlocked(id);
                int price = PlayerProfile.PriceOf(id, config);
                float x = left + i * (cardWidth + gap);
                Panel(new Rect(x,208,cardWidth,310),new Color32(34,57,65,255));
                GUI.DrawTexture(new Rect(x+cardWidth/2-50,220,100,100),Art.Creature(LookOf(id)).texture,ScaleMode.ScaleToFit,true);

                Color previous = GUI.color;
                GUI.color = PetColor(id);
                GUI.Label(new Rect(x+16,322,cardWidth-32,38),Texts.Pet(id),heading);
                GUI.color = previous;
                GUI.Label(new Rect(x+16,364,cardWidth-32,80),Texts.PetRole(id),body);

                if (owned)
                    GUI.Label(new Rect(x+16,458,cardWidth-32,46),
                        id == PlayerProfile.StarterPet ? Texts.Starter : Texts.Owned,small);
                else if (profile.coins < price)
                    GUI.Label(new Rect(x+16,458,cardWidth-32,46),
                        Texts.Short(price, price - profile.coins),small);
                else if (GUI.Button(new Rect(x+16,458,cardWidth-32,46),Texts.Unlock(price),button))
                    Buy(id);
            }
            GUI.Label(new Rect(left,536,width-64,36),
                ProfileStore.Status.Length > 0 ? ProfileStore.Status : Texts.SavePath(ProfileStore.FilePath),small);
        }

        /// <summary>
        /// Settings, and the diagnostics that have to be readable on a phone with nothing attached
        /// to it. Laid out with GUILayout because most of it is sentences: a fixed rect is what
        /// clipped the old start screen, and the two languages are not the same length.
        /// </summary>
        private void DrawSettingsTab(float width)
        {
            float contentWidth = Mathf.Min(760, width - 64);
            float left = (width - contentWidth) / 2;
            GUILayout.BeginArea(new Rect(left,94,contentWidth,528));
            settingsScroll = GUILayout.BeginScrollView(settingsScroll, false, false);

            if (!koreanUnavailable &&
                GUILayout.Button(Texts.LanguageName(Texts.Next),menuButton,GUILayout.MinHeight(46)))
                Texts.Use(Texts.Next);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Texts.MusicSetting(gameAudio.MusicEnabled),menuButton,GUILayout.MinHeight(46))) gameAudio.ToggleMusic();
            if (GUILayout.Button(Texts.SoundSetting(gameAudio.EffectsEnabled),menuButton,GUILayout.MinHeight(46))) gameAudio.ToggleEffects();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            // Offered once anything has been learned: before that the lessons are already on their
            // way, and a button promising them would be promising what is about to happen.
            if (coach.Learned != 0 && GUILayout.Button(Texts.CoachReplay,menuButton,GUILayout.MinHeight(46)))
                ForgetCoaching();

            GUILayout.Space(20);
            GUILayout.Label(link.Configured ? Texts.Unsent(link.Pending) : Texts.ServerOff,small);
            if (link.LastSend.Length > 0) GUILayout.Label(link.LastSend,small);
            if (link.Trouble.Length > 0) GUILayout.Label(link.Trouble,small);
            GUILayout.Space(6);
            // Left empty the game behaves exactly as it did before any of this existed, so the
            // box doubles as the off switch and there is no second control to keep in step.
            GUILayout.Label(Texts.ServerAddress,small);
            GUILayout.BeginHorizontal();
            serverDraft ??= link.BaseUrl;
            serverDraft = GUILayout.TextField(serverDraft,field,GUILayout.MinHeight(44));
            if (GUILayout.Button(Texts.ServerApply,menuButton,GUILayout.MinHeight(44),GUILayout.MaxWidth(150)))
                link.UseServer(serverDraft);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label(Texts.InfoHeadline,small);
            GUILayout.Label(Texts.Build,body);
            GUILayout.Space(8);
            GUILayout.Label(Texts.StartControls,body);
            GUILayout.Space(8);
            GUILayout.Label(Texts.EnemyLegend,body);
            GUILayout.Space(8);
            GUILayout.Label(recordingError.Length > 0 ? recordingError
                : Texts.SavePath(ProfileStore.FilePath),small);
            // Drawn with the built-in font on purpose, so it survives a font that cannot draw.
            GUILayout.Label(fontReport,diagnostic);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>The bar along the bottom, under the thumbs, one entry per section.</summary>
        /// <remarks>
        /// The whole cell is the hit area but only the word is drawn: a row of framed buttons
        /// reads as more things to press, and this is meant to read as where you are. The current
        /// tab is bold as well as coloured, because on a phone in sunlight a colour difference on
        /// its own is easy to miss.
        /// </remarks>
        private void DrawTabBar(float width)
        {
            Panel(new Rect(0,636,width,84),new Color32(11,20,28,255));
            var tabs = (HomeTab[])Enum.GetValues(typeof(HomeTab));
            float cell = width / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = homeTab == tabs[i];
                var box = new Rect(i * cell,636,cell,84);
                if (on) Panel(new Rect(box.x,636,cell,4),mint);
                if (GUI.Button(box,GUIContent.none,GUIStyle.none)) homeTab = tabs[i];
                GUI.Label(box,Texts.TabName(tabs[i]),on ? tabButtonOn : tabButton);
            }
        }

        /// <summary>
        /// The pause screen. Its job is to show what the run has turned into -- weapon, buddy,
        /// level, every upgrade taken -- because by the tenth level nobody remembers.
        /// </summary>
        private void DrawPause(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.94f));
            float contentWidth = Mathf.Min(760, width - 64);
            float left = (width - contentWidth) / 2;
            GUILayout.BeginArea(new Rect(left,100,contentWidth,600));
            menuScroll = GUILayout.BeginScrollView(menuScroll, false, false);
            GUILayout.Label(Texts.PausedHeadline,menuTitle);
            GUILayout.Space(12);
            DrawBuild();
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Texts.KeepGoing,menuButton,GUILayout.MinHeight(56))) TogglePause();
            GUILayout.Space(12);
            if (GUILayout.Button(Texts.Restart,menuButton,GUILayout.MinHeight(56))) StartRun();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(recordingError.Length > 0 ? recordingError : Texts.RunLog(recordingPath),small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// The end of a run: how it went, what it paid, and the two things worth doing next.
        /// </summary>
        /// <remarks>
        /// Going home is the one that leads somewhere. The weapon and the buddy are chosen there
        /// and the coins just banked are spent there, so a finished run can change the next one.
        /// Trying again keeps the same pair on purpose, for the player who only wanted another go
        /// at the same fight.
        /// </remarks>
        private void DrawResult(float width)
        {
            Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.94f));
            float contentWidth = Mathf.Min(760, width - 64);
            float left = (width - contentWidth) / 2;
            GUILayout.BeginArea(new Rect(left,100,contentWidth,600));
            menuScroll = GUILayout.BeginScrollView(menuScroll, false, false);
            GUILayout.Label(world.BossDefeated ? Texts.BossDownHeadline :
                world.State == RunState.Won ? Texts.WonHeadline : Texts.LostHeadline,menuTitle);
            GUILayout.Space(12);
            GUILayout.Label(Texts.RunResult(FormatTime(world.Time),world.Kills,world.Wave) + "\n" +
                Texts.CoinsEarned(lastRunCoins,world.BossDefeated,profile.coins),menuHeading);
            GUILayout.Space(14);
            DrawBuild();
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Texts.TryAgain,menuButton,GUILayout.MinHeight(56))) StartRun();
            GUILayout.Space(12);
            if (GUILayout.Button(Texts.ToHome,menuButton,GUILayout.MinHeight(56)))
            { started = false; homeTab = HomeTab.Home; }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
            // The run's condition belongs with the build: it is the other half of "why is this
            // run going the way it is", and it was chosen for the player rather than by them.
            GUILayout.Label(Texts.TwistTitle(world.Twist) + "  /  " + Texts.TwistDescription(world.Twist),small);
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
