/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Application Quit Handler
 * 
 * This file handles application quit events to ensure proper cleanup
 * of running assessments, objectives, and interactions. It automatically closes
 * any open timed events when the application is terminated.
 * 
 * Key Features:
 * - Automatic cleanup of running assessments and objectives
 * - Integration with AbxrLib timing system
 * - Prevents data loss during unexpected application termination
 */

using System.Collections.Generic;
using AbxrLib.Runtime.Common;
using AbxrLib.Runtime.Data;
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
                // Clear all start times since we've processed them
                Abxr.ClearAllStartTimes();
                
                // Force immediate send of all events with maximum redundancy for VR reliability
                CoroutineRunner.Instance.StartCoroutine(DataBatcher.Send());
                
                // Log the cleanup activity
                Abxr.Log($"Application quit handler closed {totalClosed} running events", Abxr.LogLevel.Info, 
                    new Dictionary<string, string> 
                    { 
                        ["events_closed"] = totalClosed.ToString(),
                        ["quit_handler"] = "automatic"
                    });
                    
                // Also force send logs (now handled by DataBatcher)
                // CoroutineRunner.Instance.StartCoroutine(DataBatcher.Send()); // Already called above
            }
        }
    }
}
