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
        Register = new RegisterViewModel(authService);
        Register.RegisterSucceeded += (_, _, _) => LoginSucceeded?.Invoke();
        Register.GoToLoginRequested += () => SelectedTabIndex = 0;
    }

    public RegisterViewModel Register { get; }

    public bool IsOAuthConfigured => _oauth.IsConfigured;

    public bool IsGoogleOAuthConfigured => _googleOAuth.IsConfigured;

    public string ThemeGlyph => ThemeService.IsDark ? "☀️" : "🌙";

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.ToggleAndRestart();
    }

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
    [ObservableProperty]
    public partial bool IsRegisterMode { get; set; }

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    partial void OnSelectedTabIndexChanged(int value)
    {
        IsRegisterMode = value == 1;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Introduce tu correo electrónico y contraseña";
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
            ErrorMessage = result.Error ?? "El inicio de sesión falló";
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
            ErrorMessage = result.Error ?? "El inicio de sesión con Google falló";
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
            ErrorMessage = result.Error ?? "El inicio de sesión falló";
        }
    }

    [RelayCommand]
    private void ShowRegister()
    {
        SelectedTabIndex = 1;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ShowLogin()
    {
        SelectedTabIndex = 0;
        ErrorMessage = null;
    }
}
