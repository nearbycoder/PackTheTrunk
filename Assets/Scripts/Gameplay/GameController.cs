using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PackTheTrunk
{
    /// <summary>Owns the game loop: level setup, picking, rotating, placing, undo and the end-of-level drive-off.</summary>
    public partial class GameController : MonoBehaviour
    {
        enum Mode { Title, Menu, Story, Playing, Closing, Results }

        struct SavedItem
        {
            public PackItem Item;
            public bool Packed;
            public Vector3Int Pos;
            public Quaternion Orientation;
        }

        static readonly Color Sky = new Color(0.58f, 0.79f, 0.95f);
        // Placement ghost per palette (Settings → Gameplay): green / red, or blue / orange for
        // red-green colour blindness. "Won't fit" is also hatched, so it never relies on colour alone.
        static readonly Color[] GhostOk = { new Color(0.35f, 1f, 0.55f, 0.38f), new Color(0.3f, 0.62f, 1f, 0.42f) };
        static readonly Color[] GhostBad = { new Color(1f, 0.3f, 0.3f, 0.42f), new Color(1f, 0.58f, 0.12f, 0.45f) };

        Camera cam;
        CameraRig rig;
        GameUI ui;
        Sfx sfx;
        MusicDirector music;
        Atmosphere atmosphere;
        bool paused;
        int previewIndex = -1;
        bool creditsAfterAlbum;

        Mode mode;
        int levelIndex;
        LevelDef level;
        Transform levelRoot;
        Vehicle vehicle;
        TrunkGrid grid;
        readonly List<PackItem> items = new List<PackItem>();

        PackItem held;
        PackItem hovered;
        bool heldFromTrunk;
        Vector3Int heldOrigin;
        Quaternion heldOriginOrientation;
        List<SavedItem> pendingSnapshot;
        readonly Stack<List<SavedItem>> undo = new Stack<List<SavedItem>>();

        Transform ghost;
        MeshFilter ghostFilter;
        MeshRenderer ghostRenderer;
        VoxelShape ghostShape;

        bool hasTarget;
        bool targetValid;
        Vector3Int targetPos;
        readonly List<int> restingHeights = new List<int>();
        Vector2Int lastColumn;
        int heightBias;
        float scrollCooldown;

        /// <summary>Running a self-test, benchmark or recording rather than being played.</summary>
        public static readonly bool Automated = System.Environment.GetCommandLineArgs()
            .Any(a => a == "-pttAutopilot" || a == "-pttShowcase" || a == "-pttBench");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameController>() != null) return;
            new GameObject("Pack The Trunk").AddComponent<GameController>();
        }

        void Awake()
        {
            var bootWatch = System.Diagnostics.Stopwatch.StartNew();
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 300f;
            rig = cam.gameObject.AddComponent<CameraRig>();
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<MasterBus>();
            gameObject.AddComponent<GamepadCursor>();

            atmosphere = Atmosphere.Apply(cam, rig);
            GameSettings.Init(cam, atmosphere);
            BuildEnvironment();
            long tEnv = bootWatch.ElapsedMilliseconds;

            sfx = gameObject.AddComponent<Sfx>();
            music = gameObject.AddComponent<MusicDirector>();
            long tAudio = bootWatch.ElapsedMilliseconds;
            ui = gameObject.AddComponent<GameUI>();
            long tUi = bootWatch.ElapsedMilliseconds;
            ui.SealedFor = Sealed;
            music.TrackStarted += (title, artist) => ui.ShowNowPlaying(title, artist);
            ui.TitleKeyPressed += () => sfx.Confirm();
            ui.ContinuePressed += () => ui.Transition(() => BeginTrip(NextTripIndex()));
            ui.TripMapPressed += ShowMenu;
            ui.AlbumPressed += () => ShowAlbum(false);
            ui.CreditsPressed += ShowCredits;
            ui.CreditsFinished += () =>
            {
                if (creditsAfterAlbum) { creditsAfterAlbum = false; ui.Transition(() => ShowTitle(false)); }
                else if (mode == Mode.Title) music.Play(MusicDirector.MenuTrack);
            };
            ui.TitleBackPressed += () => ShowTitle(false);
            ui.LevelChosen += i => ui.Transition(() => BeginTrip(i));
            ui.StartPressed += () =>
            {
                music.SetMuffled(false);
                if (showingEnding) ui.Transition(() => ShowAlbum(true));
                else Play();
            };
            ui.UndoPressed += () => { if (mode == Mode.Playing) Undo(); };
            ui.HintPressed += AskGrandpa;
            // While packing, RESTART unpacks in place as one undo step; on the postcard the car has gone, so rebuild.
            ui.RestartPressed += () =>
            {
                if (mode == Mode.Playing) UnpackEverything();
                else ui.Transition(() => StartLevel(levelIndex));
            };
            ui.MenuPressed += () => ui.Transition(ShowMenu);
            ui.ClosePressed += () => { if (mode == Mode.Playing && CanClose()) StartCoroutine(CloseTrunk()); };
            ui.NextPressed += () => ui.Transition(() =>
            {
                if (levelIndex + 1 < GameDatabase.Levels.Count) BeginTrip(levelIndex + 1);
                else ShowEnding();
            });
            ui.PutBackPressed += PutBack;
            ui.ItemRowClicked += OnRowClicked;
            ui.QuitPressed += Quit;
            ui.AlbumClosed += () =>
            {
                if (albumIsFinale)
                {
                    creditsAfterAlbum = true;
                    ShowCredits();
                }
                else ShowTitle(false);
            };
            ui.PausePressed += PauseGame;
            ui.ResumePressed += ResumeGame;
            ui.PauseRestartPressed += () => { ResumeGame(); UnpackEverything(); };
            ui.PauseMapPressed += () => { ResumeGame(); ui.Transition(ShowMenu); };
            ui.PauseMainMenuPressed += () => { ResumeGame(); ui.Transition(() => ShowTitle(false)); };
            ui.ResetProgressPressed += ResetProgress;

            var ghostGo = new GameObject("Ghost", typeof(MeshFilter), typeof(MeshRenderer));
            ghost = ghostGo.transform;
            ghostFilter = ghostGo.GetComponent<MeshFilter>();
            ghostRenderer = ghostGo.GetComponent<MeshRenderer>();
            ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ghostGo.SetActive(false);

            // Step away mid-trip and the game waits for you.
            Application.focusChanged += focused =>
            {
                if (!focused && !Automated && mode == Mode.Playing && !paused) PauseGame();
            };

            ShowTitle(true);
            // On its own host: level changes stop this controller's coroutines.
            new GameObject("Album Loader").AddComponent<CoroutineHost>().StartCoroutine(WarmAlbum());
            Debug.Log($"[Perf] boot {bootWatch.ElapsedMilliseconds} ms (environment {tEnv}, audio {tAudio - tEnv}, ui {tUi - tAudio}, title {bootWatch.ElapsedMilliseconds - tUi})");
        }

        // ------------------------------------------------------------------ Setup

        void BuildEnvironment()
        {
            var env = new GameObject("Environment").transform;
            float g = Vehicle.GroundY;

            EnvBox(env, "Grass", new Vector3(-200, g - 0.2f, -200), new Vector3(200, g - 0.02f, 200), new Color(0.47f, 0.68f, 0.36f));
            EnvBox(env, "Driveway", new Vector3(-14, g - 0.02f, -16), new Vector3(26, g, 40), new Color(0.36f, 0.37f, 0.40f));
            EnvBox(env, "Curb", new Vector3(-14.4f, g - 0.02f, -16), new Vector3(-14, g + 0.15f, 40), new Color(0.75f, 0.74f, 0.7f));
            EnvBox(env, "Curb R", new Vector3(26, g - 0.02f, -16), new Vector3(26.4f, g + 0.15f, 40), new Color(0.75f, 0.74f, 0.7f));
            for (int i = 0; i < 6; i++)
                EnvBox(env, "Stripe", new Vector3(-2.8f, g, -12 + i * 9f), new Vector3(-2.6f, g + 0.01f, -8 + i * 9f), new Color(0.95f, 0.9f, 0.6f));

            var rng = new System.Random(42);
            bool models = ModelLibrary.Load("Props/tree_round") != null;
            var trunkMat = MaterialLibrary.Lit(new Color(0.45f, 0.3f, 0.2f), 0.1f);
            Color[] leaves = { new Color(0.27f, 0.58f, 0.3f), new Color(0.35f, 0.66f, 0.32f), new Color(0.22f, 0.5f, 0.32f) };

            void Tree(Vector3 pos, float scale)
            {
                if (models)
                {
                    Prop(env, rng.Next(3) == 0 ? "tree_pine" : "tree_round", pos, (float)rng.NextDouble() * 360f, scale);
                    return;
                }
                EnvBox(env, "Tree Trunk", pos + new Vector3(-0.3f, 0, -0.3f) * scale, pos + new Vector3(0.3f, 2.4f, 0.3f) * scale, trunkMat);
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(canopy.GetComponent<Collider>());
                canopy.transform.SetParent(env, false);
                canopy.transform.position = pos + Vector3.up * 3.4f * scale;
                canopy.transform.localScale = Vector3.one * (2.8f * scale);
                canopy.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Lit(leaves[rng.Next(leaves.Length)], 0.15f);
            }

            for (int i = 0; i < 46; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = 34f + (float)rng.NextDouble() * 40f;
                var pos = new Vector3(6f + Mathf.Cos(angle) * radius, g, 12f + Mathf.Sin(angle) * radius);
                if (pos.z < -20f || (pos.z > 36f && Mathf.Abs(pos.x - 6f) < 12f)) continue;
                Tree(pos, 0.8f + (float)rng.NextDouble() * 1.2f);
            }
            if (!models) return;

            // A street scene along the driveway.
            for (float z = -10f; z < 40f; z += 9f + (float)rng.NextDouble() * 4f)
            {
                Tree(new Vector3(-21f + (float)rng.NextDouble() * 2f, g, z), 1.1f + (float)rng.NextDouble() * 0.5f);
                Tree(new Vector3(32f + (float)rng.NextDouble() * 2f, g, z + 4f), 1.1f + (float)rng.NextDouble() * 0.5f);
            }
            for (float z = -12f; z < 38f; z += 3.2f)
            {
                Prop(env, "bush", new Vector3(-16f, g, z), (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.5f);
                Prop(env, "bush", new Vector3(28f, g, z + 1.6f), (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.5f);
            }
            for (float z = -12f; z < 40f; z += 4f)
            {
                Prop(env, "fence", new Vector3(-18f, g, z), 90f, 1f);
                Prop(env, "fence", new Vector3(30f, g, z), 90f, 1f);
            }
            Prop(env, "house", new Vector3(6f, g, 44f), 0f, 1.4f);
            Prop(env, "mailbox", new Vector3(-15.2f, g, -6f), 90f, 1f);
            Prop(env, "street_lamp", new Vector3(-15.2f, g, 12f), 90f, 1f);
            Prop(env, "street_lamp", new Vector3(27.2f, g, 26f), -90f, 1f);
        }

        static void Prop(Transform parent, string name, Vector3 position, float yaw, float scale)
        {
            var go = ModelLibrary.Instantiate("Props/" + name, parent);
            if (go == null) return;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
        }

        static void EnvBox(Transform parent, string name, Vector3 min, Vector3 max, Color color) =>
            EnvBox(parent, name, min, max, MaterialLibrary.Textured(color, 0.22f));

        static void EnvBox(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ Levels

        int StarsFor(int index) => Prefs.GetInt("ptt.stars." + GameDatabase.Levels[index].Id, 0);

        bool Unlocked(int index) => index == 0 || StarsFor(index - 1) > 0;

        /// <summary>The next trip to play: the first unlocked one without stars (or the finale).</summary>
        int NextTripIndex()
        {
            for (int i = 0; i < GameDatabase.Levels.Count; i++)
                if (Unlocked(i) && StarsFor(i) == 0) return i;
            return GameDatabase.Levels.Count - 1;
        }

        /// <summary>Park the next trip's car in the driveway behind the menus, with a slow camera drift.</summary>
        void ShowPreview()
        {
            StopAllCoroutines();
            ResetPause();
            int preview = NextTripIndex();
            if (preview != previewIndex || levelRoot == null || mode == Mode.Playing || mode == Mode.Closing || mode == Mode.Results || mode == Mode.Story)
            {
                BuildLevel(preview, true);
                previewIndex = preview;
            }
            rig.Attract = true;
            rig.InputEnabled = false;
            atmosphere.SetBlur(0f);
            sfx.SetAmbience(1f, false);
            showingEnding = false;
            music.Play(MusicDirector.MenuTrack);
            music.SetMuffled(false);
        }

        void ShowTitle(bool waitForKey)
        {
            ShowPreview();
            mode = Mode.Title;
            int count = GameDatabase.Levels.Count;
            int done = 0, stars = 0;
            for (int i = 0; i < count; i++)
            {
                int s = StarsFor(i);
                if (s > 0) done++;
                stars += s;
            }
            int next = NextTripIndex();
            var nl = GameDatabase.Levels[next];
            string cont = done == 0 ? $"Begin the story  ·  {nl.Title}"
                : done == count ? "Every trip is packed  ·  play the last one again"
                : $"Trip {next + 1}  ·  {nl.Title}  ·  Chapter {nl.Chapter.Numeral}";
            string map = $"{done} of {count} trips packed  ·  {stars} of {count * 3} stars";
            string album = done == 0 ? "Empty for now" : $"{done} photo{(done == 1 ? "" : "s")} so far";
            ui.ShowTitle(waitForKey, cont, map, album);
        }

        void ShowMenu()
        {
            ShowPreview();
            mode = Mode.Menu;
            sfx.WhooshIn();
            ui.ShowMenu(GameDatabase.Levels, StarsFor, Unlocked);
        }

        void ShowCredits()
        {
            music.Play(MusicDirector.EndingTrack);
            ui.ShowCredits();
        }

        void ResetProgress()
        {
            foreach (var l in GameDatabase.Levels)
            {
                Prefs.DeleteKey("ptt.stars." + l.Id);
                Prefs.DeleteKey(SealKey(l.Id));
            }
            Prefs.Save();
            foreach (var tex in photos.Values) if (tex != null) Destroy(tex);
            photos.Clear();
            try
            {
                if (System.IO.Directory.Exists(AlbumDir)) System.IO.Directory.Delete(AlbumDir, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Album] could not clear photos: " + e.Message);
            }
            ui.HideSettings();
            if (mode == Mode.Title || mode == Mode.Menu) ui.Transition(() => ShowTitle(false));
            else ui.Toast("Progress erased.", 2f);
        }

        // ------------------------------------------------------------------ Pause

        void PauseGame()
        {
            if (mode != Mode.Playing || paused) return;
            paused = true;
            Time.timeScale = 0f;
            rig.InputEnabled = false;
            SetHovered(null);
            music.SetMuffled(true);
            sfx.SetAmbience(0.55f, true);
            atmosphere.SetBlur(1f);
            sfx.OpenPanel();
            ui.ShowPause(level);
        }

        void ResumeGame()
        {
            if (!paused) return;
            ResetPause();
            sfx.ClosePanel();
        }

        void ResetPause()
        {
            bool was = paused;
            paused = false;
            Time.timeScale = 1f;
            ui.HidePause();
            if (!was) return;
            if (mode == Mode.Playing) ui.ShowHeld(held);
            rig.InputEnabled = mode == Mode.Playing;
            music.SetMuffled(false);
            sfx.SetAmbience(1f, false);
            atmosphere.SetBlur(0f);
        }

        bool showingEnding;

        /// <summary>Park the car, then let the person you're packing for explain the trip.</summary>
        void BeginTrip(int index)
        {
            StopAllCoroutines();
            ResetPause();
            BuildLevel(index, false);
            previewIndex = -1;
            mode = Mode.Story;
            showingEnding = false;
            rig.Attract = false;
            rig.InputEnabled = false;
            // Swing in from the street while the car backs into the driveway.
            rig.SweepIn(-28f, -12f, 1.45f, 3.2f, 1.1f);
            atmosphere.SetBlur(0f);
            sfx.SetAmbience(0.75f, true);
            music.Play(level.Music);
            music.SetMuffled(true);
            arrival = StartCoroutine(Arrive());
            ui.ShowStory(level, index, GameDatabase.Levels.Count);
        }

        void ShowEnding()
        {
            ResetPause();
            mode = Mode.Story;
            showingEnding = true;
            rig.InputEnabled = false;
            atmosphere.SetBlur(0.8f);
            sfx.SetAmbience(0.6f, true);
            music.Play(MusicDirector.EndingTrack);
            music.SetMuffled(false);
            ui.ShowEnding();
        }

        void StartLevel(int index)
        {
            StopAllCoroutines();
            ResetPause();
            BuildLevel(index, false);
            previewIndex = -1;
            rig.SweepIn(-10f, -6f, 1.15f, 1.2f, 2.5f);
            Play();
        }

        void Play()
        {
            FinishArrival();
            mode = Mode.Playing;
            showingEnding = false;
            rig.Attract = false;
            rig.InputEnabled = true;
            atmosphere.SetBlur(0f);
            sfx.SetAmbience(1f, false);
            ui.ShowHud(level, items);
            RefreshHud();
            music.Play(level.Music);
            music.SetMuffled(false);
            OnTripStartTips();
            ui.SetHintAvailable(HintsAvailable);
        }

        // ------------------------------------------------------------------ Arrival

        Coroutine arrival;
        bool arriving;
        Vector3 vehicleHome;
        Quaternion vehicleHomeRotation, lidOpen, flapOpen;

        /// <summary>The car reverses into the driveway, settles on its springs and pops the trunk.</summary>
        IEnumerator Arrive()
        {
            arriving = true;
            var t = vehicle.transform;
            vehicleHome = t.localPosition;
            vehicleHomeRotation = t.localRotation;
            lidOpen = vehicle.LidPivot.localRotation;
            vehicle.LidPivot.localRotation = vehicle.LidClosed;
            if (vehicle.FlapPivot != null)
            {
                flapOpen = vehicle.FlapPivot.localRotation;
                vehicle.FlapPivot.localRotation = vehicle.FlapClosed;
            }

            const float distance = 24f, time = 2.6f;
            float last = distance;
            for (float e = 0f; e < time; e += Time.deltaTime)
            {
                float offset = distance * (1f - Ease.OutCubic(e / time));
                t.localPosition = vehicleHome + Vector3.forward * offset;
                vehicle.SpinWheels(offset - last);
                last = offset;
                yield return null;
            }
            t.localPosition = vehicleHome;
            vehicle.SpinWheels(-last);

            // Settle on the suspension.
            for (float e = 0f; e < 0.7f; e += Time.deltaTime)
            {
                float rock = 1.6f * Mathf.Exp(-6f * e) * Mathf.Sin(e * 20f);
                t.localRotation = vehicleHomeRotation * Quaternion.Euler(rock, 0f, 0f);
                yield return null;
            }
            t.localRotation = vehicleHomeRotation;
            yield return new WaitForSeconds(0.15f);

            sfx.LidOpen();
            for (float e = 0f; e < 0.6f; e += Time.deltaTime)
            {
                float k = Ease.OutBack(e / 0.6f, 1.4f);
                vehicle.LidPivot.localRotation = Quaternion.SlerpUnclamped(vehicle.LidClosed, lidOpen, k);
                if (vehicle.FlapPivot != null) vehicle.FlapPivot.localRotation = Quaternion.SlerpUnclamped(vehicle.FlapClosed, flapOpen, k);
                yield return null;
            }
            FinishArrival();
        }

        void FinishArrival()
        {
            if (!arriving) return;
            if (arrival != null) StopCoroutine(arrival);
            arrival = null;
            arriving = false;
            if (vehicle == null) return;
            vehicle.transform.localPosition = vehicleHome;
            vehicle.transform.localRotation = vehicleHomeRotation;
            vehicle.LidPivot.localRotation = lidOpen;
            if (vehicle.FlapPivot != null) vehicle.FlapPivot.localRotation = flapOpen;
        }

        void BuildLevel(int index, bool menuPreview)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            arriving = false;
            arrival = null;
            levelIndex = index;
            level = GameDatabase.Levels[index];
            ClearTips();
            ClearHint();
            ClearSeeThrough();
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            items.Clear();
            undo.Clear();
            starsAnnounced = 0;
            hintedThisTry = false;
            restartsAfterHint.Clear();
            held = null;
            hovered = null;
            pendingSnapshot = null;
            ghost.gameObject.SetActive(false);

            levelRoot = new GameObject("Level " + level.Title).transform;
            vehicle = Vehicle.Build(level, levelRoot);
            long tVehicle = watch.ElapsedMilliseconds;
            grid = new TrunkGrid(level.Size, level.Blocked);

            foreach (var def in level.Required) items.Add(PackItem.Create(def, false, levelRoot));
            foreach (var def in level.Bonus) items.Add(PackItem.Create(def, true, levelRoot));
            long tItems = watch.ElapsedMilliseconds;

            var pileBounds = LayoutPile(!menuPreview);
            long tPile = watch.ElapsedMilliseconds;
            var bounds = new Bounds(new Vector3(level.Size.x * 0.5f, 0f, level.Size.z * 0.5f), Vector3.zero);
            bounds.Encapsulate(new Vector3(-Vehicle.Wall - 1.2f, Vehicle.GroundY, -1.6f));
            bounds.Encapsulate(new Vector3(level.Size.x + Vehicle.Wall + 1.2f, level.Size.y, level.Size.z + 2f));
            bounds.Encapsulate(pileBounds);
            bounds.center = new Vector3(bounds.center.x, 0f, bounds.center.z);
            if (menuPreview) rig.Frame(bounds, true, 0.56f, 0.44f, -34f, 34f);
            else rig.Frame(bounds, true, 0.74f);
            if (watch.ElapsedMilliseconds > 20) Debug.Log($"[Perf] built {level.Id} in {watch.ElapsedMilliseconds} ms (car {tVehicle}, items {tItems - tVehicle}, pile {tPile - tItems})");
        }

        Bounds LayoutPile(bool sounds)
        {
            float startX = level.Size.x + Vehicle.Wall + 2.0f;
            float rowLimit = Mathf.Max(8f, items.Max(i => i.Shape.Size.x) + 1f);
            float x = startX, z = -1.2f, rowDepth = 0f;
            const float gap = 0.8f;

            var rng = new System.Random(level.Index * 31 + 7);
            int order = 0;
            foreach (var item in items)
            {
                var size = item.Shape.Size;
                if (x > startX && x + size.x > startX + rowLimit)
                {
                    x = startX;
                    z += rowDepth + gap;
                    rowDepth = 0f;
                }
                item.PileLocalPosition = new Vector3(x, Vehicle.GroundY, z);
                item.State = ItemState.Pile;
                var captured = item;
                float delay = 0.35f + order++ * 0.075f + (float)rng.NextDouble() * 0.05f;
                item.FallTo(item.PileLocalPosition, delay, 9f + (float)rng.NextDouble() * 3f, () =>
                {
                    if (!sounds) return;
                    var at = levelRoot.TransformPoint(captured.PileLocalPosition + captured.Shape.Center);
                    sfx.PileThump(at, Mathf.InverseLerp(1, 14, captured.Def.Volume));
                });
                x += size.x + gap;
                rowDepth = Mathf.Max(rowDepth, size.z);
            }

            var min = new Vector3(startX - 0.6f, Vehicle.GroundY, -1.8f);
            var max = new Vector3(startX + rowLimit + 0.2f, Vehicle.GroundY + 0.02f, z + rowDepth + 0.6f);
            // Red picnic plaid by the lake, sunny yellow for the party, green for camping, and so on.
            int[] plaidByTrip = { 0, 3, 2, 1, 4, 1, 0, 4 };
            PicnicBlanket.Build(levelRoot, min, max, plaidByTrip[level.Index % plaidByTrip.Length]);

            var b = new Bounds((min + max) * 0.5f, max - min);
            b.Encapsulate(max + Vector3.up * 2f);
            return b;
        }

        // ------------------------------------------------------------------ Frame loop

        void Update()
        {
            if (ui.InTransition) return;
            ui.UpdateFragileTags(mode == Mode.Playing && !paused);
            var kb = Keyboard.current;
            if (Bindings.Pressed(Bindings.Action.Music) && !ui.IsTitleWaiting && !ui.IsRebinding)
            {
                music.ToggleMute();
                ui.Toast(music.Muted ? "Music off" : "Music on", 1.2f);
            }
            if (((kb != null && kb.escapeKey.wasPressedThisFrame) || Pad.Back) && !ui.OverlayOpen && !ui.EscConsumed) OnEscape();
            if (Pad.Start && mode == Mode.Playing && !ui.OverlayOpen)
            {
                if (paused) ResumeGame();
                else PauseGame();
            }
            if (kb != null || Pad.Current != null)
            {
                bool go = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || Pad.Start;
                if (mode == Mode.Story && go && ui.StoryReady) ui.PressStart();
                else if (mode == Mode.Results && ui.ResultsReady)
                {
                    if (go) ui.PressNext();
                    else if ((kb != null && kb.rKey.wasPressedThisFrame) || Pad.Down(p => p.buttonWest)) ui.PressRetry();
                }
            }
            if (mode != Mode.Playing || paused) return;
            UpdateTips();
            UpdateHint();
            UpdateSeeThrough();

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            bool overUi = ui.PointerOverUi;
            rig.ZoomEnabled = held == null;
            scrollCooldown -= Time.deltaTime;

            // Gamepad: View undo, D-pad left hint, X turn / Y tip / RB roll (LB reverses), D-pad up/down shelf,
            // D-pad right close. Pointing and A-to-click go through GamepadCursor's virtual mouse.
            if (Pad.Current != null)
            {
                if (Pad.Down(p => p.selectButton)) Undo();
                if (Pad.Down(p => p.dpad.left)) AskGrandpa();
                if (held != null)
                {
                    bool reverse = Pad.Held(p => p.leftShoulder);
                    if (Pad.Down(p => p.buttonWest)) Rotate(Vector3.up, reverse);
                    if (Pad.Down(p => p.buttonNorth)) Rotate(rig.SnappedRight(), reverse);
                    if (Pad.Down(p => p.rightShoulder)) Rotate(rig.SnappedForward(), reverse);
                    if (Pad.Down(p => p.dpad.up)) { heightBias++; tipShelfPicked = true; }
                    if (Pad.Down(p => p.dpad.down)) { heightBias--; tipShelfPicked = true; }
                }
                if (Pad.Down(p => p.dpad.right) && CanClose() && ConfirmKeyClose())
                {
                    StartCoroutine(CloseTrunk());
                    return;
                }
            }

            if (keyboard != null)
            {
                if (Bindings.Pressed(Bindings.Action.Undo)) Undo();
                if (Bindings.Pressed(Bindings.Action.Hint)) AskGrandpa();
                if (held != null)
                {
                    bool shift = keyboard.shiftKey.isPressed;
                    if (Bindings.Pressed(Bindings.Action.Turn)) Rotate(Vector3.up, shift);
                    if (Bindings.Pressed(Bindings.Action.Tip)) Rotate(rig.SnappedRight(), shift);
                    if (Bindings.Pressed(Bindings.Action.Roll)) Rotate(rig.SnappedForward(), shift);
                    if (Bindings.Pressed(Bindings.Action.ShelfUp)) { heightBias++; tipShelfPicked = true; }
                    if (Bindings.Pressed(Bindings.Action.ShelfDown)) { heightBias--; tipShelfPicked = true; }
                }
                if (Bindings.Pressed(Bindings.Action.Close) && CanClose() && ConfirmKeyClose())
                {
                    StartCoroutine(CloseTrunk());
                    return;
                }
            }

            if (mouse == null) return;
            // Items glide every frame; make sure their colliders are where they are drawn.
            Physics.SyncTransforms();
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (held == null)
            {
                PackItem hit = null;
                if (!overUi && AimRaycast(ray, out var info))
                {
                    hit = info.collider.GetComponentInParent<PackItem>();
                    if (hit != null && hit.State != ItemState.Pile && hit.State != ItemState.Packed) hit = null;
                }
                SetHovered(hit);
                if (hit != null && mouse.leftButton.wasPressedThisFrame) TryPickUp(hit);
            }
            else
            {
                if (rig.RightClickReleased() && !overUi) Rotate(Vector3.up, false);

                float scroll = mouse.scroll.ReadValue().y;
                if (!overUi && Mathf.Abs(scroll) > 0.01f && scrollCooldown <= 0f)
                {
                    heightBias += scroll > 0 ? 1 : -1;
                    scrollCooldown = 0.08f;
                    tipShelfPicked = true;
                }

                UpdateTarget(ray, overUi);
                UpdateHeldVisuals(ray);

                if (mouse.leftButton.wasPressedThisFrame && !overUi)
                {
                    if (hasTarget && targetValid) PlaceHeld();
                    else if (hasTarget)
                    {
                        sfx.Error();
                        ui.Toast(ExplainProblem(held.Shape, targetPos.x, targetPos.z));
                        held.Squash(0.4f);
                        QueueTip(Tip.Turn);
                    }
                    else PutBack();
                }
            }
        }

        void OnEscape()
        {
            if (ui.IsAlbumOpen)
            {
                if (!albumIsFinale) { sfx.Back(); ShowTitle(false); }
                return;
            }
            switch (mode)
            {
                case Mode.Playing:
                    if (paused) ResumeGame();
                    else if (held != null) PutBack();
                    else PauseGame();
                    break;
                case Mode.Menu:
                    sfx.Back();
                    ShowTitle(false);
                    break;
                case Mode.Results:
                    if (ui.ResultsReady) ui.PressMap();
                    break;
                case Mode.Story:
                    if (!showingEnding) { sfx.Back(); ui.Transition(ShowMenu); }
                    break;
                case Mode.Title:
                    if (!ui.IsTitleWaiting) ui.Confirm("Leave the driveway?", "QUIT", Quit);
                    break;
            }
        }

        void SetHovered(PackItem item)
        {
            if (hovered == item) return;
            if (hovered != null) hovered.SetHovered(false);
            hovered = item;
            if (hovered != null) hovered.SetHovered(true);
            ui.SetHover(item == null ? "" : item.State == ItemState.Packed ? PackedHint(item)
                : item.Def.Fragile ? item.Def.Name + "  (fragile: nothing goes on top)" : item.Def.Name);
        }

        string PackedHint(PackItem item)
        {
            var top = grid.ItemOnTop(item);
            return top == null ? item.Def.Name + "  (click to take it back out)" : $"{item.Def.Name}  (under the {top.Def.Name})";
        }

        // ------------------------------------------------------------------ Targeting

        void UpdateTarget(Ray ray, bool overUi)
        {
            hasTarget = false;
            if (overUi) return;

            var size = grid.Size;
            Vector3Int cell = default;
            bool found = false;

            if (AimRaycast(ray, out var hit))
            {
                var item = hit.collider.GetComponentInParent<PackItem>();
                bool trunk = hit.collider.GetComponentInParent<TrunkSurface>() != null || (item != null && item.State == ItemState.Packed);
                if (trunk)
                {
                    var local = vehicle.transform.InverseTransformPoint(hit.point + hit.normal * 0.5f);
                    cell = Vector3Int.FloorToInt(local);
                    found = true;
                }
            }

            if (!found)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, size.y, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    var p = ray.GetPoint(enter);
                    if (p.x > -1.5f && p.x < size.x + 1.5f && p.z > -2.5f && p.z < size.z + 1.5f)
                    {
                        cell = new Vector3Int(Mathf.FloorToInt(p.x), size.y, Mathf.FloorToInt(p.z));
                        found = true;
                    }
                }
            }
            if (!found) return;

            var shape = held.Shape;
            cell.x = Mathf.Clamp(cell.x, 0, size.x - 1);
            cell.y = Mathf.Clamp(cell.y, 0, size.y);
            cell.z = Mathf.Clamp(cell.z, 0, size.z - 1);
            int ox = Mathf.Clamp(cell.x - (shape.Size.x - 1) / 2, 0, Mathf.Max(0, size.x - shape.Size.x));
            int oz = Mathf.Clamp(cell.z - (shape.Size.z - 1) / 2, 0, Mathf.Max(0, size.z - shape.Size.z));

            var column = new Vector2Int(ox, oz);
            if (column != lastColumn)
            {
                lastColumn = column;
                heightBias = 0;
            }

            var heights = grid.RestingHeights(shape, ox, oz, held.Def.Fragile, restingHeights);
            hasTarget = true;
            if (heights.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < heights.Count; i++)
                    if (Mathf.Abs(heights[i] - cell.y) < Mathf.Abs(heights[best] - cell.y)) best = i;
                heightBias = Mathf.Clamp(heightBias, -best, heights.Count - 1 - best);
                targetPos = new Vector3Int(ox, heights[best + heightBias], oz);
                targetValid = true;
            }
            else
            {
                int y = Mathf.Clamp(cell.y, 0, Mathf.Max(0, size.y - shape.Size.y));
                targetPos = new Vector3Int(ox, y, oz);
                targetValid = false;
            }
        }

        string ExplainProblem(VoxelShape shape, int ox, int oz)
        {
            var size = grid.Size;
            if (shape.Size.y > size.y) return "Too tall for this trunk. Tip it over with T.";
            if (shape.Size.x > size.x || shape.Size.z > size.z) return "Too long this way. Turn it with R.";

            var worst = PlacementResult.Blocked;
            PackItem culprit = null;
            for (int y = 0; y + shape.Size.y <= size.y; y++)
            {
                var r = grid.Check(shape, new Vector3Int(ox, y, oz), held.Def.Fragile, out var c);
                if (r == PlacementResult.OnFragile || r == PlacementResult.CrushesFragile)
                {
                    worst = r;
                    culprit = c;
                    break;
                }
                if (r == PlacementResult.Floating) worst = r;
            }

            switch (worst)
            {
                case PlacementResult.OnFragile:
                    return $"The {culprit.Def.Name} is fragile. Nothing goes on top of it!";
                case PlacementResult.CrushesFragile:
                    return $"The {held.Def.Name} is fragile. It can't go under the {culprit.Def.Name}.";
                case PlacementResult.Floating:
                    return "It would just be floating there.";
                default:
                    return "No room there. Try turning (R), tipping (T) or rolling (F) it.";
            }
        }

        void UpdateHeldVisuals(Ray ray)
        {
            if (hasTarget)
            {
                if (ghostShape != held.Shape)
                {
                    ghostShape = held.Shape;
                    if (ghostFilter.sharedMesh != null) Destroy(ghostFilter.sharedMesh);
                    ghostFilter.sharedMesh = VoxelMeshBuilder.Build(ghostShape, 1, Vector3.zero, -0.05f);
                }
                ghost.gameObject.SetActive(true);
                ghost.position = vehicle.transform.TransformPoint(targetPos);
                int palette = Mathf.Clamp(GameSettings.PlacementPalette, 0, GhostOk.Length - 1);
                ghostRenderer.sharedMaterial = MaterialLibrary.Ghost(targetValid ? GhostOk[palette] : GhostBad[palette], !targetValid);

                var hand = (Vector3)targetPos + Vector3.up * (targetValid ? 0.3f : 0.45f);
                held.MoveTo(hand, false, 22f);
            }
            else
            {
                ghost.gameObject.SetActive(false);
                var plane = new Plane(Vector3.up, new Vector3(0f, Vehicle.GroundY + 1f, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    var p = ray.GetPoint(enter);
                    var s = held.Shape.Size;
                    held.MoveTo(new Vector3(p.x - s.x * 0.5f, p.y, p.z - s.z * 0.5f), false, 22f);
                }
            }
        }

        // ------------------------------------------------------------------ Actions

        void OnRowClicked(PackItem item)
        {
            if (mode != Mode.Playing) return;
            if (held == item)
            {
                PutBack();
                return;
            }
            if (held != null) PutBack();
            if (item.State == ItemState.Pile || item.State == ItemState.Packed) TryPickUp(item);
        }

        void TryPickUp(PackItem item)
        {
            if (item.State == ItemState.Packed)
            {
                var top = grid.ItemOnTop(item);
                if (top != null)
                {
                    sfx.Error();
                    ui.Toast($"The {top.Def.Name} is sitting on it.");
                    return;
                }
                pendingSnapshot = Capture();
                grid.Remove(item);
                heldFromTrunk = true;
                heldOrigin = item.GridPos;
                heldOriginOrientation = item.Orientation;
            }
            else
            {
                pendingSnapshot = Capture();
                heldFromTrunk = false;
            }

            SetHovered(null);
            held = item;
            item.State = ItemState.Held;
            OnPickedUpHint(item);
            item.SetColliderEnabled(false);
            item.Squash(0.5f);
            Fx.Twinkle(item.transform.position + item.Shape.Center);
            lastColumn = new Vector2Int(-99, -99);
            heightBias = 0;
            sfx.Pickup(item);
            ui.ShowHeld(item);
            RefreshHud();
            OnPickedUpTips(item);
        }

        void PutBack()
        {
            if (held == null) return;
            var item = held;
            held = null;
            if (heldFromTrunk)
            {
                if (item.Orientation != heldOriginOrientation) item.SetOrientation(heldOriginOrientation);
                grid.Place(item, heldOrigin);
                item.GridPos = heldOrigin;
                item.State = ItemState.Packed;
                item.MoveTo(heldOrigin, false, 12f);
            }
            else ReturnToPile(item);

            item.SetColliderEnabled(true);
            pendingSnapshot = null;
            ghost.gameObject.SetActive(false);
            ui.ShowHeld(null);
            sfx.PutBack(item.transform.position);
            RefreshHud();
        }

        void ReturnToPile(PackItem item)
        {
            if (item.Orientation != Quaternion.identity) item.SetOrientation(Quaternion.identity);
            item.State = ItemState.Pile;
            item.MoveTo(item.PileLocalPosition, false, 10f);
        }

        void Rotate(Vector3 axis, bool reverse)
        {
            if (held == null) return;
            var q = Quaternion.AngleAxis(reverse ? -90f : 90f, axis);
            held.Rotate(q);
            heightBias = 0;
            tipRotated = true;
            sfx.Rotate(held.transform.position);
        }

        void PlaceHeld()
        {
            var item = held;
            var pos = targetPos;
            held = null;

            undo.Push(pendingSnapshot);
            pendingSnapshot = null;
            OnPlacedTips();
            grid.Place(item, pos);
            item.GridPos = pos;
            item.State = ItemState.Dropping;
            ghost.gameObject.SetActive(false);
            ui.ShowHeld(null);

            item.DropTo(pos, () =>
            {
                if (item.State != ItemState.Dropping) return;
                item.State = ItemState.Packed;
                item.SetColliderEnabled(true);
                var size = item.Shape.Size;
                var landAt = vehicle.transform.TransformPoint((Vector3)pos + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f));
                sfx.Land(item, landAt);
                Fx.Land(landAt, size);
                if (item.Def.Volume >= 8) rig.Shake(0.05f, 0.15f);
                RefreshHud();
                AnnounceStars();
            });
            RefreshHud();
        }

        float closeArmedUntil;

        /// <summary>
        /// Space and Enter also hurry the story along, so a habitual press shouldn't end the trip while
        /// extras would still fit: the first press asks, a second one within a couple of seconds closes.
        /// (The Close button is a deliberate click and always closes.)
        /// </summary>
        bool ConfirmKeyClose()
        {
            if (Time.unscaledTime <= closeArmedUntil || AllPacked() || !ExtraStillFits()) return true;
            closeArmedUntil = Time.unscaledTime + 2.2f;
            int left = items.Count(i => i.IsBonus && !IsPacked(i));
            sfx.Error();
            ui.Toast($"{left} extra{(left == 1 ? "" : "s")} would still fit! {(GamepadCursor.Active ? "D-PAD RIGHT" : Bindings.Label(Bindings.Action.Close))} again to close anyway.", 2.2f);
            return false;
        }

        /// <summary>Could any extra still on the blanket go into the trunk somewhere, in any orientation?</summary>
        bool ExtraStillFits()
        {
            foreach (var item in items)
            {
                if (!item.IsBonus || IsPacked(item)) continue;
                foreach (var (_, shape) in item.Def.Shape.Orientations())
                    for (int x = 0; x + shape.Size.x <= grid.Size.x; x++)
                    for (int z = 0; z + shape.Size.z <= grid.Size.z; z++)
                        if (grid.RestingHeights(shape, x, z, item.Def.Fragile, restingHeights).Count > 0) return true;
            }
            return false;
        }

        /// <summary>Stars for a trip: one for every essential, two for at least half the extras, three for all of them.</summary>
        public static int StarRating(bool essentialsDone, int bonusDone, int bonus) =>
            !essentialsDone ? 0 : 1 + (bonusDone * 2 >= bonus ? 1 : 0) + (bonusDone == bonus ? 1 : 0);

        int CurrentStars()
        {
            bool essentials = true;
            int bonus = 0, bonusDone = 0;
            foreach (var item in items)
            {
                bool packed = IsPacked(item);
                if (item.IsBonus) { bonus++; if (packed) bonusDone++; }
                else if (!packed) essentials = false;
            }
            return StarRating(essentials, bonusDone, bonus);
        }

        // ------------------------------------------------------------------ Grandpa's seal

        // Asked for a hint (that showed something) since this attempt began. RESTART begins a new
        // attempt; undoing the RESTART brings the hinted packing back, and with it the mark.
        bool hintedThisTry;
        readonly HashSet<List<SavedItem>> restartsAfterHint = new HashSet<List<SavedItem>>();

        static string SealKey(string levelId) => "ptt.seal." + levelId;

        bool Sealed(int index) => Prefs.GetInt(SealKey(GameDatabase.Levels[index].Id), 0) == 1;

        // The highest star count already announced by a toast (lowered when undo takes stars away).
        int starsAnnounced;

        /// <summary>When a drop earns a star, say so and say what the next one needs.</summary>
        void AnnounceStars()
        {
            int now = CurrentStars();
            if (now <= starsAnnounced) return;
            starsAnnounced = now;
            if (now == 3) ui.Toast("Everything fits! Three stars. Close the trunk.", 3f);
            else if (now == 2) ui.Toast("Two stars if you close now. Every extra makes three.", 3.2f);
            else
            {
                int bonus = items.Count(i => i.IsBonus);
                int more = (bonus + 1) / 2 - items.Count(i => i.IsBonus && IsPacked(i));
                ui.Toast($"Essentials packed: one star if you close now. {more} more extra{(more == 1 ? "" : "s")} for two.", 3.4f);
            }
        }

        void Undo()
        {
            if (held != null)
            {
                PutBack();
                return;
            }
            if (undo.Count == 0)
            {
                sfx.Error();
                ui.Toast("Nothing to undo.", 1.2f);
                return;
            }
            var step = undo.Pop();
            if (restartsAfterHint.Remove(step)) hintedThisTry = true;
            Restore(step);
            starsAnnounced = CurrentStars();
            sfx.PutBack(vehicle.transform.position);
            RefreshHud();
        }

        /// <summary>
        /// RESTART while packing: everything goes back on the blanket as a single undo step, so a
        /// misclick next to UNDO never costs a packed trunk.
        /// </summary>
        void UnpackEverything()
        {
            if (mode != Mode.Playing) return;
            if (held != null) PutBack();
            ClearHint();
            if (!items.Any(IsPacked))
            {
                ui.Toast("Nothing's packed yet. The trunk's already empty.", 1.8f);
                return;
            }
            var step = Capture();
            undo.Push(step);
            if (hintedThisTry) restartsAfterHint.Add(step);
            hintedThisTry = false;
            foreach (var item in items)
            {
                if (!IsPacked(item)) continue;
                grid.Remove(item);
                ReturnToPile(item);
                item.SetColliderEnabled(true);
            }
            closeArmedUntil = 0f;
            sfx.PutBack(vehicle.transform.position);
            RefreshHud();
            ui.Toast($"Unpacked everything. {(GamepadCursor.Active ? "VIEW" : Bindings.Label(Bindings.Action.Undo))} puts it all back.", 3f);
            Debug.Log($"[Restart] {level.Id}: unpacked {undo.Peek().Count(s => s.Packed)} items in place");
        }

        List<SavedItem> Capture()
        {
            var list = new List<SavedItem>(items.Count);
            foreach (var item in items)
            {
                bool packed = item.State == ItemState.Packed || item.State == ItemState.Dropping;
                list.Add(new SavedItem
                {
                    Item = item,
                    Packed = packed,
                    Pos = item.GridPos,
                    Orientation = packed ? item.Orientation : Quaternion.identity,
                });
            }
            return list;
        }

        void Restore(List<SavedItem> snapshot)
        {
            foreach (var item in items)
                if (item.State == ItemState.Packed || item.State == ItemState.Dropping)
                    grid.Remove(item);

            foreach (var saved in snapshot)
            {
                var item = saved.Item;
                if (saved.Packed)
                {
                    if (item.Orientation != saved.Orientation) item.SetOrientation(saved.Orientation);
                    grid.Place(item, saved.Pos);
                    item.GridPos = saved.Pos;
                    item.State = ItemState.Packed;
                    item.MoveTo(saved.Pos, false, 12f);
                }
                else ReturnToPile(item);
                item.SetColliderEnabled(true);
            }
        }

        // ------------------------------------------------------------------ Progress

        bool IsPacked(PackItem i) => i.State == ItemState.Packed || i.State == ItemState.Dropping;

        bool CanClose() => mode == Mode.Playing && items.Where(i => !i.IsBonus).All(IsPacked);

        bool AllPacked() => items.All(IsPacked);

        void RefreshHud()
        {
            if (mode != Mode.Playing) return;
            starsAnnounced = Mathf.Min(starsAnnounced, CurrentStars());
            ui.RefreshHud(items, held, CanClose(), AllPacked(), grid.FreeCellCount());
        }

        IEnumerator CloseTrunk()
        {
            if (held != null) PutBack();
            mode = Mode.Closing;
            ClearTips();
            ClearHint();
            ClearSeeThrough();
            SetHovered(null);
            ghost.gameObject.SetActive(false);
            ui.HideHudForCutscene(true);

            while (items.Any(i => i.State == ItemState.Dropping)) yield return null;

            var packed = items.Where(i => i.State == ItemState.Packed).ToList();
            var left = items.Where(i => i.State != ItemState.Packed).ToList();
            int req = items.Count(i => !i.IsBonus);
            int bonus = items.Count(i => i.IsBonus);
            int bonusDone = packed.Count(i => i.IsBonus);
            int stars = StarRating(true, bonusDone, bonus);
            LastStars = stars;
            string key = "ptt.stars." + level.Id;
            int best = Prefs.GetInt(key, 0);
            // The album keeps the best trunk: a quick replay for fewer stars doesn't replace the photo.
            if (stars >= best || PhotoFor(level.Id) == null) StartCoroutine(TakeTrunkPhoto());
            foreach (var item in packed)
            {
                item.SetColliderEnabled(false);
                item.transform.SetParent(vehicle.transform, true);
            }

            yield return new WaitForSeconds(0.25f);
            var from = vehicle.LidPivot.localRotation;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.42f);
                vehicle.LidPivot.localRotation = Quaternion.Slerp(from, vehicle.LidClosed, t * t);
                if (vehicle.FlapPivot != null)
                    vehicle.FlapPivot.localRotation = Quaternion.Slerp(vehicle.FlapOpen, vehicle.FlapClosed, Mathf.Clamp01(t * 1.6f));
                yield return null;
            }
            sfx.Slam();
            music.Duck(0.35f, 0.9f);
            foreach (var item in packed) item.Squash(0.6f);
            var top = vehicle.transform.TransformPoint(new Vector3(level.Size.x * 0.5f, level.Size.y + 1f, level.Size.z * 0.5f));
            Fx.Confetti(top);
            sfx.Popper(top);
            rig.Shake(0.15f, 0.3f);
            yield return new WaitForSeconds(0.6f);

            sfx.Honk();
            music.Duck(0.4f, 0.8f);
            yield return new WaitForSeconds(0.45f);
            sfx.Engine();
            yield return new WaitForSeconds(0.25f);

            float speed = 0f;
            for (float elapsed = 0f; elapsed < 2.4f; elapsed += Time.deltaTime)
            {
                speed += 16f * Time.deltaTime;
                float step = speed * Time.deltaTime;
                vehicle.transform.position += Vector3.forward * step;
                vehicle.SpinWheels(step);
                yield return null;
            }

            Prefs.SetInt(key, Mathf.Max(best, stars));
            // Like stars, a seal is never taken away.
            bool sealNow = stars == 3 && !hintedThisTry;
            bool hadSeal = Prefs.GetInt(SealKey(level.Id), 0) == 1;
            if (sealNow) Prefs.SetInt(SealKey(level.Id), 1);
            Prefs.Save();
            if (stars == 3) Debug.Log($"[Seal] {level.Id}: {(sealNow ? (hadSeal ? "sealed again" : "earned Grandpa's seal") : "three stars with a hint, " + (hadSeal ? "keeps its earlier seal" : "no seal"))}");

            mode = Mode.Results;
            sfx.TripComplete();
            music.Duck(0.35f, 2.6f);
            atmosphere.SetBlur(0.7f);
            sfx.SetAmbience(0.7f, true);
            ui.HideHudForCutscene(false);
            ui.ShowResults(level, stars, req, req, bonusDone, bonus, left.Select(i => i.Def.Name), levelIndex + 1 < GameDatabase.Levels.Count,
                sealNow, stars == 3 && !sealNow && !hadSeal);
        }

        // ------------------------------------------------------------------ Family album

        readonly Dictionary<string, Texture2D> photos = new Dictionary<string, Texture2D>();

        static string AlbumDir => Prefs.AlbumDir;

        /// <summary>
        /// Snap a photo of the packed trunk from just above the bumper, for the family album. The
        /// render happens now; the pixels come back from the GPU asynchronously and the PNG is
        /// encoded and saved on a worker thread, so closing the trunk never stalls a frame.
        /// </summary>
        IEnumerator TakeTrunkPhoto()
        {
            const int width = 480, height = 360;
            var go = new GameObject("Photo Camera");
            var photoCam = go.AddComponent<Camera>();
            photoCam.CopyFrom(cam);
            var size = (Vector3)level.Size;
            var centre = vehicle.transform.TransformPoint(new Vector3(size.x * 0.5f, size.y * 0.4f, size.z * 0.5f));
            float reach = Mathf.Max(size.x, size.z) * 1.1f + 2f;
            go.transform.position = centre + new Vector3(-0.15f * reach, 0.95f * reach, -0.8f * reach);
            go.transform.LookAt(centre);
            photoCam.fieldOfView = 38f;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            photoCam.targetTexture = rt;
            // The open lid hangs over the trunk from up here; leave it out of the snapshot.
            bool lidShown = vehicle.LidPivot.gameObject.activeSelf;
            vehicle.LidPivot.gameObject.SetActive(false);
            photoCam.Render();
            vehicle.LidPivot.gameObject.SetActive(lidShown);
            photoCam.targetTexture = null;
            Destroy(go);

            string id = level.Id;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            byte[] pixels;
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError)
                {
                    RenderTexture.ReleaseTemporary(rt);
                    Destroy(tex);
                    Debug.LogWarning("[Album] could not read back the photo");
                    yield break;
                }
                pixels = request.GetData<byte>().ToArray();
                tex.LoadRawTextureData(pixels);
            }
            else
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                RenderTexture.active = prev;
                pixels = tex.GetRawTextureData();
            }
            RenderTexture.ReleaseTemporary(rt);
            tex.Apply(false, true);
            if (photos.TryGetValue(id, out var old) && old != null) Destroy(old);
            photos[id] = tex;

            var path = System.IO.Path.Combine(AlbumDir, id + ".png");
            var format = tex.graphicsFormat;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var png = ImageConversion.EncodeArrayToPNG(pixels, format, width, height);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllBytes(path, png);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Album] could not save photo: " + e.Message);
                }
            });
        }

        /// <summary>Decode saved album photos a frame at a time on the title screen, so opening the album is instant.</summary>
        IEnumerator WarmAlbum()
        {
            yield return null;
            foreach (var l in GameDatabase.Levels)
            {
                if (photos.ContainsKey(l.Id)) continue;
                if (PhotoFor(l.Id) != null) yield return null;
            }
        }

        Texture2D PhotoFor(string id)
        {
            if (photos.TryGetValue(id, out var tex) && tex != null) return tex;
            var path = System.IO.Path.Combine(AlbumDir, id + ".png");
            if (!System.IO.File.Exists(path)) return null;
            tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(path));
            photos[id] = tex;
            return tex;
        }

        bool albumIsFinale;

        void ShowAlbum(bool finale)
        {
            ResetPause();
            if (!finale) ShowPreview();
            mode = finale ? Mode.Story : Mode.Title;
            albumIsFinale = finale;
            showingEnding = false;
            sfx.Page();
            music.Play(MusicDirector.EndingTrack);
            music.SetMuffled(false);
            ui.ShowAlbum(GameDatabase.Levels, StarsFor, PhotoFor, finale);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
