using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// Records a gameplay video. Launch the player with
    /// <c>-pttShowcase &lt;frameDir&gt; -pttSolutions &lt;file&gt;</c>: it plays levels like a person would
    /// (smooth mouse moves, real clicks and key presses), draws a cursor, key badges and captions,
    /// and writes one PNG per frame at a fixed 30 fps for ffmpeg to stitch together.
    /// Add <c>-pttTrailer</c> for the trailer script instead (see Showcase.Trailer.cs).
    /// </summary>
    public partial class Showcase : MonoBehaviour
    {
        const int Fps = 30;

        string outDir;
        string solutionsPath;
        GameController game;
        Dictionary<string, List<(string, List<Vector3Int>)>> solutions;

        bool capturing;
        int frame;
        Texture2D grab;
        FileStream wav;
        BinaryWriter wavWriter;
        int wavSamples;
        int channels;

        Vector2 mouse;
        bool leftDown;

        RectTransform cursor, ring;
        Image ringImage;
        float ringT = 1f;
        Text caption;
        CanvasGroup captionGroup;
        float captionTimer;
        Text keyText;
        CanvasGroup keyGroup;
        float keyTimer;

        bool shownFragileDemo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pttShowcase");
            if (i < 0 || i + 1 >= args.Length) return;
            var showcase = new GameObject("Showcase").AddComponent<Showcase>();
            showcase.outDir = args[i + 1];
            int s = Array.IndexOf(args, "-pttSolutions");
            if (s >= 0 && s + 1 < args.Length) showcase.solutionsPath = args[s + 1];
            showcase.trailer = Array.IndexOf(args, "-pttTrailer") >= 0;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            // Capture runs on a sandboxed save (see Prefs), so the video shows a new player's game
            // and the real save is left alone.
            Time.captureFramerate = Fps;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return null;
            game = FindAnyObjectByType<GameController>();
            solutions = AutoPilot.LoadSolutions(solutionsPath);
            game.AutoShowTitle();
            BuildOverlay();
            StartCoroutine(CaptureLoop());
            if (trailer)
            {
                yield return TrailerScript();
                yield break;
            }

            mouse = new Vector2(Screen.width * 0.62f, Screen.height * 0.3f);
            Push();
            yield return null;
            capturing = true;
            Log("recording");

            // Title screen and main menu.
            yield return Hold(4.2f);
            yield return Press(Key.Space, false);
            yield return Hold(1.4f);
            foreach (var entry in new[] { "Continue", "Trip Map", "Album", "Settings" })
            {
                yield return MoveTo(UiPoint(entry), 0.45f);
                yield return Hold(0.45f);
            }
            yield return Click();
            yield return Hold(1.2f);

            // Settings: a quick tour of the tabs and a live toggle.
            yield return MoveTo(NamedPoint("Music"), 0.5f);
            yield return Hold(0.8f);
            yield return MoveTo(NamedPoint("Ambience"), 0.35f);
            yield return Hold(0.8f);
            foreach (var tab in new[] { "DISPLAY", "GRAPHICS" })
            {
                yield return MoveTo(UiPoint("Tab " + tab), 0.5f);
                yield return Hold(0.2f);
                yield return Click();
                yield return Hold(1.1f);
            }
            yield return MoveTo(NamedPoint("Ink outlines", "Switch"), 0.6f);
            yield return Hold(0.4f);
            yield return Click();
            yield return Hold(1.0f);
            yield return Click();
            yield return Hold(0.8f);
            foreach (var tab in new[] { "GAMEPLAY", "CONTROLS" })
            {
                yield return MoveTo(UiPoint("Tab " + tab), 0.5f);
                yield return Hold(0.2f);
                yield return Click();
                yield return Hold(1.3f);
            }
            yield return ClickButton("Settings Back", 0.6f, 0.3f);
            yield return Hold(0.9f);

            // Chapter I: Grandpa's wagon.
            yield return ClickButton("Continue", 0.7f, 0.4f);
            yield return StartTrip();
            yield return PlayLevel("wagon", true);
            yield return CloseTrunk();
            yield return ClickButton("Next", 0.8f, 0.3f);

            // Chapter II: a real car, texts from Mom, and the pause menu.
            yield return StartTrip();
            yield return PlayLevel("weekend", false);
            Caption("Esc pauses (the world blurs behind the menu)", 2.6f);
            yield return Press(Key.Escape, false);
            yield return Hold(1.6f);
            yield return MoveTo(UiPoint("Pause Settings"), 0.5f);
            yield return Hold(0.5f);
            yield return ClickButton("Resume", 0.5f, 0.4f);
            yield return Hold(0.8f);
            yield return CloseTrunk();

            // Thirty years on: the finale, Grandma's note, the album and the credits.
            Caption("Thirty years later...", 2.6f, 34);
            yield return Hold(1.6f);
            yield return Trip("finale");
            yield return ClickButton("Next", 0.8f, 0.3f);
            yield return Hold(15f);
            yield return ClickButton("Start", 0.9f, 0.4f);
            yield return Hold(12.5f);
            yield return ClickButton("Album Done", 0.9f, 0.4f);
            yield return Hold(15f);

            capturing = false;
            yield return null;
            FinishAudio();
            Log($"done, {frame} frames, {wavSamples / Mathf.Max(1, channels)} audio frames");
            yield return null;
            Application.Quit();
        }

        /// <summary>Wait for a button to stop animating in, glide to it, then click it.</summary>
        IEnumerator ClickButton(string name, float moveSeconds, float pause)
        {
            var last = new Vector2(-9999, -9999);
            for (float t = 0f; t < 4f; t += 0.1f)
            {
                var p = UiPoint(name);
                if (ButtonVisible(name) && (p - last).sqrMagnitude < 1f) break;
                last = p;
                yield return Hold(0.1f);
            }
            yield return MoveTo(UiPoint(name), moveSeconds);
            yield return Hold(pause);
            mouse = UiPoint(name);
            Push();
            yield return null;
            yield return Click();
        }

        /// <summary>Screen point of any named UI element (optionally a named child inside it).</summary>
        Vector2 NamedPoint(string name, string child = null)
        {
            var rt = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.name == name);
            if (rt != null && child != null)
                rt = rt.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == child);
            if (rt == null) return mouse;
            return rt.TransformPoint(rt.rect.center);
        }

        // ------------------------------------------------------------------ playing

        IEnumerator PlayLevel(string levelId, bool tutorial)
        {
            if (!solutions.TryGetValue(levelId, out var placements)) yield break;
            int n = 0;
            if (tutorial)
            {
                Caption("Q / E or right-drag to look around", 3.2f);
                yield return ShowOffCar();
            }
            foreach (var (itemId, cells) in placements)
            {
                if (n == (tutorial ? 4 : 5))
                {
                    if (!tutorial) Caption("Spin the camera to find the gaps", 2.4f);
                    yield return Glance(Key.E, 0.6f);
                }
                var item = game.Items.FirstOrDefault(i => i.Def.Id == itemId && i.State == ItemState.Pile);
                if (item == null) continue;
                var min = cells.Aggregate(Vector3Int.Min);
                var want = new HashSet<Vector3Int>(cells.Select(c => c - min));

                if (tutorial && n == 0) Caption("Click something on the driveway to pick it up", 2.4f);
                yield return MoveTo(PointOnItem(item), 0.55f);
                yield return Hold(0.12f);
                yield return Click();
                yield return Hold(0.3f);
                if (game.Held != item)
                {
                    Fallback(item, cells, min);
                    yield return Hold(0.4f);
                    continue;
                }

                var keys = PlanRotation(item, want);
                if (tutorial && keys.Count > 0 && n < 3) Caption("R turns it · T tips it over · F rolls it", 2.4f);
                foreach (var (key, shift) in keys)
                {
                    yield return Press(key, shift);
                    yield return Hold(0.32f);
                }

                if (!shownFragileDemo && !item.Def.Fragile)
                    yield return FragileDemo(min, item);

                var aim = FindAim(min, item.Shape.Size);
                if (aim == null)
                {
                    Fallback(item, cells, min);
                    yield return Hold(0.4f);
                    continue;
                }
                if (tutorial && n == 0) Caption("Point into the trunk: green means it fits", 2.4f);
                yield return MoveTo(aim.Value, 0.65f);
                yield return Hold(0.2f);

                for (int k = 0; k < 4; k++)
                {
                    if (!game.CurrentTarget(out var t, out var valid) || (t == min && valid)) break;
                    if (t.x != min.x || t.z != min.z) break;
                    yield return Press(t.y < min.y ? Key.W : Key.S, false);
                    yield return Hold(0.25f);
                }

                if (game.CurrentTarget(out var target, out var ok) && target == min && ok)
                {
                    yield return Click();
                    yield return Hold(0.6f);
                }
                else
                {
                    Fallback(item, cells, min);
                    yield return Hold(0.4f);
                }
                n++;
            }
            yield return Hold(0.6f);
        }

        IEnumerator Trip(string levelId)
        {
            int index = GameDatabase.Levels.Select(l => l.Id).ToList().IndexOf(levelId);
            game.AutoTransitionTrip(index);
            yield return StartTrip();
            yield return PlayLevel(levelId, false);
            yield return CloseTrunk();
        }

        /// <summary>Let the story texts play out, then press "Let's pack!".</summary>
        IEnumerator StartTrip()
        {
            for (float t = 0f; t < 40f && !ButtonVisible("Start"); t += UiTime.Delta) yield return null;
            yield return Hold(1.2f);
            yield return ClickButton("Start", 0.7f, 0.3f);
            yield return Hold(1.2f);
        }

        static bool ButtonVisible(string name) =>
            FindObjectsByType<Button>(FindObjectsInactive.Exclude).Any(b => b.name == name);

        /// <summary>Show the fragile rule once: hover over a packed fragile item and get told off.</summary>
        IEnumerator FragileDemo(Vector3Int correct, PackItem held)
        {
            // Only demo where the game itself says "fragile" (a full-height gnome just says "no room").
            PackItem fragile = null;
            var screen = Vector2.zero;
            foreach (var candidate in game.Items.Where(i => i.Def.Fragile && i.State == ItemState.Packed))
            {
                var top = candidate.Shape.Voxels.OrderByDescending(v => v.Pos.y).First().Pos + candidate.GridPos;
                var world = game.CurrentVehicle.transform.TransformPoint((Vector3)top + new Vector3(0.5f, 1.02f, 0.5f));
                var point = (Vector2)game.Camera.WorldToScreenPoint(world);
                if (game.PreviewTarget(point, out _, out var valid, out var problem) && !valid &&
                    problem != null && problem.Contains("fragile"))
                {
                    fragile = candidate;
                    screen = point;
                    break;
                }
            }
            if (fragile == null) yield break;

            shownFragileDemo = true;
            Caption($"The {fragile.Def.Name} is fragile: nothing can go on top", 2.8f);
            yield return MoveTo(screen, 0.7f);
            yield return Hold(0.5f);
            yield return Click();
            yield return Hold(1.4f);
        }

        IEnumerator CloseTrunk()
        {
            Caption("Everything's in. Close the trunk!", 2.2f);
            yield return ClickButton("Close", 0.8f, 0.35f);
            yield return Hold(1.6f);
            yield return HoldKey(Key.E, 1.2f);
            yield return Hold(4.6f);
        }

        void Fallback(PackItem item, List<Vector3Int> cells, Vector3Int min)
        {
            Log($"fallback for {item.Def.Id}");
            game.AutoPlace(item, AutoPilot.FindOrientation(item.Def.Shape, cells), min);
        }

        /// <summary>Shortest sequence of R/T/F (optionally shifted) presses that reaches the wanted cells.</summary>
        List<(Key, bool)> PlanRotation(PackItem item, HashSet<Vector3Int> want)
        {
            var moves = new List<(Key key, bool shift, Quaternion q)>();
            foreach (var (key, axis) in new[] { (Key.R, Vector3.up), (Key.T, game.Rig.SnappedRight()), (Key.F, game.Rig.SnappedForward()) })
            {
                moves.Add((key, false, Quaternion.AngleAxis(90f, axis)));
                moves.Add((key, true, Quaternion.AngleAxis(-90f, axis)));
            }

            string ShapeKey(Quaternion q) => string.Join(";", item.Def.Shape.Rotated(q).Voxels.Select(v => v.Pos.ToString()).OrderBy(s => s));
            string goal = string.Join(";", want.Select(v => v.ToString()).OrderBy(s => s));

            var queue = new Queue<(Quaternion q, List<(Key, bool)> path)>();
            var seen = new HashSet<string>();
            queue.Enqueue((item.Orientation, new List<(Key, bool)>()));
            while (queue.Count > 0)
            {
                var (q, path) = queue.Dequeue();
                var k = ShapeKey(q);
                if (k == goal) return path;
                if (!seen.Add(k) || path.Count >= 4) continue;
                foreach (var mv in moves)
                    queue.Enqueue((mv.q * q, new List<(Key, bool)>(path) { (mv.key, mv.shift) }));
            }
            return new List<(Key, bool)>();
        }

        Vector2 PointOnItem(PackItem item)
        {
            var cells = item.Shape.Voxels.OrderByDescending(v => v.Pos.y).ThenBy(v => v.Pos.z).ToList();
            foreach (var v in cells)
            {
                var world = item.transform.TransformPoint((Vector3)v.Pos + new Vector3(0.5f, 0.6f, 0.5f));
                var screen = (Vector2)game.Camera.WorldToScreenPoint(world);
                Physics.SyncTransforms();
                if (Physics.Raycast(game.Camera.ScreenPointToRay(screen), out var hit, 300f) &&
                    hit.collider.GetComponentInParent<PackItem>() == item)
                    return screen;
            }
            return game.Camera.WorldToScreenPoint(item.transform.TransformPoint(item.Shape.Center));
        }

        Vector2? FindAim(Vector3Int min, Vector3Int size)
        {
            var vehicle = game.CurrentVehicle.transform;
            int cx = min.x + (size.x - 1) / 2, cz = min.z + (size.z - 1) / 2;
            Vector2? columnMatch = null;
            var offsets = new[] { Vector2.zero, new Vector2(0.25f, 0.25f), new Vector2(-0.25f, 0.25f), new Vector2(0.25f, -0.25f), new Vector2(-0.25f, -0.25f) };
            foreach (var off in offsets)
            for (float y = min.y; y <= 6f; y += 0.5f)
            {
                var world = vehicle.TransformPoint(new Vector3(cx + 0.5f + off.x, y + 0.02f, cz + 0.5f + off.y));
                var screen = (Vector2)game.Camera.WorldToScreenPoint(world);
                if (!game.PreviewTarget(screen, out var t, out var valid)) continue;
                if (t == min && valid) return screen;
                if (columnMatch == null && t.x == min.x && t.z == min.z) columnMatch = screen;
            }
            return columnMatch;
        }

        Vector2 UiPoint(string buttonName)
        {
            var button = FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b => b.name == buttonName);
            if (button == null) return mouse;
            var rt = (RectTransform)button.transform;
            return rt.TransformPoint(rt.rect.center);
        }

        // ------------------------------------------------------------------ input

        void Push() =>
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = mouse }.WithButton(MouseButton.Left, leftDown));

        IEnumerator MoveTo(Vector2 target, float seconds)
        {
            var start = mouse;
            var delta = target - start;
            var bend = new Vector2(-delta.y, delta.x) * 0.08f;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + UiTime.Delta / seconds);
                float e = t * t * (3f - 2f * t);
                mouse = start + delta * e + bend * Mathf.Sin(t * Mathf.PI);
                Push();
                yield return null;
            }
        }

        IEnumerator Click()
        {
            leftDown = true;
            Push();
            ringT = 0f;
            yield return null;
            yield return null;
            leftDown = false;
            Push();
            yield return null;
        }

        IEnumerator Press(Key key, bool shift)
        {
            keyText.text = (shift ? "Shift + " : "") + key.ToString().ToUpperInvariant();
            keyTimer = 0.8f;
            var state = shift ? new KeyboardState(key, Key.LeftShift) : new KeyboardState(key);
            InputSystem.QueueStateEvent(Keyboard.current, state);
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        /// <summary>Hold a key down (e.g. Q/E to orbit the camera).</summary>
        IEnumerator HoldKey(Key key, float seconds)
        {
            keyText.text = key.ToString().ToUpperInvariant();
            keyTimer = seconds + 0.3f;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            for (float t = 0f; t < seconds; t += UiTime.Delta)
            {
                keyTimer = Mathf.Max(keyTimer, 0.3f);
                yield return null;
            }
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        /// <summary>Mouse-wheel zoom: positive = in.</summary>
        IEnumerator Zoom(int notches)
        {
            for (int i = 0; i < Mathf.Abs(notches); i++)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = mouse, scroll = new Vector2(0, notches > 0 ? 120 : -120) });
                yield return null;
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = mouse });
                for (int k = 0; k < 3; k++) yield return null;
            }
        }

        /// <summary>Right-button drag to orbit/tilt the camera.</summary>
        IEnumerator RightDrag(Vector2 total, float seconds)
        {
            var start = mouse;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = mouse }.WithButton(MouseButton.Right, true));
            yield return null;
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (int i = 1; i <= frames; i++)
            {
                float e = Mathf.SmoothStep(0f, 1f, i / (float)frames) - Mathf.SmoothStep(0f, 1f, (i - 1) / (float)frames);
                var step = total * e;
                mouse += step;
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = mouse, delta = step }.WithButton(MouseButton.Right, true));
                yield return null;
            }
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = mouse });
            yield return null;
            yield return MoveTo(start, 0.4f);
        }

        /// <summary>Establishing shot: swing in close around the car, then settle back.</summary>
        IEnumerator ShowOffCar()
        {
            // Swing to the blanket side (the far side would put the car body in front of the camera).
            yield return MoveTo(new Vector2(Screen.width * 0.42f, Screen.height * 0.45f), 0.4f);
            yield return Zoom(3);
            yield return HoldKey(Key.E, 0.9f);
            yield return RightDrag(new Vector2(0f, 50f), 0.8f);
            yield return Hold(1.0f);
            yield return RightDrag(new Vector2(0f, -50f), 0.7f);
            yield return HoldKey(Key.Q, 0.9f);
            yield return Zoom(-3);
            yield return Hold(1.0f);
        }

        /// <summary>A quick look around mid-level to show the trunk in 3D.</summary>
        IEnumerator Glance(Key first, float seconds)
        {
            yield return HoldKey(first, seconds);
            yield return Hold(0.4f);
            yield return HoldKey(first == Key.Q ? Key.E : Key.Q, seconds);
            yield return Hold(0.7f);
        }

        static IEnumerator Hold(float seconds)
        {
            for (float t = 0f; t < seconds; t += UiTime.Delta) yield return null;
        }

        // ------------------------------------------------------------------ overlay + capture

        void BuildOverlay()
        {
            var canvasGo = new GameObject("Showcase Overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            overlay = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var root = (RectTransform)canvasGo.transform;

            var capBg = UiKit.Image("Caption", root, new Color(0.06f, 0.07f, 0.12f, 0.88f));
            capBg.raycastTarget = false;
            capBg.rectTransform.Pin(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-170, -175), new Vector2(900, 70));
            captionGroup = capBg.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0f;
            caption = UiKit.Label("Text", capBg.transform, "", 30, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            caption.rectTransform.Fill(8);

            var keyBg = UiKit.Image("Key", root, UiKit.Accent);
            keyBg.raycastTarget = false;
            keyBg.rectTransform.Pin(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170, 230), new Vector2(220, 74));
            keyGroup = keyBg.gameObject.AddComponent<CanvasGroup>();
            keyGroup.alpha = 0f;
            keyText = UiKit.Label("Text", keyBg.transform, "", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            keyText.rectTransform.Fill(4);

            ringImage = UiKit.Image("Ring", root, new Color(1f, 0.85f, 0.3f, 0f), false);
            ringImage.sprite = MakeRing(64);
            ringImage.raycastTarget = false;
            ring = ringImage.rectTransform;
            ring.sizeDelta = new Vector2(44, 44);

            var cursorImg = UiKit.Image("Cursor", root, Color.white, false);
            cursorImg.sprite = MakeCursor();
            cursorImg.raycastTarget = false;
            cursor = cursorImg.rectTransform;
            cursor.pivot = new Vector2(0.06f, 0.96f);
            cursor.sizeDelta = new Vector2(34, 34);
        }

        void Caption(string text, float seconds, int size = 30)
        {
            caption.text = text;
            caption.fontSize = size;
            var rt = (RectTransform)caption.transform.parent;
            rt.sizeDelta = new Vector2(Mathf.Max(520, text.Length * size * 0.56f + 60), size + 40);
            captionTimer = seconds;
        }

        void LateUpdate()
        {
            if (cursor == null) return;
            float dt = UiTime.Delta;
            if (trailer) TrailerLateUpdate();
            cursor.position = mouse;
            ring.position = mouse;
            ringT = Mathf.Min(1f, ringT + dt / 0.35f);
            ring.localScale = Vector3.one * (0.4f + ringT * 1.2f);
            ringImage.color = new Color(1f, 0.85f, 0.3f, 1f - ringT);
            captionTimer -= dt;
            captionGroup.alpha = Mathf.Clamp01(Mathf.Min(captionTimer / 0.3f, 1f));
            keyTimer -= dt;
            keyGroup.alpha = Mathf.Clamp01(keyTimer / 0.25f);
        }

        IEnumerator CaptureLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                if (!capturing && pendingStill == null)
                {
                    DrainAudio();
                    continue;
                }
                if (grab == null || grab.width != Screen.width || grab.height != Screen.height)
                    grab = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                grab.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                grab.Apply();
                if (pendingStill != null) SaveStill();
                if (!capturing)
                {
                    DrainAudio();
                    continue;
                }
                // The trailer records a lot of footage: high-quality JPEG keeps it fast and small.
                if (trailer) File.WriteAllBytes(Path.Combine(outDir, $"frame_{frame++:00000}.jpg"), grab.EncodeToJPG(95));
                else File.WriteAllBytes(Path.Combine(outDir, $"frame_{frame++:00000}.png"), grab.EncodeToPNG());
                CaptureAudio();
            }
        }

        /// <summary>Offline audio capture in lockstep with Time.captureFramerate.</summary>
        void CaptureAudio()
        {
            if (wav == null)
            {
                channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
                wav = new FileStream(Path.Combine(outDir, "audio.wav"), FileMode.Create);
                wavWriter = new BinaryWriter(wav);
                wavWriter.Write(new byte[44]);
                AudioRenderer.Start();
            }
            int count = AudioRenderer.GetSampleCountForCaptureFrame();
            if (count <= 0) return;
            using (var buffer = new NativeArray<float>(count * channels, Allocator.Temp))
            {
                AudioRenderer.Render(buffer);
                for (int i = 0; i < buffer.Length; i++)
                    wavWriter.Write((short)(Mathf.Clamp(buffer[i], -1f, 1f) * 32767f));
                wavSamples += buffer.Length;
            }
        }

        /// <summary>
        /// Between trailer clips the audio renderer keeps running: pull this frame's samples and
        /// drop them, or they would pile up and land in the next clip out of sync.
        /// </summary>
        void DrainAudio()
        {
            if (wav == null) return;
            int count = AudioRenderer.GetSampleCountForCaptureFrame();
            if (count <= 0) return;
            using (var buffer = new NativeArray<float>(count * channels, Allocator.Temp))
                AudioRenderer.Render(buffer);
        }

        void FinishAudio()
        {
            if (wav == null) return;
            AudioRenderer.Stop();
            int rate = AudioSettings.outputSampleRate;
            int dataBytes = wavSamples * 2;
            wav.Seek(0, SeekOrigin.Begin);
            wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            wavWriter.Write(36 + dataBytes);
            wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            wavWriter.Write(16);
            wavWriter.Write((short)1);
            wavWriter.Write((short)channels);
            wavWriter.Write(rate);
            wavWriter.Write(rate * channels * 2);
            wavWriter.Write((short)(channels * 2));
            wavWriter.Write((short)16);
            wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            wavWriter.Write(dataBytes);
            wavWriter.Flush();
            wav.Dispose();
            wav = null;
        }

        static Sprite MakeCursor()
        {
            const int size = 64;
            Vector2[] arrow =
            {
                new Vector2(4, 60), new Vector2(4, 12), new Vector2(16, 23), new Vector2(26, 2),
                new Vector2(34, 6), new Vector2(24, 26), new Vector2(40, 26),
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = Inside(arrow, p);
                float edge = EdgeDistance(arrow, p);
                Color32 c = new Color32(0, 0, 0, 0);
                if (inside) c = edge < 3f ? new Color32(20, 20, 24, 255) : new Color32(255, 255, 255, 255);
                else if (edge < 2f) c = new Color32(20, 20, 24, (byte)(255 * (1f - edge / 2f)));
                px[y * size + x] = c;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.06f, 0.94f));
        }

        static Sprite MakeRing(int size)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - size * 0.42f) / 3f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var b = poly[i];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
            }
            return best;
        }

        static void Log(string message) => Debug.Log("[Showcase] " + message);
    }
}
