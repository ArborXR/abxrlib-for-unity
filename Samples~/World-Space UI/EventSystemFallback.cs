/*
 * Copyright (c) 2026 ArborXR. All rights reserved.
 *
 * Gives the SDK's prompts an EventSystem when the scene has none.
 *
 * uGUI only delivers taps and clicks through an EventSystem, and a new scene doesn't have one. Without it the PIN pad,
 * keyboard and exit poll draw but ignore every tap. So when a prompt opens on a flat screen and no EventSystem is
 * enabled, this adds one with an input module the project's input handling supports.
 *
 * It belongs to the scene it was added to, not to the prompt. uGUI makes the first EventSystem enabled current, and
 * XR Interaction Toolkit's interactors attach to whichever one is current when they turn on. One that followed the
 * prompt into the next scene would be current while that scene's rig turned on, and the rig's rays would be left
 * attached to nothing once it went. So it is left in the scene, unloads with it, and is added again in the next scene
 * only if that one has none either. It also steps aside as soon as the app enables an EventSystem of its own.
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
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
#if XR_TOOLKIT_AVAILABLE
using UnityEngine.XR.Interaction.Toolkit.UI;
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
#if XR_TOOLKIT_AVAILABLE && ENABLE_INPUT_SYSTEM
        private DefaultInputActions _uiActions;
        private InputActionReference[] _uiActionReferences;
#endif

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

            // Built inactive, so the input module enables with its actions already assigned.
            var go = new GameObject(ObjectName);
            go.SetActive(false);
            var eventSystem = go.AddComponent<EventSystem>();
            var fallback = go.AddComponent<EventSystemFallback>();
            fallback._eventSystem = eventSystem;
#if XR_TOOLKIT_AVAILABLE
            // XR Interaction Toolkit's interactors reuse the XRUIInputModule on the current EventSystem, or add one
            // beside any other module, and two modules on one EventSystem fight. So with the toolkit installed, use its
            // module and let a rig that turns on later share it.
            var module = go.AddComponent<XRUIInputModule>();
#if ENABLE_INPUT_SYSTEM
            fallback.AssignDefaultUiActions(module);
#endif
#elif ENABLE_INPUT_SYSTEM
            // Input System alone, or Both. Assigns its default UI actions when enabled.
            go.AddComponent<InputSystemUIInputModule>();
#else
            // Input Manager (old) alone. StandaloneInputModule throws when only the Input System is active.
            go.AddComponent<StandaloneInputModule>();
#endif
            _instance = fallback;
            fallback._nextOtherCheck = Time.unscaledTime + OtherEventSystemCheckSeconds;
            go.SetActive(true);
            Logcat.Debug("AbxrLib - Added an EventSystem to this scene so the sign-in prompt can take input.");
        }

#if XR_TOOLKIT_AVAILABLE && ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Without action references, XRUIInputModule falls back to reading devices directly, and that reads a touch's
        /// position but never its press, so taps on a touch screen do nothing. The Input System's default UI actions
        /// bind a touch press as Click, alongside the mouse, pen and gamepad.
        /// </summary>
        private void AssignDefaultUiActions(XRUIInputModule module)
        {
            _uiActions = new DefaultInputActions();
            var ui = _uiActions.UI;
            _uiActionReferences = new[]
            {
                module.pointAction = InputActionReference.Create(ui.Point),
                module.leftClickAction = InputActionReference.Create(ui.Click),
                module.middleClickAction = InputActionReference.Create(ui.MiddleClick),
                module.rightClickAction = InputActionReference.Create(ui.RightClick),
                module.scrollWheelAction = InputActionReference.Create(ui.ScrollWheel),
                module.navigateAction = InputActionReference.Create(ui.Navigate),
                module.submitAction = InputActionReference.Create(ui.Submit),
                module.cancelAction = InputActionReference.Create(ui.Cancel),
            };
        }
#endif

        /// <summary>
        /// A prompt that survives a scene load loses the EventSystem that lived in the old scene, ours included. Waits a
        /// frame, so the new scene's own EventSystem (or the one its rig creates) is enabled before deciding.
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
            if (Time.unscaledTime < _nextOtherCheck) return;
            _nextOtherCheck = Time.unscaledTime + OtherEventSystemCheckSeconds;
            if (AnotherEventSystemEnabled()) Retire();
        }

        private bool AnotherEventSystemEnabled()
        {
            foreach (var eventSystem in Utils.FindObjects<EventSystem>())
            {
                if (eventSystem != _eventSystem && eventSystem.isActiveAndEnabled) return true;
            }
            return false;
        }

        /// <summary>
        /// Steps aside for the app's EventSystem. Destroy waits for the end of the frame, so ours is disabled and
        /// forgotten now: a prompt opened later this frame must not find it.
        /// </summary>
        private void Retire()
        {
            if (_instance == this) _instance = null;
            if (_eventSystem != null) _eventSystem.enabled = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
#if XR_TOOLKIT_AVAILABLE && ENABLE_INPUT_SYSTEM
            if (_uiActionReferences != null)
            {
                foreach (var reference in _uiActionReferences)
                {
                    if (reference != null) Destroy(reference);
                }
            }
            _uiActions?.Dispose();
#endif
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
