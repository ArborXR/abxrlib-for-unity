using System.Collections.Generic;

public static partial class Abxr
{
	#region Mixpanel Compatibility Methods

	/// <summary>
	/// Mixpanel compatibility class for property values
	/// This class provides compatibility with Mixpanel Unity SDK for easier migration
	/// Usage: new Abxr.Value() instead of global Value class
	/// </summary>
	public class Value : Dictionary<string, object>
	{
		public Value() : base() { }
		
		public Value(IDictionary<string, object> dictionary) : base(dictionary) { }
		
		/// <summary>
		/// Converts Value metadata to Dictionary<string, string> for use with AbxrLib Event system
		/// </summary>
		/// <returns>Dictionary with all values converted to strings</returns>
		public Dictionary<string, string> ToDictionary()
		{
		var stringDictionary = new Dictionary<string, string>();
		foreach (var keyValuePair in this)
		{
			stringDictionary[keyValuePair.Key] = keyValuePair.Value?.ToString() ?? string.Empty;
		}
		return stringDictionary;
		}
	}

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
	/// <param name="eventProperties">Properties to send with the event (Abxr.Value format)</param>
	public static void Track(string eventName, Abxr.Value eventProperties)
	{
		Dictionary<string, string> metadata;
		
		if (eventProperties == null)
		{
			metadata = new Dictionary<string, string>
			{
				["AbxrMethod"] = "Track"
			};
		}
		else
		{
			metadata = eventProperties.ToDictionary();
			metadata["AbxrMethod"] = "Track";
		}
		
		Event(eventName, metadata);
	}

	/// <summary>
	/// Mixpanel compatibility method - tracks an event with properties as Dictionary
	/// This method provides additional flexibility for migration from Mixpanel Unity SDK
	/// Internally calls the AbxrLib Event method
	/// </summary>
	/// <param name="eventName">Name of the event to track</param>
	/// <param name="eventProperties">Properties to send with the event as Dictionary</param>
	public static void Track(string eventName, Dictionary<string, object> eventProperties)
	{
		Dictionary<string, string> stringProperties;
		
		if (eventProperties == null)
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
			
			foreach (var keyValuePair in eventProperties)
			{
				stringProperties[keyValuePair.Key] = keyValuePair.Value?.ToString() ?? string.Empty;
			}
		}
		
		Event(eventName, stringProperties);
	}

	#endregion

    #region Cognitive3D Compatibility Methods
	/// <summary>
	/// Cognitive3D compatibility class for custom events
	/// This class provides compatibility with Cognitive3D SDK for easier migration
	/// Usage: new Cognitive3D.CustomEvent("event_name").Send() instead of Cognitive3D SDK calls
	/// </summary>
	public class CustomEvent
	{
		private readonly string _eventName;
		private Dictionary<string, string> _properties;

		public CustomEvent(string name)
		{
			_eventName = name;
			_properties = new Dictionary<string, string>();
		}

		/// <summary>
		/// Set a property for this custom event
		/// </summary>
		/// <param name="key">Property key</param>
		/// <param name="value">Property value</param>
		/// <returns>This CustomEvent instance for method chaining</returns>
		public CustomEvent SetProperty(string key, object value)
		{
			_properties[key] = value?.ToString() ?? string.Empty;
			return this;
		}

		/// <summary>
		/// Send the custom event to AbxrLib
		/// </summary>
		public void Send()
		{
			var metadata = new Dictionary<string, string>(_properties)
			{
				["Cognitive3DMethod"] = "CustomEvent"
			};
			Event(_eventName, metadata);
		}
	}

	/// <summary>
	/// Start an event (maps to EventAssessmentStart for Cognitive3D compatibility)
	/// </summary>
	/// <param name="eventName">Name of the event to start</param>
	/// <param name="eventProperties">Optional properties for the event</param>
	public static void StartEvent(string eventName, Dictionary<string, object> eventProperties = null)
	{
		var metadata = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "StartEvent"
		};

		if (eventProperties != null)
		{
			foreach (var keyValuePair in eventProperties)
			{
				metadata[keyValuePair.Key] = keyValuePair.Value?.ToString() ?? string.Empty;
			}
		}

		EventAssessmentStart(eventName, metadata);
	}

	/// <summary>
	/// End an event (maps to EventAssessmentComplete for Cognitive3D compatibility)
	/// Attempts to convert Cognitive3D result formats to AbxrLib EventStatus
	/// </summary>
	/// <param name="eventName">Name of the event to end</param>
	/// <param name="eventResult">Event result (will attempt conversion to EventStatus)</param>
	/// <param name="eventScore">Optional score (defaults to 100)</param>
	/// <param name="eventProperties">Optional properties for the event</param>
	public static void EndEvent(string eventName, object eventResult = null, int eventScore = 100, Dictionary<string, object> eventProperties = null)
	{
		var metadata = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "EndEvent"
		};

		if (eventProperties != null)
		{
			foreach (var keyValuePair in eventProperties)
			{
				metadata[keyValuePair.Key] = keyValuePair.Value?.ToString() ?? string.Empty;
			}
		}

		// Convert result to EventStatus with best guess logic
		EventStatus eventStatus = EventStatus.Complete;
		if (eventResult != null)
		{
			string resultString = eventResult.ToString().ToLower();
			if (resultString.Contains("pass") || resultString.Contains("success") || resultString.Contains("complete") || resultString == "true" || resultString == "1")
			{
				eventStatus = EventStatus.Pass;
			}
			else if (resultString.Contains("fail") || resultString.Contains("error") || resultString == "false" || resultString == "0")
			{
				eventStatus = EventStatus.Fail;
			}
			else if (resultString.Contains("incomplete"))
			{
				eventStatus = EventStatus.Incomplete;
			}
			else if (resultString.Contains("browse"))
			{
				eventStatus = EventStatus.Browsed;
			}
		}

		EventAssessmentComplete(eventName, eventScore, eventStatus, metadata);
	}

	/// <summary>
	/// Send an event (maps to EventObjectiveComplete for Cognitive3D compatibility)
	/// </summary>
	/// <param name="eventName">Name of the event</param>
	/// <param name="eventProperties">Event properties</param>
	public static void SendEvent(string eventName, Dictionary<string, object> eventProperties = null)
	{
		var metadata = new Dictionary<string, string>
		{
			["Cognitive3DMethod"] = "SendEvent"
		};

		int eventScore = 100;
		EventStatus eventStatus = EventStatus.Complete;

		if (eventProperties != null)
		{
			foreach (var keyValuePair in eventProperties)
			{
				string propertyKey = keyValuePair.Key.ToLower();
				string propertyValue = keyValuePair.Value?.ToString() ?? string.Empty;
				
				// Extract score if provided
				if (propertyKey == "score" && int.TryParse(propertyValue, out int parsedScore))
				{
					eventScore = parsedScore;
				}
				// Extract status/result if provided
				else if (propertyKey == "result" || propertyKey == "status" || propertyKey == "success")
				{
					if (propertyValue.ToLower().Contains("pass") || propertyValue.ToLower().Contains("success") || propertyValue == "true" || propertyValue == "1")
					{
						eventStatus = EventStatus.Pass;
					}
					else if (propertyValue.ToLower().Contains("fail") || propertyValue.ToLower().Contains("error") || propertyValue == "false" || propertyValue == "0")
					{
						eventStatus = EventStatus.Fail;
					}
					else if (propertyValue.ToLower().Contains("incomplete"))
					{
						eventStatus = EventStatus.Incomplete;
					}
				}

				metadata[keyValuePair.Key] = propertyValue;
			}
		}

		EventObjectiveComplete(eventName, eventScore, eventStatus, metadata);
	}

	/// <summary>
	/// Set session property (maps to Register for Cognitive3D compatibility)
	/// </summary>
	/// <param name="propertyKey">Property key</param>
	/// <param name="propertyValue">Property value</param>
	public static void SetSessionProperty(string propertyKey, object propertyValue)
	{
		Register(propertyKey, propertyValue?.ToString() ?? string.Empty);
	}

	/// <summary>
	/// Set participant property (stub for Cognitive3D compatibility - not implemented)
	/// This method exists for string replacement compatibility but does not store data
	/// Use AbxrLib's authentication system and GetLearnerData() instead
	/// </summary>
	/// <param name="propertyKey">Property key (ignored)</param>
	/// <param name="propertyValue">Property value (ignored)</param>
	[System.Obsolete("SetParticipantProperty is not implemented. Use AbxrLib's authentication system and GetLearnerData() instead.")]
	public static void SetParticipantProperty(string propertyKey, object propertyValue)
	{
		// Intentionally empty stub for compatibility
		LogWarn($"SetParticipantProperty called but not implemented. Key: {propertyKey}, Value: {propertyValue}. Use AbxrLib authentication system instead.");
	}

	/// <summary>
	/// Get participant property (maps to GetLearnerData for Cognitive3D compatibility)
	/// </summary>
	/// <param name="propertyKey">Property key to retrieve</param>
	/// <returns>Property value if found, null otherwise</returns>
	public static object GetParticipantProperty(string propertyKey)
	{
		var userData = GetUserData();
		if (userData != null && userData.ContainsKey(propertyKey))
		{
			return userData[propertyKey];
		}
		return null;
	}
	#endregion
}