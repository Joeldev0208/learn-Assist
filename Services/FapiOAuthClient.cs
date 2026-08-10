using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace learn_Assist.Services;

/// <summary>
/// Thin client for Clerk's Frontend API (FAPI), used only for the native
/// OAuth (Google/Apple) flow. The publishable key authenticates here; the
/// session created by OAuth is later re-validated through the Backend API
/// (<see cref="ClerkAuthService"/>).
/// </summary>
public class FapiOAuthClient
{
    private readonly HttpClient _http = new();
    private readonly string _publishableKey;
    private readonly string _frontendApiUrl;

    public FapiOAuthClient(string publishableKey)
    {
        _publishableKey = publishableKey;
        _frontendApiUrl = $"https://{ResolveHost(publishableKey)}/v1";
    }

    /// <summary>
    /// Creates an OAuth <b>sign-in</b> attempt and returns the authorization
    /// URL to open in the system browser. Use this for the Login screen — it
    /// signs in an existing user (Clerk may auto-create the account when the
    /// Google email is new, depending on the instance settings).
    /// </summary>
    public Task<string> StartSignInAsync(string strategy, string redirectUrl)
        => StartAsync("sign_ins", strategy, redirectUrl);

    /// <summary>
    /// Creates an OAuth <b>sign-up</b> attempt and returns the authorization
    /// URL to open in the system browser. Use this for the Register screen —
    /// it always creates a new Clerk user for the OAuth identity.
    /// </summary>
    public Task<string> StartSignUpAsync(string strategy, string redirectUrl)
        => StartAsync("sign_ups", strategy, redirectUrl);

    private async Task<string> StartAsync(string endpoint, string strategy, string redirectUrl)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["strategy"] = strategy,
            ["redirect_url"] = redirectUrl,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_frontendApiUrl}/client/{endpoint}")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _publishableKey);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126 Safari/537.36");

        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var message = TryExtractError(body) ?? $"OAuth failed (HTTP {(int)response.StatusCode})";
            throw new InvalidOperationException(message);
        }

        return ExtractRedirectUrl(body);
    }

    private static string ExtractRedirectUrl(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // The redirect URL lives under different nested paths depending on the
        // endpoint (sign_ins vs sign_ups) and Clerk API version. Try the known
        // shapes, then fall back to a top-level field.
        string? url = TryGetDeep(root, "response", "first_factor_verification", "external_verification_redirect_url")
            ?? TryGetDeep(root, "response", "external_account_verification", "redirect_url")
            ?? TryGetDeep(root, "response", "external_verification_redirect_url");

        if (url is null &&
            root.TryGetProperty("external_verification_redirect_url", out var topRedirect) &&
            topRedirect.ValueKind == JsonValueKind.String)
        {
            url = topRedirect.GetString();
        }

        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException("Clerk did not return an OAuth redirect URL");

        return url!;
    }

    private static string? TryGetDeep(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                var e = errors[0];
                if (e.TryGetProperty("long_message", out var lm) && lm.ValueKind == JsonValueKind.String)
                    return lm.GetString();
                if (e.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    return m.GetString();
            }
        }
        catch (JsonException) { }
        return null;
    }

    private static string ResolveHost(string publishableKey)
    {
        var b64 = publishableKey.Replace("pk_test_", "").Replace('_', '/').Replace('-', '+');
        var len = b64.Length;
        var padded = (len % 4) switch
        {
            0 => b64,
            2 => b64 + "==",
            3 => b64 + "=",
            _ => b64,
        };
        var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        return raw.TrimEnd('$');
    }
}