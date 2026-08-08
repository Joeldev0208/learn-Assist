using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly OAuthFlow _oauth;

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        _oauth = new OAuthFlow(authService);
    }

    public bool IsOAuthConfigured => _oauth.IsConfigured;

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsPasswordVisible { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmPasswordVisible { get; set; }

    public event Action<string, string>? RegisterSucceeded;

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
    }
    public event Action? GoToLoginRequested;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Please enter your email";
            return;
        }

        if (!EmailRegex.IsMatch(Email.Trim()))
        {
            ErrorMessage = "Please enter a valid email address";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter a password";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        var result = await _authService.SignUpAsync(Email.Trim(), Password);

        IsLoading = false;

        if (result.Success)
        {
            RegisterSucceeded?.Invoke(Email.Trim(), result.EmailAddressId ?? string.Empty);
        }
        else
        {
            ErrorMessage = result.Error ?? "Registration failed";
        }
    }

    [RelayCommand]
    private void GoToLogin()
    {
        GoToLoginRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SignUpWithGoogleAsync() => await RunOAuthAsync("oauth_google");

    [RelayCommand]
    private async Task SignUpWithAppleAsync() => await RunOAuthAsync("oauth_apple");

    private async Task RunOAuthAsync(string strategy)
    {
        IsLoading = true;
        ErrorMessage = null;

        var result = await _oauth.SignInAsync(strategy, AppSettings.Current.OAuthRedirectPort);

        IsLoading = false;

        if (result.Success)
        {
            var email = result.User?.Email ?? string.Empty;
            RegisterSucceeded?.Invoke(email, string.Empty);
        }
        else
        {
            ErrorMessage = result.Error ?? "Sign-up failed";
        }
    }
}
