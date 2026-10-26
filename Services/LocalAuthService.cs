using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Local-accounts auth backed by SQLite on this PC. Passwords are stored as
/// PBKDF2-SHA256 hashes (never plaintext). No cloud, no email verification.
/// <para/>
/// <c>ClerkAuthService</c> is kept in the repo unused so Clerk can be
/// re-enabled later for a cloud feature.
/// </summary>
public class LocalAuthService : IAuthService
{
    private const int Iterations = 210_000;
    private readonly object _lock = new();
    private readonly UserDatabase _database = UserDatabase.Current;
    private UserSession? _currentUser;

    public LocalAuthService()
    {
        RestoreRememberedSession();
    }

    public bool IsAuthenticated => _currentUser is not null;

    public UserSession? CurrentUser => _currentUser;

    public Task<AuthResult> SignUpAsync(string email, string password)
    {
        email = email.Trim();

        lock (_lock)
        {
            if (_database.FindUserByEmail(email) is not null)
                return Task.FromResult(new AuthResult { Success = false, Error = "An account with this email already exists" });

            var user = new UserDatabase.UserRecord(
                Guid.NewGuid().ToString("N"),
                email,
                string.Empty,
                HashPassword(password),
                true,
                DateTime.UtcNow);
            _database.AddUser(user);

            return Task.FromResult(ToSessionResult(user));
        }
    }

    public Task<AuthResult> SignInAsync(string email, string password)
    {
        email = email.Trim();

        lock (_lock)
        {
            var user = _database.FindUserByEmail(email);

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
            var user = _database.FindUserById(userId);
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
            var user = _database.FindUserByEmail(email);

            if (user is null)
            {
                user = new UserDatabase.UserRecord(
                    Guid.NewGuid().ToString("N"),
                    email,
                    name ?? string.Empty,
                    string.Empty,
                    false,
                    DateTime.UtcNow);
                _database.AddUser(user);
            }

            return Task.FromResult(ToSessionResult(user));
        }
    }

    public Task SignOutAsync()
    {
        lock (_lock)
        {
            _currentUser = null;
            DeleteRememberedSession();
        }
        return Task.CompletedTask;
    }

    private AuthResult ToSessionResult(UserDatabase.UserRecord user)
    {
        var session = new UserSession
        {
            UserId = user.Id,
            Email = user.Email,
            SessionId = Guid.NewGuid().ToString("N"),
        };
        _currentUser = session;
        SaveRememberedSession(session);
        return new AuthResult { Success = true, User = session };
    }

    private void RestoreRememberedSession()
    {
        lock (_lock)
        {
            try
            {
                var remembered = _database.LoadRememberedSession();
                if (remembered is null
                    || string.IsNullOrWhiteSpace(remembered.UserId)
                    || string.IsNullOrWhiteSpace(remembered.SessionId))
                {
                    _database.DeleteRememberedSession();
                    return;
                }

                var user = _database.FindUserById(remembered.UserId);
                if (user is null
                    || !string.Equals(user.Email, remembered.Email, StringComparison.OrdinalIgnoreCase))
                {
                    _database.DeleteRememberedSession();
                    return;
                }

                _currentUser = new UserSession
                {
                    UserId = user.Id,
                    Email = user.Email,
                    SessionId = remembered.SessionId,
                };
            }
            catch
            {
                // A temporarily unavailable database must not block startup.
            }
        }
    }

    private void SaveRememberedSession(UserSession session)
    {
        try
        {
            _database.SaveRememberedSession(new UserDatabase.RememberedSession(
                session.UserId,
                session.Email,
                session.SessionId));
        }
        catch
        {
            // The authenticated session remains usable for this process.
        }
    }

    private void DeleteRememberedSession()
    {
        try
        {
            _database.DeleteRememberedSession();
        }
        catch
        {
            // The next successful sign-in can overwrite the database row.
        }
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

}
