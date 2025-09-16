using System.Collections.Generic;

public static partial class Abxr
{
    #region Cognitive3D Compatibility Methods
	/// <summary>
	/// Cognitive3D compatibility class for custom events
	/// This class provides compatibility with Cognitive3D SDK for easier migration
	/// Usage: new Cognitive3D.CustomEvent("event_name").Send() instead of Cognitive3D SDK calls
	/// </summary>
	public class CustomEvent
	{
		private readonly string eventName;
		private Dictionary<string, string> properties;

		public CustomEvent(string name)
		{
			eventName = name;
			properties = new Dictionary<string, string>();
		}

		/// <summary>
		/// Set a property for this custom event
		/// </summary>
		/// <param name="key">Property key</param>
		/// <param name="value">Property value</param>
		/// <returns>This CustomEvent instance for method chaining</returns>
		public CustomEvent SetProperty(string key, object value)
		{
			properties[key] = value?.ToString() ?? string.Empty;
			return this;
		}

		/// <summary>
		/// Send the custom event to AbxrLib
		/// </summary>
		public void Send()
		{
			var meta = new Dictionary<string, string>(properties)
			{
				["Cognitive3DMethod"] = "CustomEvent"
			};
			Event(eventName, meta);
		}
	}

	/// <summary>
	/// Start an event (maps to EventAssessmentStart for Cognitive3D compatibility)
	/// </summary>
	/// <param name="eventName">Name of the event to start</param>
	/// <param name="properties">Optional properties for the event</param>
	public static void StartEvent(string eventName, Dictionary<string, object> properties = null)
	{
		var meta = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "StartEvent"
		};

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				meta[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
		}

		EventAssessmentStart(eventName, meta);
	}

	/// <summary>
	/// End an event (maps to EventAssessmentComplete for Cognitive3D compatibility)
	/// Attempts to convert Cognitive3D result formats to AbxrLib EventStatus
	/// </summary>
	/// <param name="eventName">Name of the event to end</param>
	/// <param name="result">Event result (will attempt conversion to EventStatus)</param>
	/// <param name="score">Optional score (defaults to 100)</param>
	/// <param name="properties">Optional properties for the event</param>
	public static void EndEvent(string eventName, object result = null, int score = 100, Dictionary<string, object> properties = null)
	{
		var meta = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "EndEvent"
		};

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				meta[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
		}

		// Convert result to EventStatus with best guess logic
		EventStatus status = EventStatus.Complete;
		if (result != null)
		{
			string resultStr = result.ToString().ToLower();
			if (resultStr.Contains("pass") || resultStr.Contains("success") || resultStr.Contains("complete") || resultStr == "true" || resultStr == "1")
			{
				status = EventStatus.Pass;
			}
			else if (resultStr.Contains("fail") || resultStr.Contains("error") || resultStr == "false" || resultStr == "0")
			{
				status = EventStatus.Fail;
			}
			else if (resultStr.Contains("incomplete"))
			{
				status = EventStatus.Incomplete;
			}
			else if (resultStr.Contains("browse"))
			{
				status = EventStatus.Browsed;
			}
		}

		EventAssessmentComplete(eventName, score, status, meta);
	}

	/// <summary>
	/// Send an event (maps to EventObjectiveComplete for Cognitive3D compatibility)
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="properties">Event properties</param>
	public static void SendEvent(string eventName, Dictionary<string, object> properties = null)
	{
		var meta = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "SendEvent"
		};

		int score = 100;
		EventStatus status = EventStatus.Complete;

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				string key = kvp.Key.ToLower();
				string value = kvp.Value?.ToString() ?? string.Empty;
				
				// Extract score if provided
				if (key == "score" && int.TryParse(value, out int parsedScore))
				{
					score = parsedScore;
				}
				// Extract status/result if provided
				else if (key == "result" || key == "status" || key == "success")
				{
					if (value.ToLower().Contains("pass") || value.ToLower().Contains("success") || value == "true" || value == "1")
					{
						status = EventStatus.Pass;
					}
					else if (value.ToLower().Contains("fail") || value.ToLower().Contains("error") || value == "false" || value == "0")
					{
						status = EventStatus.Fail;
					}
					else if (value.ToLower().Contains("incomplete"))
					{
						status = EventStatus.Incomplete;
					}
				}

				meta[kvp.Key] = value;
			}
		}

		EventObjectiveComplete(eventName, score, status, meta);
	}

	/// <summary>
	/// Set session property (maps to Register for Cognitive3D compatibility)
	/// </summary>
	/// <param name="key">Property key</param>
	/// <param name="value">Property value</param>
	public static void SetSessionProperty(string key, object value)
	{
		Register(key, value?.ToString() ?? string.Empty);
	}

	/// <summary>
	/// Set participant property (stub for Cognitive3D compatibility - not implemented)
	/// This method exists for string replacement compatibility but does not store data
	/// Use AbxrLib's authentication system and GetLearnerData() instead
	/// </summary>
	/// <param name="key">Property key (ignored)</param>
	/// <param name="value">Property value (ignored)</param>
	[System.Obsolete("SetParticipantProperty is not implemented. Use AbxrLib's authentication system and GetLearnerData() instead.")]
	public static void SetParticipantProperty(string key, object value)
	{
		// Intentionally empty stub for compatibility
		LogWarn($"SetParticipantProperty called but not implemented. Key: {key}, Value: {value}. Use AbxrLib authentication system instead.");
	}

	/// <summary>
	/// Get participant property (maps to GetLearnerData for Cognitive3D compatibility)
	/// </summary>
	/// <param name="key">Property key to retrieve</param>
	/// <returns>Property value if found, null otherwise</returns>
	public static object GetParticipantProperty(string key)
	{
		var learnerData = GetUserData();
		if (learnerData != null && learnerData.ContainsKey(key))
		{
			return learnerData[key];
		}
		return null;
	}
	#endregion

	#region Mixpanel Compatibility Methods
	/// <summary>
	/// Mixpanel compatibility method - tracks an event with just a name
	/// This method provides compatibility with Mixpanel Unity SDK for easier migration
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	public static void Track(string eventName)
	{
		var meta = new Dictionary<string, string>
		{
			["AbxrMethod"] = "Track"
		};
		Event(eventName, meta);
	}

	/// <summary>
	/// Mixpanel compatibility method - tracks an event with properties
	/// This method provides compatibility with Mixpanel Unity SDK for easier migration
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	/// <param name="properties">Properties to send with the event (Abxr.Value format)</param>
	public static void Track(string eventName, Abxr.Value properties)
	{
		Dictionary<string, string> meta;
		
		if (properties == null)
		{
			meta = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
		}
		else
		{
			meta = properties.ToDictionary();
			meta["AbxrMethod"] = "Track";
		}
		
		Event(eventName, meta);
	}

	/// <summary>
	/// Mixpanel compatibility method - tracks an event with properties as Dictionary
	/// This method provides additional flexibility for migration from Mixpanel Unity SDK
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	/// <param name="properties">Properties to send with the event as Dictionary</param>
	public static void Track(string eventName, Dictionary<string, object> properties)
	{
		Dictionary<string, string> stringProperties;
		
		if (properties == null)
		{
			stringProperties = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
		}
		else
		{
			stringProperties = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
			
			foreach (var kvp in properties)
			{
				stringProperties[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
			}
		}
		
		Event(eventName, stringProperties);
	}
	#endregion
}