using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Direct Google Cloud OAuth 2.0 (authorization-code flow) using credentials
/// from the Google Cloud Console. Returns the verified Google profile; the
/// caller is responsible for turning it into a Clerk account/session through
/// <see cref="IAuthService.SignInWithGoogleAsync"/>.
/// </summary>
public class GoogleOAuthService
{
    private static readonly Uri GoogleAuthorizeUri = new("https://accounts.google.com/o/oauth2/v2/auth");
    private static readonly Uri GoogleTokenUri = new("https://oauth2.googleapis.com/token");
    private static readonly Uri GoogleUserInfoUri = new("https://openidconnect.googleapis.com/v1/userinfo");

    private readonly HttpClient _http = new();
    private readonly string? _clientId;
    private readonly string? _clientSecret;

    public GoogleOAuthService(AppSettings settings)
    {
        _clientId = settings.GoogleClientId;
        _clientSecret = settings.GoogleClientSecret;

        if (settings.GoogleClientSecretFile is { } path
            && (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
            && TryLoadFromJsonFile(path, out var fromFile))
        {
            _clientId ??= fromFile.ClientId;
            _clientSecret ??= fromFile.ClientSecret;
        }
    }

    /// <summary>
    /// Parses an OAuth client JSON downloaded from the Google Cloud Console
    /// (<c>client_secret_*.json</c>), extracting <c>client_id</c> and
    /// <c>client_secret</c> from the <c>installed</c> (desktop) or <c>web</c>
    /// section. Returns false when the file is missing or lacks credentials.
    /// </summary>
    private static bool TryLoadFromJsonFile(string path, out (string ClientId, string ClientSecret) credentials)
    {
        credentials = default;
        if (!System.IO.File.Exists(path))
            return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var root = doc.RootElement;

            foreach (var section in new[] { "installed", "web" })
            {
                if (!root.TryGetProperty(section, out var sec))
                    continue;

                var id = sec.TryGetProperty("client_id", out var idEl) ? idEl.GetString() : null;
                var secret = sec.TryGetProperty("client_secret", out var secretEl) ? secretEl.GetString() : null;

                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret))
                {
                    credentials = (id.Trim(), secret.Trim());
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    /// <summary>
    /// Runs the full flow: opens the browser at Google's consent screen, waits
    /// for the loopback callback, exchanges the code for tokens, fetches the
    /// user profile, and validates the <c>state</c>. Returns the verified
    /// <see cref="GoogleProfile"/> or throws.
    /// </summary>
    public async Task<GoogleProfile> SignInAsync(int redirectPort)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Google OAuth is not configured. Set GOOGLE_CLIENT_ID/GOOGLE_CLIENT_SECRET, or GOOGLE_CLIENT_SECRET_FILE pointing to the client_secret_*.json you downloaded from the Google Cloud Console.");

        using var listener = new OAuthLoopbackListener(redirectPort);
        var redirectUri = listener.RedirectUrl;
        var state = Guid.NewGuid().ToString("N");

        var authorizeUrl = BuildAuthorizeUrl(redirectUri, state);
        try
        {
            OpenBrowser(authorizeUrl);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Could not open your browser. Please try again.");
        }

        var callback = await listener.WaitForCallbackAsync();
        if (callback?.Code is null)
            throw new InvalidOperationException("Sign-in was cancelled or failed. Please try again.");

        if (callback.State != state)
            throw new InvalidOperationException("OAuth state mismatch. Please try again.");

        var token = await ExchangeCodeAsync(callback.Code, redirectUri);
        return await FetchProfileAsync(token);
    }

    private string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _clientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = "openid email profile";
        query["access_type"] = "online";
        query["state"] = state;

        var builder = new UriBuilder(GoogleAuthorizeUri) { Query = query.ToString() };
        return builder.ToString();
    }

    private async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });

        using var response = await _http.PostAsync(GoogleTokenUri, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to exchange OAuth code (HTTP {(int)response.StatusCode})");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl))
            throw new InvalidOperationException("OAuth token response missing access_token");

        return tokenEl.GetString()!;
    }

    private async Task<GoogleProfile> FetchProfileAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GoogleUserInfoUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to fetch Google profile (HTTP {(int)response.StatusCode})");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;

        var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var verified = root.TryGetProperty("email_verified", out var ve) && ve.ValueKind == System.Text.Json.JsonValueKind.True;

        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("Google profile missing email");

        if (!verified)
            throw new InvalidOperationException("Google email is not verified");

        return new GoogleProfile(email, name ?? string.Empty, verified);
    }

    private static void OpenBrowser(string url)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            },
        };
        process.Start();
    }

    public void Dispose() => _http.Dispose();
}