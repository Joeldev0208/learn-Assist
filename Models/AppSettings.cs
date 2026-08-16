using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace learn_Assist.Models;

/// <summary>
/// Strongly-typed application settings, loaded from the <c>.env</c> file and
/// real environment variables (which take precedence), validated with
/// DataAnnotations. Equivalent to pydantic-settings in .NET.
/// </summary>
public class AppSettings
{
    public static AppSettings Current { get; set; } = new();

    [ConfigurationKeyName("CLERK_SECRET_KEY")]
    [Required(AllowEmptyStrings = false)]
    public string? ClerkSecretKey { get; set; }

    /// <summary>
    /// Clerk publishable key, used to talk to the Clerk Frontend API (FAPI)
    /// for the native OAuth (Google/Apple) flow. Optional: OAuth buttons are
    /// disabled when it is missing, email/password still works.
    /// </summary>
    [ConfigurationKeyName("CLERK_PUBLISHABLE_KEY")]
    public string? ClerkPublishableKey { get; set; }

    /// <summary>
    /// Loopback port the app listens on to capture the OAuth callback.
    /// Default 53174.
    /// </summary>
    [ConfigurationKeyName("OAUTH_REDIRECT_PORT")]
    [Range(1024, 65535)]
    public int OAuthRedirectPort { get; set; } = 53174;

    /// <summary>
    /// Google OAuth credentials from the Google Cloud Console. Optional:
    /// the Google sign-in buttons are hidden when either is missing,
    /// email/password and Clerk OAuth still work.
    /// </summary>
    [ConfigurationKeyName("GOOGLE_CLIENT_ID")]
    public string? GoogleClientId { get; set; }

    [ConfigurationKeyName("GOOGLE_CLIENT_SECRET")]
    public string? GoogleClientSecret { get; set; }

    /// <summary>
    /// Optional path to the OAuth client JSON downloaded from the Google Cloud
    /// Console (<c>client_secret_*.json</c>). Used to resolve the Google
    /// credentials when <c>GOOGLE_CLIENT_ID</c>/<c>GOOGLE_CLIENT_SECRET</c>
    /// are not set directly. Direct env vars take precedence over this file.
    /// </summary>
    [ConfigurationKeyName("GOOGLE_CLIENT_SECRET_FILE")]
    public string? GoogleClientSecretFile { get; set; }
}