using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public interface IAuthService
{
    Task<AuthResult> SignUpAsync(string email, string password);
    Task<AuthResult> PrepareEmailVerificationAsync(string emailAddressId);
    Task<AuthResult> AttemptEmailVerificationAsync(string emailAddressId, string code);
    Task<AuthResult> SignInAsync(string email, string password);
    Task SignOutAsync();
    bool IsAuthenticated { get; }
    UserSession? CurrentUser { get; }
}
