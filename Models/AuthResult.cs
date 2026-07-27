namespace learn_Assist.Models;

public class AuthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public UserSession? User { get; set; }
    public string? EmailAddressId { get; set; }
    public string? VerificationId { get; set; }
}
