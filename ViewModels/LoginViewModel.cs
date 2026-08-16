using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly OAuthFlow _oauth;
    private readonly GoogleOAuthService _googleOAuth;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        _oauth = new OAuthFlow(authService);
        _googleOAuth = new GoogleOAuthService(AppSettings.Current);
    }

    public bool IsOAuthConfigured => _oauth.IsConfigured;

    public bool IsGoogleOAuthConfigured => _googleOAuth.IsConfigured;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsPasswordVisible { get; set; }

    public event Action? LoginSucceeded;

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }
    public event Action? GoToRegisterRequested;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter email and password";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        var result = await _authService.SignInAsync(Email.Trim(), Password);

        IsLoading = false;

        if (result.Success)
        {
            LoginSucceeded?.Invoke();
        }
        else
        {
            ErrorMessage = result.Error ?? "Login failed";
        }
    }

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        if (!_googleOAuth.IsConfigured)
            return;

        IsLoading = true;
        ErrorMessage = null;

        AuthResult result;
        try
        {
            var profile = await _googleOAuth.SignInAsync(AppSettings.Current.OAuthRedirectPort);
            result = await _authService.SignInWithGoogleAsync(profile.Email, profile.Name);
        }
        catch (Exception ex)
        {
            result = new AuthResult { Success = false, Error = ex.Message };
        }

        IsLoading = false;

        if (result.Success)
        {
            LoginSucceeded?.Invoke();
        }
        else
        {
            ErrorMessage = result.Error ?? "Google sign-in failed";
        }
    }

    [RelayCommand]
    private async Task SignInWithAppleAsync() => await RunOAuthAsync("oauth_apple");

    private async Task RunOAuthAsync(string strategy)
    {
        IsLoading = true;
        ErrorMessage = null;

        var result = await _oauth.SignInAsync(strategy, AppSettings.Current.OAuthRedirectPort);

        IsLoading = false;

        if (result.Success)
        {
            LoginSucceeded?.Invoke();
        }
        else
        {
            ErrorMessage = result.Error ?? "Sign-in failed";
        }
    }

    [RelayCommand]
    private void GoToRegister()
    {
        GoToRegisterRequested?.Invoke();
    }
}

