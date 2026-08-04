using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using learn_Assist.Models;
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
            ShowMainWindow(desktop);
            loginView.Close();
        };

        loginVm.GoToRegisterRequested += () =>
        {
            ShowRegisterWindow(desktop);
            loginView.Close();
        };

        loginView.Show();
    }

    private void ShowRegisterWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var registerVm = new RegisterViewModel(_authService);
        var registerView = new RegisterView { DataContext = registerVm };

        registerVm.RegisterSucceeded += (email, emailAddressId) =>
        {
            if (!string.IsNullOrEmpty(emailAddressId))
                ShowVerifyEmailWindow(desktop, email, emailAddressId);
            else
                ShowMainWindow(desktop);

            registerView.Close();
        };

        registerVm.GoToLoginRequested += () =>
        {
            ShowLoginWindow(desktop);
            registerView.Close();
        };

        registerView.Show();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var email = _authService.CurrentUser?.Email ?? "user@example.com";

        var config = ConfigEncryption.LoadConfig();
        SessionPersistenceService? persistence = null;

        IAiService aiService;
        if (config is not null)
        {
            aiService = AiServiceFactory.Create(config);
            if (!string.IsNullOrEmpty(config.SessionsDirectory))
                persistence = new SessionPersistenceService(config.SessionsDirectory);
        }
        else
        {
            aiService = new MockAiService();
        }

        var mainVm = new MainViewModel(aiService, email, config, persistence);
        var mainWindow = new MainWindow
        {
            DataContext = mainVm,
        };
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void ShowVerifyEmailWindow(IClassicDesktopStyleApplicationLifetime desktop, string email, string emailAddressId)
    {
        var verifyVm = new VerifyEmailViewModel(_authService, email, emailAddressId);
        var verifyView = new VerifyEmailView { DataContext = verifyVm };

        verifyVm.VerificationSucceeded += () =>
        {
            ShowLoginWindow(desktop);
            verifyView.Close();
        };

        verifyVm.BackToRegisterRequested += () =>
        {
            ShowRegisterWindow(desktop);
            verifyView.Close();
        };

        verifyView.Show();
    }
}
