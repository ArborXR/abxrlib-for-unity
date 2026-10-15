// Copyright (c) 2026 ArborXR. All rights reserved.
// PlayMode tests for the redeem client (SDK-60): every Send reports back exactly once, including a request that
// can't start and one the server never answers, against a local HttpListener so nothing leaves the machine.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using AbxrLib.Runtime.Services.Pairing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class PairingRedeemClientTests
{
    private GameObject _runnerObject;
    private Runner _runner;
    private HttpListener _listener;
    private string _url;
    private readonly List<PairingHttpResponse> _responses = new List<PairingHttpResponse>();

    private sealed class Runner : MonoBehaviour { }

    [SetUp]
    public void SetUp()
    {
        _responses.Clear();
        _runnerObject = new GameObject("PairingRedeemClientTests");
        _runner = _runnerObject.AddComponent<Runner>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_listener != null)
        {
            _listener.Close();
            _listener = null;
        }
        UnityEngine.Object.Destroy(_runnerObject);
    }

    [Test]
    public void Send_OnAnInactiveRunner_ReportsNoResponseRightAway()
    {
        _runnerObject.SetActive(false);
        var client = new UnityWebRequestPairingClient(_runner);

        client.Send("http://127.0.0.1:9/pairing", "{}", _responses.Add);

        Assert.AreEqual(1, _responses.Count, "A coroutine can't start on an inactive GameObject, so the client answers itself.");
        Assert.IsTrue(_responses[0].NetworkError);
        Assert.AreEqual(0, _responses[0].StatusCode);
    }

    [UnityTest]
    public IEnumerator Send_WhenTheRequestCantBeBuilt_ReportsNoResponse()
    {
        var client = new UnityWebRequestPairingClient(_runner);

        client.Send("http://127.0.0.1:9/pairing", null, _responses.Add);
        yield return WaitFor(() => _responses.Count > 0, 2f);

        Assert.AreEqual(1, _responses.Count);
        Assert.IsTrue(_responses[0].NetworkError);
        StringAssert.Contains("couldn't be sent", _responses[0].ErrorDetail);
    }

    [UnityTest]
    public IEnumerator Send_ToAServerThatNeverAnswers_GivesUpAtItsOwnDeadline()
    {
        StartListener(answer: false);
        var client = new UnityWebRequestPairingClient(_runner, answerDeadlineSeconds: 0.5);

        client.Send(_url, "{}", _responses.Add);
        yield return WaitFor(() => _responses.Count > 0, 4f);

        Assert.AreEqual(1, _responses.Count, "The client's own deadline answers before requestTimeoutSeconds, which is at least 5s.");
        Assert.IsTrue(_responses[0].NetworkError);
        StringAssert.Contains("no answer", _responses[0].ErrorDetail);

        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(1, _responses.Count, "Aborting the request doesn't report it a second time.");
    }

    [UnityTest]
    public IEnumerator Send_ToAServerThatAnswers_ReportsItsStatusBodyAndRetryAfter()
    {
        StartListener(answer: true);
        var client = new UnityWebRequestPairingClient(_runner);

        client.Send(_url, "{}", _responses.Add);
        yield return WaitFor(() => _responses.Count > 0, 5f);

        Assert.AreEqual(1, _responses.Count);
        Assert.AreEqual(429, _responses[0].StatusCode);
        Assert.AreEqual("{\"error\":\"Too many attempts\"}", _responses[0].Body);
        Assert.AreEqual("7", _responses[0].RetryAfter);
        Assert.IsFalse(_responses[0].NetworkError);
    }

    /// <summary>Listens on a free loopback port. With answer, each request gets a 429; without, requests hang until the listener closes.</summary>
    private void StartListener(bool answer)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _url = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_url);
        _listener.Start();
        _listener.BeginGetContext(OnContext, answer);
    }

    private void OnContext(IAsyncResult result)
    {
        HttpListener listener = _listener;
        if (listener == null || !listener.IsListening) return;
        HttpListenerContext context;
        try
        {
            context = listener.EndGetContext(result);
        }
        catch (Exception)
        {
            return; // Closed during TearDown.
        }
        if (!(bool)result.AsyncState) return;

        byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"Too many attempts\"}");
        context.Response.StatusCode = 429;
        context.Response.AddHeader("Retry-After", "7");
        context.Response.ContentType = "application/json";
        context.Response.OutputStream.Write(body, 0, body.Length);
        context.Response.Close();
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }
}
