/*
 * Copyright (c) 2026 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - Record Observer
 *
 * A read-only view of the records AbxrLib creates (events, telemetry, logs, storage) and of what happened when it
 * sent them. It is a diagnostic API: use it to show or debug what the SDK records, not to drive app logic.
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbxrLib.Runtime.Core;

/// <summary>
/// Diagnostic, best-effort observer for the records AbxrLib creates and sends. Both events are raised on the main
/// thread. With no handler attached the SDK does no extra work: no ids, no copies.
/// </summary>
public static class AbxrObserver
{
	public enum RecordKind
	{
		Event,
		Telemetry,
		Log,
		Storage
	}

	/// <summary>Why a record was not kept, or why a sent record was dropped later.</summary>
	public enum DropReason
	{
		/// <summary>The record was kept.</summary>
		None,
		/// <summary>The app isn't paired with an organization and no pairing prompt is open, so the SDK isn't recording.</summary>
		NotRecording,
		/// <summary>The queue for this kind of record was full (Maximum Cached Items).</summary>
		QueueFull,
		/// <summary>Storage needs an authenticated session.</summary>
		NotAuthenticated,
		/// <summary>User-scoped storage needs a signed-in user.</summary>
		NoUser,
		/// <summary>The session ended (EndSession, StartNewSession, or a pairing change) before the record was sent.</summary>
		SessionEnded
	}

	public enum SendStatus
	{
		/// <summary>The backend accepted the batch.</summary>
		Sent,
		/// <summary>
		/// The batch failed after its retries. While the app runs it goes back in the queue, and any record that no longer
		/// fits gets a separate Dropped result. A batch sent while the app quits isn't queued again.
		/// </summary>
		Failed,
		/// <summary>The record went to the ArborXR client app, which sends it. The SDK can't see whether that send succeeds.</summary>
		HandedToService,
		/// <summary>The record was queued but will never be sent. DropReason says why.</summary>
		Dropped
	}

	/// <summary>One record, as the SDK built it.</summary>
	public readonly struct Record
	{
		/// <summary>Unique for this app run. Matches the ids in <see cref="SendResult.RecordIds"/>.</summary>
		public long Id { get; }
		public RecordKind Kind { get; }
		/// <summary>The Abxr method the app called, for example "EventAssessmentComplete". Null when <see cref="Automatic"/>.</summary>
		public string Method { get; }
		/// <summary>The event or telemetry name, the log message, or the storage entry name.</summary>
		public string Name { get; }
		/// <summary>For logs, the level: debug, info, warn, error, or critical. Null for other kinds.</summary>
		public string Level { get; }
		/// <summary>A copy of the metadata the app passed, before the SDK added anything. Empty when it passed none; null when <see cref="Automatic"/>.</summary>
		public IReadOnlyDictionary<string, string> CallerData { get; }
		/// <summary>The metadata as queued, including the fields the SDK added (scene, duration, super metadata, scores).</summary>
		public IReadOnlyDictionary<string, string> Data { get; }
		/// <summary>True when the SDK made the record itself, for example auto telemetry, scene events, or gaze telemetry.</summary>
		public bool Automatic { get; }
		/// <summary>None when the record was kept.</summary>
		public DropReason DropReason { get; }
		public bool Dropped => DropReason != DropReason.None;
		/// <summary>When the SDK created the record, in UTC.</summary>
		public DateTime Timestamp { get; }

		internal Record(long id, RecordKind kind, string method, string name, string level,
			IReadOnlyDictionary<string, string> callerData, IReadOnlyDictionary<string, string> data, bool automatic,
			DropReason dropReason, DateTime timestamp)
		{
			Id = id;
			Kind = kind;
			Method = method;
			Name = name;
			Level = level;
			CallerData = callerData;
			Data = data;
			Automatic = automatic;
			DropReason = dropReason;
			Timestamp = timestamp;
		}
	}

	/// <summary>What happened to a set of records the SDK sent, or tried to.</summary>
	public readonly struct SendResult
	{
		public IReadOnlyList<long> RecordIds { get; }
		public SendStatus Status { get; }
		/// <summary>The HTTP status of the last attempt, or 0 when there was no response or no HTTP request.</summary>
		public int HttpStatus { get; }
		/// <summary>The last attempt's error for Failed. Null otherwise.</summary>
		public string Error { get; }
		/// <summary>For Dropped, why. None otherwise.</summary>
		public DropReason DropReason { get; }

		internal SendResult(IReadOnlyList<long> recordIds, SendStatus status, int httpStatus, string error, DropReason dropReason)
		{
			RecordIds = recordIds;
			Status = status;
			HttpStatus = httpStatus;
			Error = error;
			DropReason = dropReason;
		}
	}

	/// <summary>Raised on the main thread for every record the SDK creates, including the ones it doesn't keep.</summary>
	public static event Action<Record> OnRecordCreated;

	/// <summary>Raised on the main thread when records are sent, fail to send, are handed to the ArborXR client app, or are dropped after being queued.</summary>
	public static event Action<SendResult> OnRecordsSent;

	// Main thread only, like the rest of the record path.
	private static long _lastId;
	private static bool _appScopeOpen;
	private static bool _appScopeTaken;
	private static RecordKind _appScopeKind;
	private static string _appScopeMethod;
	private static Dictionary<string, string> _appScopeCallerData;
	private static int _automaticDepth;

	/// <summary>True while a handler is attached to either event. Every other entry point is a no-op otherwise.</summary>
	internal static bool IsObserved => OnRecordCreated != null || OnRecordsSent != null;

	/// <summary>
	/// Opened by each Abxr record method so the record it makes reports the method name and what the app passed.
	/// A struct, and a no-op with no handler attached. Only the outermost scope counts, and none opens inside an
	/// <see cref="AutomaticScope"/>.
	/// </summary>
	internal readonly struct Scope : IDisposable
	{
		private const byte None = 0, App = 1, Auto = 2;
		private readonly byte _type;

		private Scope(byte type) => _type = type;

		internal static Scope ForApp() => new Scope(App);
		internal static Scope ForAutomatic() => new Scope(Auto);

		public void Dispose()
		{
			if (_type == App)
			{
				_appScopeOpen = false;
				_appScopeTaken = false;
				_appScopeMethod = null;
				_appScopeCallerData = null;
			}
			else if (_type == Auto)
			{
				_automaticDepth--;
			}
		}
	}

	internal static Scope AppScope(RecordKind kind, Dictionary<string, string> callerData, [CallerMemberName] string method = null)
	{
		if (!IsObserved || _appScopeOpen || _automaticDepth > 0) return default;
		_appScopeOpen = true;
		_appScopeTaken = false;
		_appScopeKind = kind;
		_appScopeMethod = method;
		_appScopeCallerData = callerData != null ? new Dictionary<string, string>(callerData) : new Dictionary<string, string>();
		return Scope.ForApp();
	}

	/// <summary>Wraps SDK code that calls the public Abxr API, so the records it makes report as automatic, even inside an app's call.</summary>
	internal static Scope AutomaticScope()
	{
		_automaticDepth++;
		return Scope.ForAutomatic();
	}

	/// <summary>The id for a new record. Call only while <see cref="IsObserved"/>; 0 means "not tracked" everywhere.</summary>
	internal static long NextId() => ++_lastId;

	/// <summary>
	/// Raises <see cref="OnRecordCreated"/>. The first record of the app scope's kind takes the scope; any other record
	/// (gaze telemetry before an event, location telemetry after it) reports as automatic.
	/// </summary>
	internal static void Created(long id, RecordKind kind, string name, string level, Dictionary<string, string> data, DropReason dropReason)
	{
		string method = null;
		Dictionary<string, string> callerData = null;
		bool automatic = true;
		if (_appScopeOpen && !_appScopeTaken && _automaticDepth == 0 && _appScopeKind == kind)
		{
			_appScopeTaken = true;
			method = _appScopeMethod;
			callerData = _appScopeCallerData;
			automatic = false;
		}

		var handler = OnRecordCreated;
		if (handler == null) return;
		var record = new Record(id, kind, method, name, level, callerData,
			data != null ? new Dictionary<string, string>(data) : new Dictionary<string, string>(),
			automatic, dropReason, DateTime.UtcNow);
		Raise(handler, record, "OnRecordCreated");
	}

	/// <summary>Raises <see cref="OnRecordsSent"/> for one record.</summary>
	internal static void Sent(long id, SendStatus status, int httpStatus = 0, string error = null, DropReason dropReason = DropReason.None)
	{
		if (id == 0 || OnRecordsSent == null) return;
		Sent(new List<long> { id }, status, httpStatus, error, dropReason);
	}

	/// <summary>Raises <see cref="OnRecordsSent"/>. Skipped when the list is empty, which is what a batch made while nobody observed produces.</summary>
	internal static void Sent(List<long> ids, SendStatus status, int httpStatus = 0, string error = null, DropReason dropReason = DropReason.None)
	{
		var handler = OnRecordsSent;
		if (handler == null || ids == null || ids.Count == 0) return;
		Raise(handler, new SendResult(ids.AsReadOnly(), status, httpStatus, error, dropReason), "OnRecordsSent");
	}

	/// <summary>Adds the id when it's tracked and there's a list (null while nobody observes). For building the id list of a batch.</summary>
	internal static void AddId(List<long> ids, long id)
	{
		if (id != 0) ids?.Add(id);
	}

	/// <summary>Calls each handler separately, so one that throws can't stop recording or the handlers after it.</summary>
	private static void Raise<T>(Action<T> handler, T value, string eventName)
	{
		foreach (var each in handler.GetInvocationList())
		{
			try { ((Action<T>)each)(value); }
			catch (Exception ex) { Logcat.Error($"An AbxrObserver.{eventName} handler threw: {ex.Message}"); }
		}
	}

	/// <summary>For testing only. Clears handlers, ids, and any scope a failed test left open.</summary>
	internal static void ResetForTesting()
	{
		OnRecordCreated = null;
		OnRecordsSent = null;
		_lastId = 0;
		_appScopeOpen = false;
		_appScopeTaken = false;
		_appScopeMethod = null;
		_appScopeCallerData = null;
		_automaticDepth = 0;
	}
}
