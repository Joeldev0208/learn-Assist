using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public event Action? LoginSucceeded;
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
    private void GoToRegister()
    {
        GoToRegisterRequested?.Invoke();
    }
}

