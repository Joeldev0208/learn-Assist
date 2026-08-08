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
}