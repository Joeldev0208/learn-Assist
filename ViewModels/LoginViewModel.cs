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
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(TabLogin));
        OnPropertyChanged(nameof(TabRegister));
        OnPropertyChanged(nameof(WelcomeBack));
        OnPropertyChanged(nameof(WelcomeSubtitle));
        OnPropertyChanged(nameof(SignInWithGoogle));
        OnPropertyChanged(nameof(OrSeparator));
        OnPropertyChanged(nameof(LocalAccountNote));
        OnPropertyChanged(nameof(EmailLabel));
        OnPropertyChanged(nameof(EmailPlaceholder));
        OnPropertyChanged(nameof(PasswordLabel));
        OnPropertyChanged(nameof(PasswordPlaceholder));
        OnPropertyChanged(nameof(CreateAccountLink));
        OnPropertyChanged(nameof(SignInButton));
        OnPropertyChanged(nameof(SignInButtonLoading));
        OnPropertyChanged(nameof(SignInWithGoogleFailed));
        OnPropertyChanged(nameof(SignInFailed));
        OnPropertyChanged(nameof(MissingCredentials));
        OnPropertyChanged(nameof(ThemeTip));
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

    private bool IsEnglish => LanguageService.IsEnglish;

    public string WindowTitle => IsEnglish ? "learn_Assist - Sign in" : "learn_Assist - Iniciar sesión";
    public string TabLogin => IsEnglish ? "Sign in" : "Iniciar sesión";
    public string TabRegister => IsEnglish ? "Create account" : "Crear cuenta";
    public string WelcomeBack => IsEnglish ? "Welcome back!" : "¡Bienvenido de nuevo!";
    public string WelcomeSubtitle => IsEnglish ? "Everything you need to learn, in one place" : "todo lo necesario para aprender, en un solo lugar";
    public string SignInWithGoogle => IsEnglish ? "Sign in with Google" : "Iniciar sesión con Google";
    public string OrSeparator => IsEnglish ? "Or" : "O";
    public string LocalAccountNote => IsEnglish ? "Local account: no email verification required" : "Cuenta local: no se requiere verificación por correo";
    public string EmailLabel => IsEnglish ? "Email address" : "Correo electrónico";
    public string EmailPlaceholder => IsEnglish ? "Enter your email address" : "Introduce tu correo electrónico";
    public string PasswordLabel => IsEnglish ? "Password" : "Contraseña";
    public string PasswordPlaceholder => IsEnglish ? "Enter your password" : "Introduce tu contraseña";
    public string CreateAccountLink => IsEnglish ? "Create a local account" : "Crear una cuenta local";
    public string SignInButton => IsEnglish ? "Sign in" : "Iniciar sesión";
    public string SignInButtonLoading => IsEnglish ? "Signing in..." : "Iniciando sesión...";
    public string SignInWithGoogleFailed => IsEnglish ? "Sign in with Google failed" : "El inicio de sesión con Google falló";
    public string SignInFailed => IsEnglish ? "Sign in failed" : "El inicio de sesión falló";
    public string MissingCredentials => IsEnglish ? "Enter your email address and password" : "Introduce tu correo electrónico y contraseña";
    public string ThemeTip => IsEnglish ? "Switch light/dark theme" : "Cambiar tema claro/oscuro";

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
            ErrorMessage = MissingCredentials;
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
            ErrorMessage = result.Error ?? SignInFailed;
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
            ErrorMessage = result.Error ?? SignInWithGoogleFailed;
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
            ErrorMessage = result.Error ?? SignInFailed;
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
