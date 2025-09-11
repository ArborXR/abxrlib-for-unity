using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace AbxrLib.Runtime.Common
{
    public class CoroutineRunner : MonoBehaviour
    {
        private static List<CoroutineRunner> _instances = new List<CoroutineRunner>();
        private static readonly object _lock = new object();
        private static System.Threading.Timer _backupTimer;
        private static int _currentInstanceIndex = 0;

        // Backup timer actions that run independent of Unity's Update cycle
        private static readonly Queue<System.Action> _backupActions = new Queue<System.Action>();
        private static readonly object _actionLock = new object();

        public static CoroutineRunner Instance
        {
            get
            {
                lock (_lock)
                {
                    // Clean up any null/destroyed instances
                    _instances.RemoveAll(instance => instance == null);
                    
                    // Create multiple instances for redundancy if we have fewer than 3
                    while (_instances.Count < 3)
                    {
                        CreateNewInstance();
                    }
                    
                    // Round-robin selection to distribute load
                    if (_instances.Count > 0)
                    {
                        _currentInstanceIndex = (_currentInstanceIndex + 1) % _instances.Count;
                        return _instances[_currentInstanceIndex];
                    }
                    
                    // Fallback - create new instance if all failed
                    return CreateNewInstance();
                }
            }
        }

        private static CoroutineRunner CreateNewInstance()
        {
            var go = new GameObject($"CoroutineRunner_{_instances.Count}");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<CoroutineRunner>();
            _instances.Add(runner);
            
            // Start backup timer on first instance creation
            if (_backupTimer == null)
            {
                _backupTimer = new System.Threading.Timer(ExecuteBackupActions, null, 
                    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            }
            
            return runner;
        }

        /// <summary>
        /// Schedule an action to run via backup timer if Update() cycle fails
        /// This runs on a background thread, independent of Unity's main thread
        /// </summary>
        public static void ScheduleBackupAction(System.Action action)
        {
            lock (_actionLock)
            {
                _backupActions.Enqueue(action);
            }
        }

        private static void ExecuteBackupActions(object state)
        {
            try
            {
                lock (_actionLock)
                {
                    while (_backupActions.Count > 0)
                    {
                        var action = _backupActions.Dequeue();
                        try
                        {
                            // Execute on main thread when possible
                            if (Instance != null)
                            {
                                Instance.StartCoroutine(ExecuteActionCoroutine(action));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"AbxrLib - Backup action failed: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib - Backup timer execution failed: {ex.Message}");
            }
        }

        private static IEnumerator ExecuteActionCoroutine(System.Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"AbxrLib - Action execution failed: {ex.Message}");
            }
            yield return null;
        }

        /// <summary>
        /// Health check - removes failed instances and ensures we have working runners
        /// </summary>
        public static void HealthCheck()
        {
            lock (_lock)
            {
                _instances.RemoveAll(instance => instance == null);
                if (_instances.Count == 0)
                {
                    Debug.LogWarning("AbxrLib - All CoroutineRunner instances failed, creating new ones");
                    CreateNewInstance();
                }
            }
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                _instances.Remove(this);
                Debug.Log($"AbxrLib - CoroutineRunner destroyed, {_instances.Count} remaining");
            }
        }

        /// <summary>
        /// Safely start a coroutine with automatic fallback to other instances
        /// </summary>
        public static Coroutine SafeStartCoroutine(IEnumerator routine)
        {
            try
            {
                return Instance.StartCoroutine(routine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AbxrLib - Primary coroutine start failed: {ex.Message}, trying fallback");
                
                // Try other instances
                lock (_lock)
                {
                    foreach (var runner in _instances)
                    {
                        if (runner != null)
                        {
                            try
                            {
                                return runner.StartCoroutine(routine);
                            }
                            catch
                            {
                                continue; // Try next instance
                            }
                        }
                    }
                }
                
                // Last resort - schedule as backup action
                ScheduleBackupAction(() => {
                    try 
                    {
                        Instance.StartCoroutine(routine);
                    }
                    catch (Exception backupEx)
                    {
                        Debug.LogError($"AbxrLib - All coroutine execution attempts failed: {backupEx.Message}");
                    }
                });
                
                return null;
            }
        }
    }
}