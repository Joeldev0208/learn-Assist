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
}