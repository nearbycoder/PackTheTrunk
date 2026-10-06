using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    /// <summary>
    /// Logs every input device the player sees, at start and whenever one comes or goes, so a real
    /// controller test that "does nothing" can be diagnosed from Player.log. A controller that Unity
    /// only knows as a generic joystick (not a gamepad) can't drive the game; that gets its own line
    /// and a toast with what to try.
    /// </summary>
    public static class InputReport
    {
        /// <summary>Raised with the device name when an unsupported joystick connects.</summary>
        public static event Action<string> UnsupportedController;

        /// <summary>The most recent [Input] line (for the self-test).</summary>
        public static string LastLine { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            foreach (var device in InputSystem.devices) Report("found", device);
            InputSystem.onDeviceChange += (device, change) =>
            {
                switch (change)
                {
                    case InputDeviceChange.Added: Report("added", device); break;
                    case InputDeviceChange.Removed: Report("removed", device); break;
                    case InputDeviceChange.Reconnected: Report("reconnected", device); break;
                    case InputDeviceChange.Disconnected: Report("disconnected", device); break;
                }
            };
        }

        static void Report(string what, InputDevice device)
        {
            string kind = device is Gamepad ? "Gamepad" : device is Joystick ? "Joystick" : device is Keyboard ? "Keyboard"
                : device is Mouse ? "Mouse" : device.GetType().Name;
            var d = device.description;
            Log($"[Input] {what} {kind} \"{device.displayName}\" (layout {device.layout}" +
                (string.IsNullOrEmpty(d.interfaceName) ? "" : $", interface {d.interfaceName}") +
                (string.IsNullOrEmpty(d.manufacturer) ? "" : $", maker \"{d.manufacturer}\"") +
                (string.IsNullOrEmpty(d.product) ? "" : $", product \"{d.product}\"") + ")");
            if (device is Joystick && (what == "found" || what == "added" || what == "reconnected"))
            {
                Log($"[Input] \"{device.displayName}\" is a generic joystick, not a gamepad Unity recognises, so the game can't use it. " +
                    "On Steam, turn on Steam Input for the game; otherwise try the controller's X-input (Xbox) mode.");
                UnsupportedController?.Invoke(device.displayName);
            }
        }

        static void Log(string line)
        {
            LastLine = line;
            Debug.Log(line);
        }
    }
}
