using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clerk.BackendAPI;
using Clerk.BackendAPI.Models.Components;
using Clerk.BackendAPI.Models.Operations;
using learn_Assist.Models;

namespace learn_Assist.Services;

public class ClerkAuthService : IAuthService
{
    private readonly ClerkBackendApi _api;
    private UserSession? _currentUser;

    public ClerkAuthService()
    {
        var secretKey = Environment.GetEnvironmentVariable("CLERK_SECRET_KEY")
            ?? throw new InvalidOperationException("CLERK_SECRET_KEY environment variable is not set");

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
                User = new UserSession
                {
                    UserId = response.User.Id,
                    Email = email,
                },
                EmailAddressId = emailAddressId,
            };

            _currentUser = result.User;

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

    public Task SignOutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }
}
