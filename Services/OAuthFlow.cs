using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Orchestrates the native OAuth flow: FAPI creates a sign-in attempt, the
/// system browser opens the authorization URL, the loopback callback
/// captures the created session, and the Backend API adopts it.
/// </summary>
public class OAuthFlow
{
    private readonly IAuthService _auth;
    private readonly string? _publishableKey;

    public OAuthFlow(IAuthService auth)
    {
        _auth = auth;
        _publishableKey = AppSettings.Current.ClerkPublishableKey;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_publishableKey);

    /// <summary>
    /// Runs the OAuth sign-in/up flow for the given strategy and returns the
    /// adopted session (or an error).
    /// </summary>
    public async Task<AuthResult> SignInAsync(string strategy, int redirectPort, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return new AuthResult { Success = false, Error = "OAuth is not configured. Add CLERK_PUBLISHABLE_KEY to your .env file." };

        using var listener = new OAuthLoopbackListener(redirectPort);
        var fapi = new FapiOAuthClient(_publishableKey!);

        string authorizeUrl;
        try
        {
            authorizeUrl = await fapi.StartOAuthAsync(strategy, listener.RedirectUrl);
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }

        try
        {
            OpenBrowser(authorizeUrl);
        }
        catch (Exception)
        {
            return new AuthResult { Success = false, Error = "Could not open your browser. Please try again." };
        }

        var callback = await listener.WaitForCallbackAsync(cancellationToken);
        if (callback is null)
            return new AuthResult { Success = false, Error = "Sign-in was cancelled or timed out." };

        return await _auth.AdoptOAuthSessionAsync(callback.SessionId);
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                },
            };
            process.Start();
        }
        catch (Exception)
        {
            throw;
        }
    }
}