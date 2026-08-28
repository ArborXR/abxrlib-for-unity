/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 *
 * How the world-space objects introduce themselves to core.
 *
 * Everything core knows about the keyboard, PIN pad, exit poll, and QR scanner arrives through the
 * registrations below. Core calls the interfaces in Runtime/Core/UI and never names a type in this folder, so
 * these objects - and TextMeshPro, uGUI, XR Interaction Toolkit, and ZXing with them - can ship separately
 * from the core package.
 *
 * Two phases, because they have different timing requirements:
 *   SubsystemRegistration - handing over the implementations. No GameObjects, safe this early, and guaranteed
 *                           by Unity to run before BeforeSceneLoad, so core never looks for a UI that has not
 *                           registered yet.
 *   BeforeSceneLoad       - creating the scene objects, driven by Initialize through the registered callback so
 *                           they appear at exactly the point they did before they became optional.
 */

using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Core.UI;
using AbxrLib.Runtime.Types;
using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using AbxrLib.Runtime.Core.QRScanner;
#endif

namespace AbxrLib.Runtime.UI
{
    internal static class AbxrWorldSpaceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            AbxrUi.RegisterAuthUi(new KeyboardAuthUi());
            AbxrUi.RegisterPollUi(new ExitPollUi());
            AbxrUi.RegisterSceneObjectAttacher(AttachSceneObjects);
            AbxrUi.RegisterSceneChangedHandler(LaserPointerManager.OnSceneChanged);
        }

        private static void AttachSceneObjects()
        {
            ObjectAttacher.Attach<KeyboardHandler>("KeyboardHandler");
            ObjectAttacher.Attach<ExitPollHandler>("ExitPollHandler");

#if UNITY_ANDROID && !UNITY_EDITOR
#if PICO_SDK_3_4_OR_NEWER
            AbxrUi.RegisterQrScanner(ObjectAttacher.Attach<QRCodeReaderPico>("QRCodeReaderPico"));
#else
            AbxrUi.RegisterQrScanner(ObjectAttacher.Attach<QRCodeReaderMeta>("QRCodeReaderMeta"));
#endif
#endif
        }
    }

    /// <summary>Adapts the shipped keyboard and PIN pad, whose API is static, to the interface core calls.</summary>
    internal sealed class KeyboardAuthUi : IAbxrAuthUi
    {
        public void Show(AuthUiKind kind) => KeyboardHandler.Create(kind == AuthUiKind.PinPad
            ? KeyboardHandler.KeyboardType.PinPad
            : KeyboardHandler.KeyboardType.FullKeyboard);

        public void SetPrompt(string prompt) => KeyboardHandler.SetPrompt(prompt);

        public void Hide() => KeyboardHandler.Destroy();

        public void StopProcessing() => KeyboardHandler.StopProcessing();

        public void ShowPinPad() => KeyboardHandler.ShowPinPad();
    }

    /// <summary>Adapts the shipped exit poll to the interface core calls.</summary>
    internal sealed class ExitPollUi : IAbxrPollUi
    {
        public void AddPoll(string prompt, PollType pollType, List<string> responses, Action<string> callback) =>
            ExitPollHandler.AddPoll(prompt, pollType, responses, callback);
    }
}
