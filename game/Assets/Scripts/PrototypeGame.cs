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
        private Vec2 move, aim, queuedAim;
        private bool queuedPunch, paused, started;
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
            CreateArena();
            player = CreateCreature("You", paper, 0.9f, 10, true).transform;
            pet = CreateCreature("Mochi", mint, 0.55f, 11, true).transform;
            world = new CombatWorld(config, 42);
        }

        private void StartRun()
        {
            EndRecording(true);
            foreach (var view in views.Values) Destroy(view);
            views.Clear();
            foreach (var view in transient) Destroy(view);
            transient.Clear(); transientEnds.Clear();
            world = new CombatWorld(config, unchecked(Environment.TickCount));
            started = true; paused = false; accumulator = 0; hurtFlash = 0;
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
            if (started && !paused && world.State == RunState.Playing)
            {
                ReadInput();
                accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                while (accumulator >= CombatWorld.StepSeconds)
                {
                    world.Step(new PlayerInput(move, queuedPunch ? queuedAim : aim, queuedPunch));
                    queuedPunch = false;
                    RecordEvents();
                    foreach (CombatEvent e in world.Events) ShowEvent(e);
                    accumulator -= CombatWorld.StepSeconds;
                    if (world.State != RunState.Playing) { EndRecording(false); ClearInput(); break; }
                }
            }
            player.position = new Vector3(world.Position.x, world.Position.y, 0);
            float orbit = world.Time * 2;
            pet.position = player.position + new Vector3(Mathf.Cos(orbit) * 1.1f, Mathf.Sin(orbit) * 0.75f, 0);
            player.GetComponent<SpriteRenderer>().color = Time.unscaledTime < hurtFlash ? coral : paper;
            SynchronizeEnemies();
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
                        if (ended) { queuedPunch = touch.phase == TouchPhase.Ended; queuedAim = aim; attackFinger = -1; }
                    }
                }
                if (!moveSeen) moveFinger = -1;
                if (!attackSeen) attackFinger = -1;
                return;
            }
            moveFinger = attackFinger = -1;
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            move = new Vec2(x, y);
            if (Input.mousePosition.y < Screen.height * .84f)
            {
                Vector3 point = gameCamera.ScreenToWorldPoint(Input.mousePosition);
                aim = new Vec2(point.x - world.Position.x, point.y - world.Position.y);
                if (Input.GetMouseButtonDown(0)) { queuedPunch = true; queuedAim = aim; }
            }
            if (Input.GetKeyDown(KeyCode.Space)) { aim = new Vec2(); queuedAim = aim; queuedPunch = true; }
        }

        private void ClearInput()
        {
            moveFinger = attackFinger = -1;
            move = aim = queuedAim = new Vec2(); queuedPunch = false; accumulator = 0;
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
                    view = CreateCreature(enemy.Kind.ToString(), enemy.Kind == EnemyKind.Runner ?
                        new Color32(255, 203, 115,255) : coral, enemy.Kind == EnemyKind.Runner ? .62f : .9f, 5, false);
                    views.Add(enemy.Id, view);
                }
                view.transform.position = new Vector3(enemy.Position.x,enemy.Position.y,0);
            }
        }
        private void ShowEvent(CombatEvent e)
        {
            if (e.type == "hurt") hurtFlash = Time.unscaledTime + .15f;
            if (e.type != "attack") return;
            GameObject fx;
            if (e.source == "punch")
            {
                fx = new GameObject("Punch");
                fx.transform.SetParent(transform);
                fx.transform.position = player.position + new Vector3(e.x,e.y,0) * config.punchRange * .6f;
                fx.transform.localScale = Vector3.one * config.punchRange;
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
            GUI.Label(new Rect(28,87,320,32),"MOCHI  /  AUTO SUPPORT",small);
            if (started)
            {
                Panel(new Rect(28,48,220,9),new Color(.3f,.35f,.38f));
                Panel(new Rect(28,48,220 * world.Health / config.playerHealth,9),mint);
                GUI.Label(new Rect(width-380,22,240,35),"WAVE " + world.Wave + "   /   " + world.Kills + " KOs",body);
                if (world.State == RunState.Playing && GUI.Button(new Rect(width-110,15,88,43),paused ? "PLAY" : "II",button)) TogglePause();
                GUI.Label(new Rect(28,666,400,30),"MOVE  /  WASD or left thumb",small);
                GUI.Label(new Rect(width-410,666,390,30),"PUNCH  /  tap + aim or SPACE",small);
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
            if (!started || paused || world.State != RunState.Playing)
            {
                Panel(new Rect(0,76,width,644),new Color(ink.r,ink.g,ink.b,.92f));
                float left = width/2-340;
                GUI.Label(new Rect(left,158,700,90),!started ? "PET THEM!" :
                    paused && world.State == RunState.Playing ? "TAKE A BREATHER." :
                    world.State == RunState.Won ? "NICE PETTING." : "ONE MORE PAT?",title);
                GUI.Label(new Rect(left,265,680,90),!started ?
                    "Your hands. Their problem.\nSurvive 3 minutes with your pet Mochi." :
                    "Time " + FormatTime(world.Time) + "   /   " + world.Kills + " KOs   /   Wave " + world.Wave,heading);
                GUI.Label(new Rect(left,370,680,90),!started ?
                    "Move with your left thumb. Tap the right side to punch.\nDrag before releasing to aim. Mochi attacks automatically.\nDesktop: WASD + mouse click. SPACE auto-targets." :
                    "Mint buddy = automatic support. Coral = chaser. Gold = runner.\nPunch to make an escape route; keep moving.",body);
                string label = !started ? "LET'S PLAY  >" : paused && world.State == RunState.Playing ? "KEEP GOING  >" : "TRY AGAIN  >";
                if (GUI.Button(new Rect(left,490,340,62),label,button))
                { if (started && paused && world.State == RunState.Playing) TogglePause(); else StartRun(); }
                if (started && paused && world.State == RunState.Playing &&
                    GUI.Button(new Rect(left+360,490,220,62),"RESTART",button)) StartRun();
                GUI.Label(new Rect(left,575,680,70),recordingError.Length > 0 ? recordingError :
                    started ? "Run log: " + recordingPath : "PROTOTYPE 0.1  /  TOUCH COMBAT LAB",small);
            }
        }
        private static string FormatTime(float time) => ((int)time / 60).ToString("00") + ":" + ((int)time % 60).ToString("00");
    }
}
