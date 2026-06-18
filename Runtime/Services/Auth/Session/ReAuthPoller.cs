using System;
using System.Collections;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>
    /// Watches token expiry and asks the auth service to run a normal auth refresh shortly before expiry.
    /// </summary>
    internal sealed class ReAuthPoller
    {
        private const float ReAuthPollSeconds = 60f;
        private const int ReAuthThresholdSeconds = 120;
        private static readonly WaitForSeconds ReAuthWait = new(ReAuthPollSeconds);

        private readonly MonoBehaviour _runner;
        private readonly AuthSessionState _sessionState;
        private readonly Func<bool> _isStopping;
        private readonly Func<bool> _isAttemptActive;
        private readonly Action _authenticate;
        private Coroutine _coroutine;

        internal ReAuthPoller(MonoBehaviour runner, AuthSessionState sessionState, Func<bool> isStopping,
            Func<bool> isAttemptActive, Action authenticate)
        {
            _runner = runner;
            _sessionState = sessionState;
            _isStopping = isStopping;
            _isAttemptActive = isAttemptActive;
            _authenticate = authenticate;
        }

        internal void Start()
        {
            Stop();
            if (_runner == null) return;
            _coroutine = _runner.StartCoroutine(PollCoroutine());
        }

        internal void Stop()
        {
            if (_coroutine != null && _runner != null)
            {
                _runner.StopCoroutine(_coroutine);
                _coroutine = null;
            }
        }

        internal IEnumerator PollCoroutine()
        {
            while (!IsStopping())
            {
                yield return ReAuthWait;
                TryTriggerReAuthIfNeeded();
            }
        }

        internal bool TryTriggerReAuthIfNeeded()
        {
            if (IsStopping()) return false;
            if (_sessionState.TokenExpiryUtc == DateTime.MinValue || IsAttemptActive()) return false;
            if (_sessionState.TokenExpiryUtc - DateTime.UtcNow > TimeSpan.FromSeconds(ReAuthThresholdSeconds))
                return false;
            if (_authenticate == null) return false;

            _authenticate.Invoke();
            return true;
        }

        private bool IsStopping() => _isStopping != null && _isStopping();
        private bool IsAttemptActive() => _isAttemptActive != null && _isAttemptActive();
    }
}
