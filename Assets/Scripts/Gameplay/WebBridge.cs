using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// The browser build tells its page what's on screen: a small JSON object in <c>window.pttState</c> four times a
    /// second, ten while the touch controls show (screen, trip, packed count, held item, settings, the keyboard cursor's
    /// control, what the touch controls need). Tools/check-pages.mjs reads it to know the title is up
    /// and to play a few moves with the real mouse; <c>SendMessage("WebBridge", "Probe", "0")</c> adds where to click (an
    /// item on the blanket, or a spot in the trunk where the held item fits, and the buttons on screen) in screen pixels
    /// from the top left.
    /// The page's on-screen touch controls talk to the game through it too: <c>Touch</c> ("1,&lt;aim offset px&gt;,&lt;left&gt;,&lt;bottom&gt;"
    /// when they show, with the share of the screen they cover; "0" when a mouse, key or pad takes over), <c>Lighten</c> (after a crash on a phone), <c>Verb</c> (a button: turn, tip, roll, up, down, back, undo, redo,
    /// hint, pause, xray-down, xray-up, cancel), <c>Orbit</c> ("dx,dy" screen pixels, y up) and <c>Pinch</c> (a spread
    /// factor). See GameController.Touch.cs. Only made in the browser build.
    /// </summary>
    public class WebBridge : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PttPublishState(string json);
#else
        static void PttPublishState(string json) { }
#endif

        GameController game;
        float next;
        int frames;
        float fpsStart;
        float fps;
        string pile = "null", aim = "null", buttons = "[]";

        static string Rot(Vector3 e) =>
            e.x.ToString("0", CultureInfo.InvariantCulture) + " " + e.y.ToString("0", CultureInfo.InvariantCulture) + " " + e.z.ToString("0", CultureInfo.InvariantCulture);
        readonly StringBuilder sb = new StringBuilder(512);

        public static void Attach(GameController game)
        {
            var go = new GameObject("WebBridge");
            DontDestroyOnLoad(go);
            go.AddComponent<WebBridge>().game = game;
        }

        void Update()
        {
            frames++;
            float now = Time.realtimeSinceStartup;
            if (now - fpsStart >= 1f)
            {
                fps = frames / (now - fpsStart);
                frames = 0;
                fpsStart = now;
            }
            if (now < next) return;
            next = now + (Web.TouchActive ? 0.1f : 0.25f);
            Publish();
        }

        public void Touch(string args)
        {
            var parts = args.Split(',');
            float Part(int i) => parts.Length > i && float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
            Web.SetTouch(parts[0] == "1", Part(1), Part(2), Part(3));
            Publish();
        }

        public void Verb(string verb) => game.QueueTouchVerb(verb);

        /// <summary>
        /// The page found that the browser closed the game last time on a phone or tablet (most likely for memory): back
        /// to the Low graphics step, whatever the save says.
        /// </summary>
        public void Lighten(string unused)
        {
            if (GameSettings.Fidelity == 0 && !GameSettings.FidelityCustom) return;
            Debug.Log($"[Web] the browser closed the game last time: graphics from step {GameSettings.Fidelity}{(GameSettings.FidelityCustom ? " (fine-tuned)" : "")} to Low");
            GameSettings.Fidelity = 0;
            GameSettings.Apply();
        }

        public void Orbit(string delta)
        {
            var parts = delta.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float dx)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float dy))
                game.TouchOrbit(new Vector2(dx, dy));
        }

        public void Pinch(string spread)
        {
            if (float.TryParse(spread, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) game.TouchPinch(f);
        }

        /// <summary>
        /// Work out where to click next (called from the page), and publish at once. <paramref name="skip"/> passes over
        /// that many clickable things on the blanket (essentials come first), so a page can try another one.
        /// </summary>
        public void Probe(string skip)
        {
            int.TryParse(skip, NumberStyles.Integer, CultureInfo.InvariantCulture, out int passOver);
            pile = aim = "null";
            buttons = Buttons();
            var cam = game.Camera;
            if (cam != null && game.IsPlaying)
            {
                Physics.SyncTransforms();
                if (game.Held == null)
                {
                    for (int i = 0; i < game.Items.Count * 2 && pile == "null"; i++)
                    {
                        // essentials on the first pass, extras on the second
                        var item = game.Items[i % game.Items.Count];
                        if (item.State != ItemState.Pile || item.IsBonus != (i >= game.Items.Count)) continue;
                        var r = item.GetComponentInChildren<Renderer>();
                        var s = cam.WorldToScreenPoint(r != null ? r.bounds.center : item.transform.position);
                        if (s.z <= 0f) continue;
                        // Only an item the pointer would really land on (not one hidden behind another).
                        if (Physics.Raycast(cam.ScreenPointToRay(s), out var hit, 300f) && hit.collider.GetComponentInParent<PackItem>() == item
                            && passOver-- <= 0)
                            pile = Point(s);
                    }
                }
                else if (FindAim(out var screen)) aim = Point(screen);
            }
            Publish();
        }

        /// <summary>A screen point over the trunk where the held item would land validly (like AutoPilot.FindAim).</summary>
        bool FindAim(out Vector2 screen)
        {
            var size = game.TrunkSize;
            var trunk = game.CurrentVehicle.transform;
            for (int y = 0; y <= size.y; y++)
            for (int z = 0; z < size.z; z++)
            for (int x = 0; x < size.x; x++)
            {
                var s = game.Camera.WorldToScreenPoint(trunk.TransformPoint(new Vector3(x + 0.5f, y, z + 0.5f)));
                screen = new Vector2(s.x, s.y);
                if (game.PreviewTarget(screen, out _, out bool valid) && valid) return true;
            }
            screen = default;
            return false;
        }

        /// <summary>The buttons a tap would reach right now: [{"name","label","at":[x,y]}], top-left screen pixels.</summary>
        string Buttons()
        {
            var events = EventSystem.current;
            if (events == null) return "[]";
            var b = new StringBuilder("[");
            var corners = new Vector3[4];
            foreach (var sel in Selectable.allSelectablesArray)
            {
                if (!sel.isActiveAndEnabled || !sel.IsInteractable() || sel.transform is not RectTransform rect) continue;
                var canvas = sel.GetComponentInParent<Canvas>();
                if (canvas == null) continue;
                var cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
                rect.GetWorldCorners(corners);
                var centre = (RectTransformUtility.WorldToScreenPoint(cam, corners[0]) + RectTransformUtility.WorldToScreenPoint(cam, corners[2])) * 0.5f;
                if (centre.x < 0 || centre.y < 0 || centre.x >= Screen.width || centre.y >= Screen.height) continue;
                hits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = centre }, hits);
                if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(sel.transform)) continue;
                var label = sel.GetComponentInChildren<Text>();
                if (b.Length > 1) b.Append(',');
                b.Append("{\"name\":\"").Append(JsonText(sel.name)).Append("\",\"label\":\"").Append(JsonText(label != null ? label.text : ""))
                 .Append("\",\"at\":").Append(Point(centre)).Append('}');
            }
            return b.Append(']').ToString();
        }

        static string JsonText(string text)
        {
            var b = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else b.Append(c < ' ' ? ' ' : c);
            }
            return b.ToString();
        }

        readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();

        static string Pointer(Mouse m) =>
            m == null ? "null" : "{\"device\":\"" + m.name + "\",\"at\":" + Point(m.position.ReadValue()) + ",\"down\":" + (m.leftButton.isPressed ? "true" : "false") + "}";

        static string Point(Vector2 s) =>
            "[" + s.x.ToString("0", CultureInfo.InvariantCulture) + "," + (Screen.height - s.y).ToString("0", CultureInfo.InvariantCulture) + "]";

        void Publish()
        {
            if (game == null) return;
            int packed = 0;
            foreach (var item in game.Items)
                if (item.State == ItemState.Packed) packed++;
            var inv = CultureInfo.InvariantCulture;
            sb.Clear();
            sb.Append("{\"mode\":\"").Append(game.ModeName)
              .Append("\",\"waiting\":").Append(game.Ui.IsTitleWaiting ? "true" : "false")
              .Append(",\"paused\":").Append(game.IsPaused ? "true" : "false")
              .Append(",\"settings\":").Append(game.Ui.SettingsOpen ? "true" : "false")
              .Append(",\"nav\":\"").Append(GamepadCursor.LastNavigation.Replace("\"", "'")).Append('"')
              .Append(",\"level\":\"").Append(game.IsPlaying || game.IsShowingResults ? game.CurrentLevelDef?.Id : "")
              .Append("\",\"items\":").Append(game.Items.Count)
              .Append(",\"packed\":").Append(packed)
              .Append(",\"held\":").Append(game.Held != null ? "true" : "false")
              .Append(",\"fidelity\":").Append(GameSettings.Fidelity)
              .Append(",\"custom\":").Append(GameSettings.FidelityCustom ? "true" : "false")
              .Append(",\"master\":").Append(GameSettings.Master.ToString("0.00", inv))
              .Append(",\"fullscreen\":").Append(Screen.fullScreen ? "true" : "false")
              .Append(",\"w\":").Append(Screen.width)
              .Append(",\"h\":").Append(Screen.height)
              .Append(",\"cursor\":\"").Append(GamepadCursor.KeysActive ? "keys" : GamepadCursor.Active ? "pad" : "mouse")
              .Append("\",\"pointer\":").Append(Pointer(Mouse.current))
              .Append(",\"fps\":").Append(fps.ToString("0.0", inv))
              .Append(",\"pile\":").Append(pile)
              .Append(",\"aim\":").Append(aim)
              .Append(",\"touch\":").Append(Web.TouchActive ? "true" : "false")
              .Append(",\"touchFirst\":").Append(Web.IsTouchFirst ? "true" : "false")
              .Append(",\"touchOffset\":").Append(Web.TouchAimOffset.ToString("0", inv))
              .Append(",\"xray\":").Append(game.XRayOn ? "true" : "false")
              .Append(",\"hint\":").Append(game.HintShowing ? "true" : "false")
              .Append(",\"yaw\":").Append(game.Rig != null ? game.Rig.Yaw.ToString("0.0", inv) : "0")
              .Append(",\"rot\":\"").Append(Rot(game.HeldRotation)).Append('"')
              .Append(",\"redo\":").Append(game.Ui.RedoShowing ? "true" : "false")
              .Append(",\"hintOk\":").Append(game.HintsAvailable ? "true" : "false")
              .Append(",\"overlay\":").Append(game.Ui.OverlayOpen ? "true" : "false")
              .Append(",\"buttons\":").Append(buttons)
              .Append('}');
            PttPublishState(sb.ToString());
        }
    }
}
