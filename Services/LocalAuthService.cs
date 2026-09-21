using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Local-accounts auth: users live in a JSON file on this PC
/// (<c>users.json</c> next to the encrypted AI config), passwords stored as
/// PBKDF2-SHA256 hashes (never plaintext). No cloud, no email verification.
/// <para/>
/// <c>ClerkAuthService</c> is kept in the repo unused so Clerk can be
/// re-enabled later for a cloud feature.
/// </summary>
public class LocalAuthService : IAuthService
{
    private const int Iterations = 210_000;

    private sealed class LocalUser
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private readonly object _lock = new();
    private UserSession? _currentUser;

    public bool IsAuthenticated => _currentUser is not null;

    public UserSession? CurrentUser => _currentUser;

    public Task<AuthResult> SignUpAsync(string email, string password)
    {
        email = email.Trim();

        lock (_lock)
        {
            var users = LoadUsers();

            if (users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(new AuthResult { Success = false, Error = "An account with this email already exists" });

            var user = new LocalUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = email,
                PasswordHash = HashPassword(password),
                HasPassword = true,
                CreatedAt = DateTime.UtcNow,
            };
            users.Add(user);
            SaveUsers(users);

            return Task.FromResult(ToSessionResult(user));
        }
    }

    public Task<AuthResult> SignInAsync(string email, string password)
    {
        email = email.Trim();

        lock (_lock)
        {
            var user = LoadUsers().FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

            if (user is null || !user.HasPassword || !VerifyPassword(password, user.PasswordHash))
                return Task.FromResult(new AuthResult { Success = false, Error = "Invalid email or password" });

            return Task.FromResult(ToSessionResult(user));
        }
    }

    /// <summary>
    /// Local accounts need no email verification; report success.
    /// The UI skips the verify screen (no <c>EmailAddressId</c> is returned).
    /// </summary>
    public Task<AuthResult> PrepareEmailVerificationAsync(string emailAddressId) =>
        Task.FromResult(new AuthResult { Success = true });

    public Task<AuthResult> AttemptEmailVerificationAsync(string emailAddressId, string code) =>
        Task.FromResult(new AuthResult { Success = true });

    /// <summary>
    /// Clerk-native (FAPI) OAuth sessions can't be adopted without Clerk.
    /// Kept for the <see cref="IAuthService"/> contract; Google sign-in goes
    /// through <see cref="SignInWithGoogleAsync"/> instead.
    /// </summary>
    public Task<AuthResult> AdoptOAuthSessionAsync(string createdSessionId) =>
        Task.FromResult(new AuthResult
        {
            Success = false,
            Error = "Clerk-based sign-in is disabled. Use email/password or Google.",
        });

    public Task<AuthResult> CreateSessionAsync(string userId, string email)
    {
        lock (_lock)
        {
            var user = LoadUsers().FirstOrDefault(u => u.Id == userId);
            if (user is null)
                return Task.FromResult(new AuthResult { Success = false, Error = "Account not found" });

            return Task.FromResult(ToSessionResult(user));
        }
    }

    /// <summary>
    /// Google identity: existing email → new session; new email → a local
    /// passwordless account is created (linked to Google), then signed in.
    /// </summary>
    public Task<AuthResult> SignInWithGoogleAsync(string email, string name)
    {
        email = email.Trim();

        lock (_lock)
        {
            var users = LoadUsers();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

            if (user is null)
            {
                user = new LocalUser
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Email = email,
                    Name = name ?? string.Empty,
                    HasPassword = false,
                    CreatedAt = DateTime.UtcNow,
                };
                users.Add(user);
                SaveUsers(users);
            }

            return Task.FromResult(ToSessionResult(user));
        }
    }

    public Task SignOutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    private AuthResult ToSessionResult(LocalUser user)
    {
        var session = new UserSession
        {
            UserId = user.Id,
            Email = user.Email,
            SessionId = Guid.NewGuid().ToString("N"),
        };
        _currentUser = session;
        return new AuthResult { Success = true, User = session };
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        try
        {
            var parts = stored.Split('.');
            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static string GetUsersPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "learn-assist");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "users.json");
    }

    private static List<LocalUser> LoadUsers()
    {
        try
        {
            var path = GetUsersPath();
            if (!File.Exists(path))
                return [];
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<LocalUser>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveUsers(List<LocalUser> users)
    {
        var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetUsersPath(), json);
    }
}
