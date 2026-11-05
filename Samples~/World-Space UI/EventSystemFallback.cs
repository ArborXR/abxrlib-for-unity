/*
 * Copyright (c) 2026 ArborXR. All rights reserved.
 *
 * Gives the SDK's prompts an EventSystem when the scene has none.
 *
 * uGUI only delivers taps and clicks through an EventSystem, and a new scene doesn't have one. Without it the PIN pad,
 * keyboard and exit poll draw but ignore every tap. So when a prompt opens on a flat screen and no EventSystem is
 * enabled, this adds one with the input module the project's Active Input Handling supports.
 *
 * It must not outstay the prompt. uGUI makes the first EventSystem enabled current, and every other one skips its
 * Update, so a leftover one would beat the app's own EventSystem in a later scene. It removes itself when the last
 * prompt closes, or as soon as another EventSystem is enabled.
 *
 * On a headset it adds nothing: XR Interaction Toolkit's ray interactors create an EventSystem with an XRUIInputModule
 * themselves, and the pointer and touch modules can't drive rays.
 */

using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
#if XR_TOOLKIT_AVAILABLE
using UnityEngine.XR.Interaction.Toolkit.UI;
#elif ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace AbxrLib.Runtime.UI
{
    internal sealed class EventSystemFallback : MonoBehaviour
    {
        internal const string ObjectName = "AbxrLib EventSystem";

        /// <summary>How often to look for an EventSystem the app enabled while ours was up.</summary>
        private const float OtherEventSystemCheckSeconds = 1f;

        private static EventSystemFallback _instance;
        private static bool _warnedOnHeadset;

        private EventSystem _eventSystem;
        private float _nextOtherCheck;

        /// <summary>True while the SDK's own EventSystem is up.</summary>
        internal static bool IsActive => _instance != null;

        /// <summary>
        /// Adds an EventSystem for the SDK's prompts when none is enabled. Call it each time a prompt opens or survives
        /// a scene load. Does nothing when the app already has one.
        /// </summary>
        internal static void Ensure()
        {
            if (EventSystem.current != null || _instance != null) return;

            if (XRSettings.isDeviceActive)
            {
                if (_warnedOnHeadset) return;
                _warnedOnHeadset = true;
                Logcat.Warning("AbxrLib - The scene has no EventSystem, so the sign-in prompt can't take input. On a " +
                               "headset, your rig supplies one: XR Interaction Toolkit's ray interactors add it when UI " +
                               "interaction is on.");
                return;
            }

            var go = new GameObject(ObjectName);
            // The prompts outlive a scene load, so their EventSystem must too.
            DontDestroyOnLoad(go);
            var eventSystem = go.AddComponent<EventSystem>();
#if XR_TOOLKIT_AVAILABLE
            // Handles mouse and touch for either input backend. A ray interactor that turns on later attaches to
            // EventSystem.current and adds an XRUIInputModule if it has none, and two modules would fight.
            go.AddComponent<XRUIInputModule>();
#elif ENABLE_INPUT_SYSTEM
            // Input System alone, or Both. Assigns its default UI actions when enabled.
            go.AddComponent<InputSystemUIInputModule>();
#else
            // Input Manager (old) alone. StandaloneInputModule throws when only the Input System is active.
            go.AddComponent<StandaloneInputModule>();
#endif
            _instance = go.AddComponent<EventSystemFallback>();
            _instance._eventSystem = eventSystem;
            _instance._nextOtherCheck = Time.unscaledTime + OtherEventSystemCheckSeconds;
            Logcat.Debug("AbxrLib - Added an EventSystem so the sign-in prompt can take input. It's removed when the prompt closes.");
        }

        /// <summary>
        /// A prompt that survives a scene load loses an EventSystem that lived in the old scene. Waits a frame, so the
        /// old scene's EventSystem is gone and the new scene's is enabled before deciding.
        /// </summary>
        internal static void OnSceneChanged()
        {
            if (!AnyPromptOpen) return;
            MonoBehaviour runner = ExitPollHandler.Instance;
            if (runner != null && runner.isActiveAndEnabled) runner.StartCoroutine(EnsureAfterAFrame());
            else Ensure();
        }

        private static System.Collections.IEnumerator EnsureAfterAFrame()
        {
            yield return null;
            if (AnyPromptOpen) Ensure();
        }

        private static bool AnyPromptOpen => KeyboardHandler.IsOpen || ExitPollHandler.IsOpen;

        private void Update()
        {
            if (!AnyPromptOpen)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.unscaledTime < _nextOtherCheck) return;
            _nextOtherCheck = Time.unscaledTime + OtherEventSystemCheckSeconds;
            if (AnotherEventSystemEnabled()) Destroy(gameObject);
        }

        private bool AnotherEventSystemEnabled()
        {
            foreach (var eventSystem in Utils.FindObjects<EventSystem>())
            {
                if (eventSystem != _eventSystem && eventSystem.isActiveAndEnabled) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Clears what an Editor play session left behind when domain reload is off.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _warnedOnHeadset = false;
        }
    }
}
