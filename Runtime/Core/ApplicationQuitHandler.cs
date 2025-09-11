using System.Collections.Generic;
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
            Debug.Log("AbxrLib - Application quitting, automatically closing running events");
            CloseRunningEvents();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // On mobile platforms, OnApplicationPause(true) is often called instead of OnApplicationQuit
            if (pauseStatus)
            {
                Debug.Log("AbxrLib - Application paused, automatically closing running events");
                CloseRunningEvents();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Additional safety net for platforms where focus loss might indicate app termination
            if (!hasFocus)
            {
                Debug.Log("AbxrLib - Application lost focus, checking for running events");
                // Note: We don't automatically close on focus loss as this can happen during normal use
                // This is just for logging/debugging purposes
                LogRunningEvents();
            }
        }

        /// <summary>
        /// Automatically complete all running Assessments, Objectives, and Interactions
        /// Uses Incomplete status to indicate the events were terminated due to application quit
        /// Processing order: Interactions → Objectives → Assessments (hierarchical order)
        /// </summary>
        private void CloseRunningEvents()
        {
            // Get references to the static dictionaries using reflection to access private fields
            var assessmentStartTimes = GetStartTimesDictionary("AssessmentStartTimes");
            var objectiveStartTimes = GetStartTimesDictionary("ObjectiveStartTimes");
            var interactionStartTimes = GetStartTimesDictionary("InteractionStartTimes");

            int totalClosed = 0;

            // Close running Interactions first (lowest level)
            if (interactionStartTimes != null && interactionStartTimes.Count > 0)
            {
                var interactionNames = new List<string>(interactionStartTimes.Keys);
                foreach (string interactionName in interactionNames)
                {
                    Abxr.EventInteractionComplete(interactionName, Abxr.InteractionType.Null, "incomplete_quit",
                        new Dictionary<string, string> 
                        { 
                            ["quit_reason"] = "application_quit",
                            ["auto_closed"] = "true"
                        });
                    totalClosed++;
                }
            }

            // Close running Objectives second (middle level)
            if (objectiveStartTimes != null && objectiveStartTimes.Count > 0)
            {
                var objectiveNames = new List<string>(objectiveStartTimes.Keys);
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
            if (assessmentStartTimes != null && assessmentStartTimes.Count > 0)
            {
                var assessmentNames = new List<string>(assessmentStartTimes.Keys);
                foreach (string assessmentName in assessmentNames)
                {
                    Abxr.EventAssessmentComplete(assessmentName, 0, Abxr.EventStatus.Incomplete, 
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
                Debug.Log($"AbxrLib - Automatically closed {totalClosed} running events due to application quit");
                
                // Force immediate send of all events with maximum redundancy for VR reliability
                AbxrLib.Runtime.Events.EventBatcher.ForceImmediateSend();
                
                // Log the cleanup activity
                Abxr.Log($"Application quit handler closed {totalClosed} running events", Abxr.LogLevel.Info, 
                    new Dictionary<string, string> 
                    { 
                        ["events_closed"] = totalClosed.ToString(),
                        ["quit_handler"] = "automatic"
                    });
                    
                // Also force send logs
                AbxrLib.Runtime.Events.EventBatcher.ForceImmediateSend();
            }
        }

        /// <summary>
        /// Log information about currently running events without closing them
        /// Used for debugging and monitoring purposes
        /// </summary>
        private void LogRunningEvents()
        {
            var assessmentStartTimes = GetStartTimesDictionary("AssessmentStartTimes");
            var objectiveStartTimes = GetStartTimesDictionary("ObjectiveStartTimes");
            var interactionStartTimes = GetStartTimesDictionary("InteractionStartTimes");

            int totalRunning = 0;
            if (assessmentStartTimes != null) totalRunning += assessmentStartTimes.Count;
            if (objectiveStartTimes != null) totalRunning += objectiveStartTimes.Count;
            if (interactionStartTimes != null) totalRunning += interactionStartTimes.Count;

            if (totalRunning > 0)
            {
                Debug.Log($"AbxrLib - Currently {totalRunning} events running (Assessments: {assessmentStartTimes?.Count ?? 0}, Objectives: {objectiveStartTimes?.Count ?? 0}, Interactions: {interactionStartTimes?.Count ?? 0})");
            }
        }

        /// <summary>
        /// Use reflection to access the private static dictionaries from the Abxr class
        /// This is necessary because the timing dictionaries are private fields
        /// </summary>
        /// <param name="fieldName">Name of the static field to retrieve</param>
        /// <returns>Dictionary of event names to start times, or null if not found</returns>
        private Dictionary<string, System.DateTime> GetStartTimesDictionary(string fieldName)
        {
            try
            {
                var abxrType = typeof(Abxr);
                var field = abxrType.GetField(fieldName, 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (field != null)
                {
                    return field.GetValue(null) as Dictionary<string, System.DateTime>;
                }
                else
                {
                    Debug.LogWarning($"AbxrLib - Could not find field {fieldName} in Abxr class");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AbxrLib - Error accessing field {fieldName}: {ex.Message}");
            }
            
            return null;
        }
    }
}
