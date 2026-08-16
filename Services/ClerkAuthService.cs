using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clerk.BackendAPI;
using Clerk.BackendAPI.Models.Operations;
using learn_Assist.Models;

namespace learn_Assist.Services;

public class ClerkAuthService : IAuthService
{
    private readonly ClerkBackendApi _api;
    private UserSession? _currentUser;

    public ClerkAuthService()
    {
        var secretKey = AppSettings.Current.ClerkSecretKey
            ?? throw new InvalidOperationException("CLERK_SECRET_KEY is not configured. Add it to your .env file and restart the app.");

        _api = new ClerkBackendApi(bearerAuth: secretKey);
    }

    public bool IsAuthenticated => _currentUser is not null;

    public UserSession? CurrentUser => _currentUser;

    public async Task<AuthResult> SignUpAsync(string email, string password)
    {
        try
        {
            var listResponse = await _api.Users.ListAsync(new GetUserListRequest
            {
                EmailAddress = [email],
                Limit = 1,
            });

            if (listResponse?.UserList?.Count > 0)
                return new AuthResult { Success = false, Error = "An account with this email already exists" };

            var body = new CreateUserRequestBody
            {
                EmailAddress = new List<string> { email },
                Password = password,
                SkipPasswordRequirement = false,
                SkipPasswordChecks = false,
            };

            var response = await _api.Users.CreateAsync(body);

            if (response.User is null || string.IsNullOrEmpty(response.User.Id))
                return new AuthResult { Success = false, Error = "Failed to create user" };

            var emailAddressId = response.User.PrimaryEmailAddressId
                 ?? response.User.EmailAddresses?.FirstOrDefault()?.Id;

            var result = new AuthResult
            {
                Success = true,
                EmailAddressId = emailAddressId,
                User = new UserSession
                {
                    UserId = response.User.Id,
                    Email = email,
                },
            };

            return result;
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                message = "An account with this email already exists";
            else if (message.Contains("password", StringComparison.OrdinalIgnoreCase)
                && (message.Contains("weak", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("strength", StringComparison.OrdinalIgnoreCase)))
                message = "Password is too weak. Use at least 8 characters with uppercase, lowercase, and numbers.";

            return new AuthResult { Success = false, Error = message };
        }
    }

    public async Task<AuthResult> PrepareEmailVerificationAsync(string emailAddressId)
    {
        try
        {
            var response = await _api.EmailAddresses.PrepareVerificationAsync(emailAddressId);

            if (response?.VerificationResponse is null)
                return new AuthResult { Success = false, Error = "Failed to send verification code" };

            return new AuthResult
            {
                Success = true,
                VerificationId = response.VerificationResponse.Id,
            };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> AttemptEmailVerificationAsync(string emailAddressId, string code)
    {
        try
        {
            var body = new AttemptEmailAddressVerificationRequestBody
            {
                Code = code,
            };

            var response = await _api.EmailAddresses.AttemptVerificationAsync(emailAddressId, body);

            if (response?.VerificationResponse is null)
                return new AuthResult { Success = false, Error = "Verification failed" };

            if (response.VerificationResponse.Status != "verified"
                && response.VerificationResponse.Status != "completed")
                return new AuthResult { Success = false, Error = "Invalid verification code" };

            return new AuthResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        try
        {
            var listResponse = await _api.Users.ListAsync(new GetUserListRequest
            {
                EmailAddress = [email],
                Limit = 1,
            });

            var user = listResponse?.UserList?.FirstOrDefault();
            if (user is null || string.IsNullOrEmpty(user.Id))
                return new AuthResult { Success = false, Error = "User not found. Please register first." };

            var verifyBody = new VerifyPasswordRequestBody
            {
                Password = password,
            };

            var verifyResponse = await _api.Users.VerifyPasswordAsync(user.Id, verifyBody);

            if (verifyResponse?.Object?.Verified != true)
                return new AuthResult { Success = false, Error = "Invalid password" };

            var sessionBody = new CreateSessionRequestBody
            {
                UserId = user.Id,
            };

            var sessionResponse = await _api.Sessions.CreateAsync(sessionBody);

            var userSession = new UserSession
            {
                UserId = user.Id,
                Email = email,
                SessionId = sessionResponse?.Session?.Id ?? string.Empty,
            };

            _currentUser = userSession;

            return new AuthResult { Success = true, User = userSession };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> CreateSessionAsync(string userId, string email)
    {
        try
        {
            var sessionBody = new CreateSessionRequestBody
            {
                UserId = userId,
            };

            var sessionResponse = await _api.Sessions.CreateAsync(sessionBody);

            var userSession = new UserSession
            {
                UserId = userId,
                Email = email,
                SessionId = sessionResponse?.Session?.Id ?? string.Empty,
            };

            _currentUser = userSession;

            return new AuthResult { Success = true, User = userSession };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> AdoptOAuthSessionAsync(string createdSessionId)
    {
        try
        {
            var sessionResponse = await _api.Sessions.GetAsync(createdSessionId);
            var session = sessionResponse?.Session;

            if (session is null || string.IsNullOrEmpty(session.Id) || string.IsNullOrEmpty(session.UserId))
                return new AuthResult { Success = false, Error = "OAuth session not found" };

            var userResponse = await _api.Users.GetAsync(session.UserId);
            var user = userResponse?.User;

            var email = FindPrimaryEmail(user?.PrimaryEmailAddressId, user?.EmailAddresses) ?? string.Empty;

            var result = new AuthResult
            {
                Success = true,
                User = new UserSession
                {
                    UserId = session.UserId,
                    Email = email,
                    SessionId = session.Id,
                },
            };

            _currentUser = result.User;

            return result;
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> SignInWithGoogleAsync(string email, string name)
    {
        try
        {
            var listResponse = await _api.Users.ListAsync(new GetUserListRequest
            {
                EmailAddress = [email],
                Limit = 1,
            });

            var user = listResponse?.UserList?.FirstOrDefault();

            if (user is null || string.IsNullOrEmpty(user.Id))
            {
                // New email → create the Clerk user record (verified email, no password)
                var createBody = new CreateUserRequestBody
                {
                    EmailAddress = new List<string> { email },
                    SkipPasswordRequirement = true,
                    SkipPasswordChecks = true,
                    FirstName = name,
                };

                var created = await _api.Users.CreateAsync(createBody);
                if (created?.User is null || string.IsNullOrEmpty(created.User.Id))
                    return new AuthResult { Success = false, Error = "Failed to create Clerk user from Google account" };

                user = created.User;
            }

            return await CreateSessionAsync(user.Id, email);
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                // Race: user was created concurrently → open a session anyway
                var existing = await _api.Users.ListAsync(new GetUserListRequest
                {
                    EmailAddress = [email],
                    Limit = 1,
                });
                var existingUser = existing?.UserList?.FirstOrDefault();
                if (existingUser is null || string.IsNullOrEmpty(existingUser.Id))
                    return new AuthResult { Success = false, Error = "An account with this email already exists" };
                return await CreateSessionAsync(existingUser.Id, email);
            }

            return new AuthResult { Success = false, Error = message };
        }
    }

    private static string? FindPrimaryEmail(string? primaryId, ICollection<Clerk.BackendAPI.Models.Components.EmailAddress>? addresses)
    {
        if (addresses is null || addresses.Count == 0)
            return null;

        var primary = addresses.FirstOrDefault(a => a.Id == primaryId);
        if (primary is not null && !string.IsNullOrEmpty(primary.EmailAddressValue))
            return primary.EmailAddressValue;

        var any = addresses.FirstOrDefault(a => !string.IsNullOrEmpty(a.EmailAddressValue));
        return any?.EmailAddressValue;
    }

    public Task SignOutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }
}
