using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public interface IAuthService
{
    Task<AuthResult> SignUpAsync(string email, string password);
    Task<AuthResult> PrepareEmailVerificationAsync(string emailAddressId);
    Task<AuthResult> AttemptEmailVerificationAsync(string emailAddressId, string code);
    Task<AuthResult> SignInAsync(string email, string password);

    /// <summary>
    /// Turns a session id created by the native OAuth (FAPI) flow into an
    /// authenticated <see cref="UserSession"/> by re-validating it through
    /// the Clerk Backend API.
    /// </summary>
    Task<AuthResult> AdoptOAuthSessionAsync(string createdSessionId);

    /// <summary>
    /// Creates a Clerk session for the given user (e.g., after email
    /// verification) and sets it as the current user.
    /// </summary>
    Task<AuthResult> CreateSessionAsync(string userId, string email);

    Task SignOutAsync();
    bool IsAuthenticated { get; }
    UserSession? CurrentUser { get; }
}
