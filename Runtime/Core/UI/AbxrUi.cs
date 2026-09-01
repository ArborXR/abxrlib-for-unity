
using System;

namespace AbxrLib.Runtime.Core.UI
{
    public static class AbxrUi
    {
        private static IAbxrAuthUi _authUi;
        private static IAbxrPollUi _pollUi;
        private static IAbxrQrScanner _qrScanner;
        private static Action _sceneObjectAttacher;
        private static Action _sceneChangedHandler;
        private static bool _warnedNoAuthUi;
        private static bool _warnedNoPollUi;

        /// <summary>The registered sign-in UI, or null when this project does not include the world-space objects.</summary>
        public static IAbxrAuthUi AuthUi => Alive(_authUi) ? _authUi : null;

        /// <summary>The registered exit-poll UI, or null when this project does not include the world-space objects.</summary>
        public static IAbxrPollUi PollUi => Alive(_pollUi) ? _pollUi : null;

        /// <summary>
        /// The registered scanner, but only while it reports itself usable: availability depends on the device,
        /// its permissions, and initialization, all of which change after registration. Null means "no QR right
        /// now", which is what every caller needs to know.
        /// </summary>
        public static IAbxrQrScanner QrScanner => Alive(_qrScanner) && _qrScanner.IsAvailable ? _qrScanner : null;

        /// <summary>
        /// Whether a registration can still be used. The fields are interface-typed, and an interface-typed null
        /// check never reaches UnityEngine.Object's overloaded equality - so a destroyed MonoBehaviour registrant
        /// would read as present, get handed to callers dead, and suppress the no-UI warnings because its slot
        /// looks occupied. Unwrapping to the Unity null check makes a destroyed registrant read as absent, which
        /// is both safe and what turns the warnings back on.
        /// </summary>
        private static bool Alive(object registration) =>
            registration != null && (!(registration is UnityEngine.Object obj) || obj != null);

        /// <summary>
        /// Authentication, as the UI sees it. Set by the subsystem once the auth service exists; the UI reads it
        /// when the user submits input.
        /// </summary>
        public static IAbxrAuthBridge AuthBridge { get; internal set; }

        public static void RegisterAuthUi(IAbxrAuthUi authUi)
        {
            WarnIfReplacing("sign-in UI", AuthUi, authUi);
            _authUi = authUi;
        }

        /// <summary>
        /// Registers the callback that creates the world-space scene objects. Kept separate from the interface
        /// registrations because the two happen at different times: registering is safe at SubsystemRegistration,
        /// while creating GameObjects has to wait for <see cref="Initialize"/> at BeforeSceneLoad - the same point
        /// they were created before they became optional.
        /// </summary>
        public static void RegisterSceneObjectAttacher(Action attach) => _sceneObjectAttacher = attach;

        /// <summary>Creates the world-space scene objects, or does nothing in a core-only project.</summary>
        internal static void AttachSceneObjects() => _sceneObjectAttacher?.Invoke();

        /// <summary>
        /// Registers a handler to be told when the active scene changes, so the world-space objects can drop
        /// references to objects that went away with the old scene.
        /// </summary>
        public static void RegisterSceneChangedHandler(Action onSceneChanged) => _sceneChangedHandler = onSceneChanged;

        internal static void RaiseSceneChanged() => _sceneChangedHandler?.Invoke();

        public static void RegisterPollUi(IAbxrPollUi pollUi)
        {
            WarnIfReplacing("poll UI", PollUi, pollUi);
            _pollUi = pollUi;
        }

        public static void RegisterQrScanner(IAbxrQrScanner qrScanner)
        {
            WarnIfReplacing("QR scanner", _qrScanner, qrScanner);
            _qrScanner = qrScanner;
        }

        /// <summary>
        /// Removes a registration, but only the caller's own - a late teardown must never clobber whatever
        /// registered after it. A MonoBehaviour-backed implementation should call this from OnDestroy: the
        /// registry treats a destroyed Unity object as absent on its own, but only the implementation can
        /// release the slot deliberately, for example when it is replaced mid-scene rather than destroyed.
        /// </summary>
        public static void UnregisterAuthUi(IAbxrAuthUi authUi)
        {
            if (ReferenceEquals(_authUi, authUi)) _authUi = null;
        }

        /// <summary>See <see cref="UnregisterAuthUi"/>: removes only the caller's own registration.</summary>
        public static void UnregisterPollUi(IAbxrPollUi pollUi)
        {
            if (ReferenceEquals(_pollUi, pollUi)) _pollUi = null;
        }

        /// <summary>See <see cref="UnregisterAuthUi"/>: removes only the caller's own registration.</summary>
        public static void UnregisterQrScanner(IAbxrQrScanner qrScanner)
        {
            if (ReferenceEquals(_qrScanner, qrScanner)) _qrScanner = null;
        }

        /// <summary>
        /// Registration is last-write-wins, and the order registrants load in is not guaranteed - so when both
        /// the world-space objects and the app register an implementation, which one wins is arbitrary and
        /// otherwise invisible. Saying so in the log is what turns "the wrong keyboard appeared" into a
        /// diagnosable state.
        /// </summary>
        private static void WarnIfReplacing(string what, object current, object incoming)
        {
            if (current == null || incoming == null || ReferenceEquals(current, incoming)) return;

            // The same type re-registering is not a conflict - it is the domain-reload-disabled Editor case:
            // statics survive between plays while the registrant runs again with a fresh instance. A real
            // sample-vs-app conflict always involves two different types.
            if (current.GetType() == incoming.GetType()) return;

            Logcat.Warning($"AbxrUi: replacing the registered {what} ({current.GetType().Name}) with " +
                           $"{incoming.GetType().Name}. Registration is last-write-wins and registration order is " +
                           "not guaranteed - if this project should use only one of these, remove the other " +
                           "registration.");
        }

        /// <summary>
        /// Explains the one situation where a core-only install goes quiet: the backend asked for user input,
        /// the app has not handled Abxr.OnInputRequested, and there is no UI to fall back on. Logged once - this
        /// is reached per authentication attempt, and repeating it would bury the rest of the log.
        /// </summary>
        internal static void WarnNoAuthUi(string authType)
        {
            if (_warnedNoAuthUi) return;
            _warnedNoAuthUi = true;

            Logcat.Warning($"AbxrLib needs the user to sign in (\"{authType}\"), but this project has no way to ask " +
                           "them: the world-space objects are not installed and nothing is handling " +
                           "Abxr.OnInputRequested.\n" +
                           "WHAT TO DO: either install the AbxrLib world-space objects (Analytics for XR > Setup " +
                           "Wizard > Project Setup) to use the built-in keyboard and PIN pad, or set " +
                           "Abxr.OnInputRequested to collect the value yourself and pass it to " +
                           "Abxr.OnInputSubmitted.");
        }

        /// <summary>
        /// The poll counterpart of <see cref="WarnNoAuthUi"/>: Abxr.PollUser was called with a valid poll, but no
        /// poll UI is registered, so the poll is dropped and its callback will never be invoked. Without this the
        /// drop is completely silent - invalid polls log errors, so the valid ones were the only quiet case.
        /// </summary>
        internal static void WarnNoPollUi()
        {
            if (_warnedNoPollUi) return;
            _warnedNoPollUi = true;

            Logcat.Warning("AbxrLib was asked to show a poll (Abxr.PollUser), but this project has no way to show " +
                           "it: the world-space objects are not installed and no poll UI is registered. The poll " +
                           "was dropped and its callback will not be invoked.\n" +
                           "WHAT TO DO: either install the AbxrLib world-space objects (Analytics for XR > Setup " +
                           "Wizard > Project Setup) to use the built-in poll UI, or register your own with " +
                           "AbxrUi.RegisterPollUi.");
        }

        /// <summary>Test hook: drops every registration so a test can exercise the core-only path.</summary>
        internal static void ResetForTesting()
        {
            _authUi = null;
            _pollUi = null;
            _qrScanner = null;
            AuthBridge = null;
            _sceneObjectAttacher = null;
            _sceneChangedHandler = null;
            _warnedNoAuthUi = false;
            _warnedNoPollUi = false;
        }
    }
}
