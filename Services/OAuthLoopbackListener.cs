using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace learn_Assist.Services;

public record OAuthCallbackResult(string SessionId, string? RotatingTokenNonce);

/// <summary>
/// Temporary localhost HTTP listener that captures the OAuth callback
/// (RFC 8252 loopback redirect for native apps). Listens only until the
/// callback arrives or the timeout elapses.
/// </summary>
public class OAuthLoopbackListener : IDisposable
{
    private readonly HttpListener _listener;
    private readonly int _port;

    public OAuthLoopbackListener(int port)
    {
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
    }

    public string RedirectUrl => $"http://127.0.0.1:{_port}/callback";

    /// <summary>
    /// Waits for the browser callback on /callback, ignoring other browser
    /// requests (favicon etc.), reads <c>created_session_id</c> (and
    /// <c>rotating_token_nonce</c>) from the query string, and answers so the
    /// browser tab can close.
    /// </summary>
    public async Task<OAuthCallbackResult?> WaitForCallbackAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpListenerContext? ctx;
            try { ctx = await _listener.GetContextAsync().WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { return null; }

            if (ctx.Request.Url?.AbsolutePath != "/callback")
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.OutputStream.Close();
                continue;
            }

            string? sessionId = null;
            string? nonce = null;

            if (ctx.Request.QueryString["created_session_id"] is { } sid && !string.IsNullOrEmpty(sid))
                sessionId = sid;
            if (ctx.Request.QueryString["rotating_token_nonce"] is { } n && !string.IsNullOrEmpty(n))
                nonce = n;

            var body = Encoding.UTF8.GetBytes(
                "<html><body><h2>Sign-in complete. You can close this tab and return to learn-Assist.</h2></body></html>");
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.OutputStream.Close();

            return sessionId is null ? null : new OAuthCallbackResult(sessionId, nonce);
        }
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        ((IDisposable)_listener).Dispose();
    }
}