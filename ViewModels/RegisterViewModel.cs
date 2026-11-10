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
    private readonly GoogleOAuthService _googleOAuth;

    public bool IsOAuthConfigured => _oauth.IsConfigured;

    public bool IsGoogleOAuthConfigured => _googleOAuth.IsConfigured;

    private bool IsEnglish => LanguageService.IsEnglish;

    public string WindowTitle => IsEnglish ? "learn_Assist - Register" : "learn_Assist - Registro";
    public string TabLogin => IsEnglish ? "Sign in" : "Iniciar sesión";
    public string TabRegister => IsEnglish ? "Create account" : "Crear cuenta";
    public string CreateAccount => IsEnglish ? "Create your account!" : "¡Crea tu cuenta!";
    public string CreateAccountSubtitle => IsEnglish ? "All you need is an email address and a password." : "Solo necesitas un correo electrónico y una contraseña.";
    public string SignUpWithGoogle => IsEnglish ? "Sign up with Google" : "Registrarse con Google";
    public string OrSeparator => IsEnglish ? "Or" : "O";
    public string LocalAccountNote => IsEnglish ? "Local account: no email verification required" : "Cuenta local: no se requiere verificación por correo";
    public string EmailLabel => IsEnglish ? "Email address" : "Correo electrónico";
    public string EmailPlaceholder => IsEnglish ? "Enter your email address" : "Introduce tu correo electrónico";
    public string PasswordLabel => IsEnglish ? "Password" : "Contraseña";
    public string PasswordPlaceholder => IsEnglish ? "Create a password" : "Crea una contraseña";
    public string ConfirmPasswordLabel => IsEnglish ? "Confirm password" : "Confirmar contraseña";
    public string ConfirmPasswordPlaceholder => IsEnglish ? "Confirm your password" : "Confirma tu contraseña";
    public string RegisterButton => IsEnglish ? "Create account" : "Crear cuenta";
    public string RegisterButtonLoading => IsEnglish ? "Creating account..." : "Creando cuenta...";
    public string AlreadyHaveAccount => IsEnglish ? "Already have an account? Sign in" : "¿Ya tienes una cuenta? Inicia sesión";
    public string MissingEmail => IsEnglish ? "Enter your email address" : "Introduce tu correo electrónico";
    public string InvalidEmail => IsEnglish ? "Enter a valid email address" : "Introduce un correo electrónico válido";
    public string MissingPassword => IsEnglish ? "Enter a password" : "Introduce una contraseña";
    public string PasswordTooShort => IsEnglish ? "Password must be at least 8 characters" : "La contraseña debe tener al menos 8 caracteres";
    public string PasswordsMismatch => IsEnglish ? "Passwords do not match" : "Las contraseñas no coinciden";
    public string RegisterFailed => IsEnglish ? "Registration failed" : "El registro falló";
    public string RegisterWithGoogleFailed => IsEnglish ? "Registration with Google failed" : "El registro con Google falló";
    public string ThemeTip => IsEnglish ? "Switch light/dark theme" : "Cambiar tema claro/oscuro";

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
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsPasswordVisible { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmPasswordVisible { get; set; }

    public event Action<string, string, string>? RegisterSucceeded;

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

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        _oauth = new OAuthFlow(authService);
        _googleOAuth = new GoogleOAuthService(AppSettings.Current);
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(TabLogin));
        OnPropertyChanged(nameof(TabRegister));
        OnPropertyChanged(nameof(CreateAccount));
        OnPropertyChanged(nameof(CreateAccountSubtitle));
        OnPropertyChanged(nameof(SignUpWithGoogle));
        OnPropertyChanged(nameof(OrSeparator));
        OnPropertyChanged(nameof(LocalAccountNote));
        OnPropertyChanged(nameof(EmailLabel));
        OnPropertyChanged(nameof(EmailPlaceholder));
        OnPropertyChanged(nameof(PasswordLabel));
        OnPropertyChanged(nameof(PasswordPlaceholder));
        OnPropertyChanged(nameof(ConfirmPasswordLabel));
        OnPropertyChanged(nameof(ConfirmPasswordPlaceholder));
        OnPropertyChanged(nameof(RegisterButton));
        OnPropertyChanged(nameof(RegisterButtonLoading));
        OnPropertyChanged(nameof(AlreadyHaveAccount));
        OnPropertyChanged(nameof(MissingEmail));
        OnPropertyChanged(nameof(InvalidEmail));
        OnPropertyChanged(nameof(MissingPassword));
        OnPropertyChanged(nameof(PasswordTooShort));
        OnPropertyChanged(nameof(PasswordsMismatch));
        OnPropertyChanged(nameof(RegisterFailed));
        OnPropertyChanged(nameof(RegisterWithGoogleFailed));
        OnPropertyChanged(nameof(ThemeTip));
    }

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = MissingEmail;
            return;
        }

        if (!EmailRegex.IsMatch(Email.Trim()))
        {
            ErrorMessage = InvalidEmail;
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = MissingPassword;
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = PasswordTooShort;
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = PasswordsMismatch;
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        var result = await _authService.SignUpAsync(Email.Trim(), Password);

        IsLoading = false;

        if (result.Success)
        {
            RegisterSucceeded?.Invoke(Email.Trim(), result.EmailAddressId ?? string.Empty, result.User?.UserId ?? string.Empty);
            }
        else
        {
            ErrorMessage = result.Error ?? RegisterFailed;
        }
    }

    [RelayCommand]
    private void GoToLogin()
    {
        GoToLoginRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SignUpWithGoogleAsync()
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
            var email = result.User?.Email ?? string.Empty;
            RegisterSucceeded?.Invoke(email, string.Empty, result.User?.UserId ?? string.Empty);
        }
        else
        {
            ErrorMessage = result.Error ?? RegisterWithGoogleFailed;
        }
    }

    [RelayCommand]
    private async Task SignUpWithAppleAsync() => await RunOAuthAsync("oauth_apple");

    private async Task<AuthResult> RunOAuthAsync(string strategy)
    {
        IsLoading = true;
        ErrorMessage = null;

        var result = await _oauth.SignInAsync(strategy, AppSettings.Current.OAuthRedirectPort);

        IsLoading = false;

        if (result.Success)
        {
            var email = result.User?.Email ?? string.Empty;
            RegisterSucceeded?.Invoke(email, result.EmailAddressId ?? string.Empty, result.User?.UserId ?? string.Empty);
        }
        else
        {
            ErrorMessage = result.Error ?? RegisterFailed;
        }

        return result;
    }
}
