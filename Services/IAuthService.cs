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

    Task SignOutAsync();
    bool IsAuthenticated { get; }
    UserSession? CurrentUser { get; }
}
