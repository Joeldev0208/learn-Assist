using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class VerifyEmailViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly string _emailAddressId;

    public VerifyEmailViewModel(IAuthService authService, string email, string emailAddressId)
    {
        _authService = authService;
        _email = email;
        _emailAddressId = emailAddressId;
        DisplayEmail = email;

        SendCodeCommand.Execute(null);
    }

    [ObservableProperty]
    public partial string DisplayEmail { get; set; }

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool CodeSent { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    private readonly string _email;

    public event Action? VerificationSucceeded;
    public event Action? BackToRegisterRequested;

    [RelayCommand]
    private async Task SendCodeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        var result = await _authService.PrepareEmailVerificationAsync(_emailAddressId);

        IsLoading = false;

        if (result.Success)
        {
            CodeSent = true;
            StatusMessage = "Verification code sent to your email";
        }
        else
        {
            ErrorMessage = result.Error ?? "Failed to send verification code";
        }
    }

    [RelayCommand]
    private async Task VerifyCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Please enter the verification code";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        var result = await _authService.AttemptEmailVerificationAsync(_emailAddressId, Code.Trim());

        IsLoading = false;

        if (result.Success)
        {
            StatusMessage = "Email verified successfully!";
            VerificationSucceeded?.Invoke();
        }
        else
        {
            ErrorMessage = result.Error ?? "Invalid verification code";
        }
    }

    [RelayCommand]
    private void BackToRegister()
    {
        BackToRegisterRequested?.Invoke();
    }
}
