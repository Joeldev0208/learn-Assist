namespace learn_Assist.Models;

public class AuthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public UserSession? User { get; set; }

    /// <summary>
    /// The Clerk email-address id (idn_...) used to drive email verification
    /// after sign-up via the Clerk Backend API.
    /// </summary>
    public string? EmailAddressId { get; set; }

    public string? VerificationId { get; set; }
}
