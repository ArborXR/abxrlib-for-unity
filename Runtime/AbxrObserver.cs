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
		/// The batch failed after its retries. Usually it goes back in the queue, and any record that no longer fits gets a
		/// separate Dropped result. A batch sent as the session ends (EndSession, or the app quitting) isn't queued again,
		/// so Failed is its last result.
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
	private static ScopeState _scope;
	private static bool _raising;
	/// <summary>Results raised while a handler runs, delivered after it returns.</summary>
	private static readonly Queue<Action> _deferred = new Queue<Action>();

	/// <summary>Who is recording right now, as the scopes below set it.</summary>
	internal struct ScopeState
	{
		public bool AppOpen;
		public bool AppTaken;
		public RecordKind AppKind;
		public string AppMethod;
		public Dictionary<string, string> AppCallerData;
		public int AutomaticDepth;
	}

	/// <summary>
	/// True while a handler is attached to either event and no handler is running: the gate for tracking a new record
	/// (an id, a scope, a copy). The records a handler makes (a handler that logs, say) are kept but never tracked, so
	/// they can't loop.
	/// </summary>
	internal static bool IsObserved => !_raising && (OnRecordCreated != null || OnRecordsSent != null);

	/// <summary>
	/// True while a send handler is attached. The gate for collecting the ids of a batch: records tracked earlier still
	/// get their result when it happens inside a handler (one that ends the session, say).
	/// </summary>
	internal static bool HasSendHandler => OnRecordsSent != null;

	/// <summary>Restores the scope state it saved when disposed. A struct; the default one does nothing.</summary>
	internal readonly struct Scope : IDisposable
	{
		private readonly bool _active;
		private readonly ScopeState _saved;

		internal Scope(ScopeState saved)
		{
			_active = true;
			_saved = saved;
		}

		public void Dispose()
		{
			if (_active) _scope = _saved;
		}
	}

	/// <summary>
	/// Opened by each Abxr record method so the record it makes reports the method name and what the app passed.
	/// A no-op with no handler attached. Only the outermost scope counts, so an overload that delegates reports the
	/// method the app called, and none opens inside an <see cref="AutomaticScope"/>.
	/// </summary>
	internal static Scope AppScope(RecordKind kind, Dictionary<string, string> callerData, [CallerMemberName] string method = null)
	{
		if (!IsObserved || _scope.AppOpen || _scope.AutomaticDepth > 0) return default;
		var saved = _scope;
		_scope.AppOpen = true;
		_scope.AppTaken = false;
		_scope.AppKind = kind;
		_scope.AppMethod = method;
		_scope.AppCallerData = callerData != null ? new Dictionary<string, string>(callerData) : new Dictionary<string, string>();
		return new Scope(saved);
	}

	/// <summary>Wraps SDK code that calls the public Abxr API, so the records it makes report as automatic, even inside an app's call.</summary>
	internal static Scope AutomaticScope()
	{
		var saved = _scope;
		_scope.AutomaticDepth++;
		return new Scope(saved);
	}

	/// <summary>
	/// Wraps a call into app code made while a record is being created (OnModuleTarget from EventAssessmentComplete),
	/// so what the app records there gets its own scope instead of the SDK's or the outer call's.
	/// </summary>
	internal static Scope AppCodeScope()
	{
		var saved = _scope;
		_scope = default;
		return new Scope(saved);
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
		if (_scope.AppOpen && !_scope.AppTaken && _scope.AutomaticDepth == 0 && _scope.AppKind == kind)
		{
			_scope.AppTaken = true;
			method = _scope.AppMethod;
			callerData = _scope.AppCallerData;
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

	/// <summary>
	/// Calls the handlers. A result raised while a handler runs waits until it returns, so handlers never nest, and
	/// it can't loop: only records tracked before the handler ran have ids to report.
	/// </summary>
	private static void Raise<T>(Action<T> handler, T value, string eventName)
	{
		if (_raising)
		{
			_deferred.Enqueue(() => Invoke(handler, value, eventName));
			return;
		}
		_raising = true;
		try
		{
			Invoke(handler, value, eventName);
			while (_deferred.Count > 0) _deferred.Dequeue()();
		}
		finally
		{
			_raising = false;
			_deferred.Clear();
		}
	}

	/// <summary>Calls each handler separately, so one that throws can't stop recording or the handlers after it.</summary>
	private static void Invoke<T>(Action<T> handler, T value, string eventName)
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
		_scope = default;
		_raising = false;
		_deferred.Clear();
	}
}
