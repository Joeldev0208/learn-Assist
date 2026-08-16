using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace learn_Assist.Services;

/// <summary>
/// Callback captured by <see cref="OAuthLoopbackListener"/>. Supports both
/// Clerk's native OAuth flow (<see cref="SessionId"/>) and a plain OAuth 2.0
/// authorization-code flow (<see cref="Code"/>/<see cref="State"/>) such as
/// Google Cloud OAuth.
/// </summary>
public record OAuthCallbackResult(string? SessionId, string? RotatingTokenNonce, string? Code = null, string? State = null);

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
    /// requests (favicon etc.), reads the OAuth query parameters, and answers
    /// so the browser tab can close.
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

            var qs = ctx.Request.QueryString;
            var sessionId = GetValue(qs, "created_session_id");
            var nonce = GetValue(qs, "rotating_token_nonce");
            var code = GetValue(qs, "code");
            var state = GetValue(qs, "state");
            var error = GetValue(qs, "error");

            if (!string.IsNullOrEmpty(error))
            {
                RespondOk(ctx);
                return null;
            }

            var body = Encoding.UTF8.GetBytes(
                "<html><body><h2>Sign-in complete. You can close this tab and return to learn-Assist.</h2></body></html>");
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.OutputStream.Close();

            if (sessionId is not null)
                return new OAuthCallbackResult(sessionId, nonce);
            if (code is not null)
                return new OAuthCallbackResult(null, null, code, state);

            return null;
        }
    }

    private static string? GetValue(System.Collections.Specialized.NameValueCollection qs, string key)
    {
        var value = qs[key];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static void RespondOk(HttpListenerContext ctx)
    {
        var body = Encoding.UTF8.GetBytes(
            "<html><body><h2>Sign-in failed. You can close this tab and return to learn-Assist.</h2></body></html>");
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        ctx.Response.ContentLength64 = body.Length;
        ctx.Response.OutputStream.Write(body);
        ctx.Response.OutputStream.Close();
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        ((IDisposable)_listener).Dispose();
    }
}