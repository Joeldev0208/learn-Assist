using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using learn_Assist.Services;
using learn_Assist.ViewModels;
using learn_Assist.Views;

namespace learn_Assist;

public partial class App : Application
{
    private readonly IAuthService _authService = new ClerkAuthService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            mainWindow.Hide();

            ShowLoginWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var loginVm = new LoginViewModel(_authService);
        var loginView = new LoginView { DataContext = loginVm };

        loginVm.LoginSucceeded += () =>
        {
            loginView.Close();
            ShowMainWindow(desktop);
        };

        loginVm.GoToRegisterRequested += () =>
        {
            loginView.Close();
            ShowRegisterWindow(desktop);
        };

        loginView.Show();
    }

    private void ShowRegisterWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var registerVm = new RegisterViewModel(_authService);
        var registerView = new RegisterView { DataContext = registerVm };

        registerVm.RegisterSucceeded += (email, emailAddressId) =>
        {
            registerView.Close();
            ShowMainWindow(desktop);
        };

        registerVm.GoToLoginRequested += () =>
        {
            registerView.Close();
            ShowLoginWindow(desktop);
        };

        registerView.Show();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var email = _authService.CurrentUser?.Email ?? "user@example.com";
        var aiService = new MockAiService();
        var mainVm = new MainViewModel(aiService, email);
        if (desktop.MainWindow is MainWindow mainWindow)
            mainWindow.DataContext = mainVm;
        desktop.MainWindow?.Show();
    }

    private void ShowVerifyEmailWindow(IClassicDesktopStyleApplicationLifetime desktop, string email, string emailAddressId)
    {
        var verifyVm = new VerifyEmailViewModel(_authService, email, emailAddressId);
        var verifyView = new VerifyEmailView { DataContext = verifyVm };

        verifyVm.VerificationSucceeded += () =>
        {
            verifyView.Close();
            ShowLoginWindow(desktop);
        };

        verifyVm.BackToRegisterRequested += () =>
        {
            verifyView.Close();
            ShowRegisterWindow(desktop);
        };

        verifyView.Show();
    }
}
