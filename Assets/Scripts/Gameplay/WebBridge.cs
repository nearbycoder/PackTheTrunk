using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    /// <summary>
    /// The browser build tells its page what's on screen: a small JSON object in <c>window.pttState</c> four times a
    /// second (screen, trip, packed count, held item, settings, the keyboard cursor's control). Tools/check-pages.mjs reads it to know the title is up
    /// and to play a few moves with the real mouse; <c>SendMessage("WebBridge", "Probe", "0")</c> adds where to click (an
    /// item on the blanket, or a spot in the trunk where the held item fits) in screen pixels from the top left.
    /// Only made in the browser build.
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
        string pile = "null", aim = "null";
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
            next = now + 0.25f;
            Publish();
        }

        /// <summary>
        /// Work out where to click next (called from the page), and publish at once. <paramref name="skip"/> passes over
        /// that many clickable things on the blanket (essentials come first), so a page can try another one.
        /// </summary>
        public void Probe(string skip)
        {
            int.TryParse(skip, NumberStyles.Integer, CultureInfo.InvariantCulture, out int passOver);
            pile = aim = "null";
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
              .Append('}');
            PttPublishState(sb.ToString());
        }
    }
}
