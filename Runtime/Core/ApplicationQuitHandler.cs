/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Application Quit Handler
 * 
 * This file handles application quit and pause events to ensure proper cleanup
 * of running assessments, objectives, and interactions. It automatically closes
 * any open timed events when the application is terminated or paused.
 * 
 * Key Features:
 * - Automatic cleanup of running assessments and objectives
 * - Graceful handling of application pause/resume events
 * - Integration with AbxrLib timing system
 * - Prevents data loss during unexpected application termination
 */

using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Events;
using AbxrLib.Runtime.Logs;
using UnityEngine;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Handles application quit events to automatically close running Assessments, Objectives and Interactions
    /// This ensures data integrity and proper completion tracking when users force-quit or close the application
    /// </summary>
    public class ApplicationQuitHandler : MonoBehaviour
    {
        private void OnApplicationQuit()
        {
            Debug.Log("AbxrLib: Application quitting, automatically closing running events");
            CloseRunningEvents();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // On mobile platforms, OnApplicationPause(true) is often called instead of OnApplicationQuit
            if (pauseStatus)
            {
                Debug.Log("AbxrLib: Application paused, automatically closing running events");
                CloseRunningEvents();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Additional safety net for platforms where focus loss might indicate app termination
            if (!hasFocus)
            {
                Debug.Log("AbxrLib: Application lost focus, checking for running events");
                // Note: We don't automatically close on focus loss as this can happen during normal use
                // This is just for logging/debugging purposes
                DebugLogRunningEvents();
            }
        }

        /// <summary>
        /// Automatically complete all running Assessments, Objectives, and Interactions
        /// Uses Incomplete status to indicate the events were terminated due to application quit
        /// Processing order: Interactions → Objectives → Assessments (hierarchical order)
        /// </summary>
        private void CloseRunningEvents()
        {
            // Get references to the static dictionaries using safe public methods
            var runningAssessmentTimes = Abxr.GetAssessmentStartTimes();
            var runningObjectiveTimes = Abxr.GetObjectiveStartTimes();
            var runningInteractionTimes = Abxr.GetInteractionStartTimes();

            int totalClosed = 0;

            // Close running Interactions first (lowest level)
            if (runningInteractionTimes != null && runningInteractionTimes.Count > 0)
            {
                var interactionNames = new List<string>(runningInteractionTimes.Keys);
                foreach (string interactionName in interactionNames)
                {
                    Abxr.EventInteractionComplete(interactionName, Abxr.InteractionType.Null, Abxr.InteractionResult.Neutral, "",
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            // Close running Objectives second (middle level)
            if (runningObjectiveTimes != null && runningObjectiveTimes.Count > 0)
            {
                var objectiveNames = new List<string>(runningObjectiveTimes.Keys);
                foreach (string objectiveName in objectiveNames)
                {
                    Abxr.EventObjectiveComplete(objectiveName, 0, Abxr.EventStatus.Incomplete,
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            // Close running Assessments last (highest level)
            if (runningAssessmentTimes != null && runningAssessmentTimes.Count > 0)
            {
                var assessmentNames = new List<string>(runningAssessmentTimes.Keys);
                foreach (string assessmentName in assessmentNames)
                {
                    Abxr.EventAssessmentComplete(assessmentName, 0, Abxr.EventStatus.Fail, 
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            if (totalClosed > 0)
            {
                Debug.Log($"AbxrLib: Automatically closed {totalClosed} running events due to application quit");
                
                // Clear all start times since we've processed them
                Abxr.ClearAllStartTimes();
                
                // Force immediate send of all events with maximum redundancy for VR reliability
                CoroutineRunner.Instance.StartCoroutine(EventBatcher.Send());
                
                // Log the cleanup activity
                Abxr.Log($"Application quit handler closed {totalClosed} running events", Abxr.LogLevel.Info, 
                    new Dictionary<string, string> 
                    { 
                        ["events_closed"] = totalClosed.ToString(),
                        ["quit_handler"] = "automatic"
                    });
                    
                // Also force send logs
                CoroutineRunner.Instance.StartCoroutine(LogBatcher.Send());
            }
        }

        /// <summary>
        /// Log information about currently running events without closing them
        /// Used for debugging and monitoring purposes
        /// </summary>
        private void DebugLogRunningEvents()
        {
            var runningAssessmentTimes = Abxr.GetAssessmentStartTimes();
            var runningObjectiveTimes = Abxr.GetObjectiveStartTimes();
            var runningInteractionTimes = Abxr.GetInteractionStartTimes();

            int totalRunning = 0;
            if (runningAssessmentTimes != null) totalRunning += runningAssessmentTimes.Count;
            if (runningObjectiveTimes != null) totalRunning += runningObjectiveTimes.Count;
            if (runningInteractionTimes != null) totalRunning += runningInteractionTimes.Count;

            if (totalRunning > 0)
            {
                Debug.Log($"AbxrLib: Currently {totalRunning} events running (Assessments: {runningAssessmentTimes?.Count ?? 0}, Objectives: {runningObjectiveTimes?.Count ?? 0}, Interactions: {runningInteractionTimes?.Count ?? 0})");
            }
        }

    }
}
