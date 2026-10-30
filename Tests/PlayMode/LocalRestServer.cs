// Copyright (c) 2026 ArborXR. All rights reserved.
// A local stand-in for lib-backend, so PlayMode tests pick the backend's answers. Point restUrl at Url.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// Answers every request with <see cref="StatusCode"/> (or what <see cref="Respond"/> returns) and keeps each request's
/// path and body. Runs on a listener thread, so handlers must be thread-safe.
/// </summary>
public sealed class LocalRestServer : IDisposable
{
    /// <summary>One answer: status, body, and optional Retry-After.</summary>
    public readonly struct Answer
    {
        public readonly int Status;
        public readonly string Body;
        public readonly string RetryAfter;

        public Answer(int status, string body = "{}", string retryAfter = null)
        {
            Status = status;
            Body = body;
            RetryAfter = retryAfter;
        }
    }

    private readonly HttpListener _listener = new HttpListener();
    private readonly ConcurrentQueue<(string path, string body)> _requests = new ConcurrentQueue<(string, string)>();
    private readonly ManualResetEventSlim _released = new ManualResetEventSlim(true);
    public string Url { get; }
    public volatile int StatusCode = 200;
    /// <summary>When set, picks the answer from the path and body. Null falls back to <see cref="StatusCode"/>.</summary>
    public volatile Func<string, string, Answer?> Respond;

    /// <summary>While true, requests are recorded but not answered, so a test can act while a send is in flight.</summary>
    public bool HoldResponses
    {
        get => !_released.IsSet;
        set { if (value) _released.Reset(); else _released.Set(); }
    }

    public LocalRestServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _listener.BeginGetContext(OnRequest, null);
    }

    public List<string> Bodies(string path) => _requests.Where(r => r.path == path).Select(r => r.body).ToList();

    public List<string> Paths() => _requests.Select(r => r.path).ToList();

    /// <summary>A sign-in answer with a token the SDK accepts (a JWT whose exp is an hour out).</summary>
    public static string AuthSuccessBody(string token = null, string secret = "replay-secret")
    {
        token ??= JwtWithExp(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        return "{\"token\":\"" + token + "\",\"secret\":\"" + secret + "\",\"userData\":{},\"appId\":\"12345678-1234-1234-1234-123456789012\",\"modules\":[]}";
    }

    public static string JwtWithExp(long exp)
    {
        string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}") + "." + B64("{\"exp\":" + exp + ",\"sid\":\"" + Guid.NewGuid() + "\"}") + ".sig";
    }

    private void OnRequest(IAsyncResult ar)
    {
        HttpListenerContext context;
        try { context = _listener.EndGetContext(ar); }
        catch { return; } // stopped
        try { _listener.BeginGetContext(OnRequest, null); } catch { /* stopped */ }

        string body;
        using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            body = reader.ReadToEnd();
        string path = context.Request.Url.AbsolutePath;
        _requests.Enqueue((path, body));
        _released.Wait(TimeSpan.FromSeconds(10));
        var answer = Respond?.Invoke(path, body) ?? new Answer(StatusCode);
        try
        {
            context.Response.StatusCode = answer.Status;
            if (answer.RetryAfter != null) context.Response.AddHeader("Retry-After", answer.RetryAfter);
            byte[] bytes = Encoding.UTF8.GetBytes(answer.Body ?? "{}");
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }
        catch { /* the client went away */ }
    }

    public void Dispose()
    {
        _released.Set();
        try { _listener.Stop(); _listener.Close(); } catch { /* already closed */ }
    }
}
