using System;
using System.IO;
using System.Threading.Tasks;
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
    // Local accounts on this PC. ClerkAuthService is kept in the repo
    // (unused) so Clerk can be re-enabled later for a cloud feature.
    private readonly IAuthService _authService = new LocalAuthService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeService.Apply();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (ShouldShowInstaller())
                ShowInstallWindow(desktop);
            else
                ShowLoginWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool ShouldShowInstaller()
    {
        if (Environment.GetEnvironmentVariable("LEARN_ASSIST_FORCE_INSTALL") == "1")
            return true;
        return !InstallationService.IsInstalled();
    }

    private void ShowInstallWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var installVm = new InstallViewModel();
        var installView = new InstallView { DataContext = installVm };

        installVm.InstallationFinished += () =>
        {
            _ = ShowLoginAfterInstallAsync(desktop);
            installView.Close();
        };

        installVm.InstallationSkipped += () =>
        {
            _ = ShowLoginAfterInstallAsync(desktop);
            installView.Close();
        };

        installView.Show();
    }

    private async Task ShowLoginAfterInstallAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        await _authService.SignOutAsync();
        ShowLoginWindow(desktop);
    }

    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_authService.IsAuthenticated)
        {
            ShowAuthenticatedExperience(desktop);
            return;
        }

        var loginVm = new LoginViewModel(_authService);
        var loginView = new LoginView { DataContext = loginVm };

        loginVm.LoginSucceeded += () =>
        {
            ShowAuthenticatedExperience(desktop);
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

        registerVm.RegisterSucceeded += (email, emailAddressId, userId) =>
        {
            if (!string.IsNullOrEmpty(emailAddressId))
                ShowVerifyEmailWindow(desktop, email, emailAddressId, userId);
            else
                ShowAuthenticatedExperience(desktop);

            registerView.Close();
        };

        registerVm.GoToLoginRequested += () =>
        {
            ShowLoginWindow(desktop);
            registerView.Close();
        };

        registerView.Show();
    }

    private async void ShowAuthenticatedExperience(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var user = _authService.CurrentUser;
            var email = user?.Email ?? "user@example.com";
            var userId = user?.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                ShowMainWindow(desktop, null, null, email, null);
                return;
            }

            var profileService = new LearningProfileService();
            var profile = await profileService.LoadAsync(userId);

            if (profile is null || !profile.IsCompleted)
            {
                ShowLearningProfileWindow(desktop, profileService, userId, profile, email);
                return;
            }

            ShowMainWindow(desktop, profileService, profile, email, userId);
        }
        catch (Exception ex)
        {
            LogStartupError(ex);
            ShowMainWindow(desktop, null, null, _authService.CurrentUser?.Email ?? "user@example.com", null);
        }
    }

    private void ShowLearningProfileWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        LearningProfileService profileService,
        string userId,
        LearningProfile? existingProfile,
        string email,
        MainWindow? mainWindow = null,
        MainViewModel? mainVm = null)
    {
        var profileVm = new LearningProfileViewModel(
            profileService,
            userId,
            existingProfile,
            isRequired: mainWindow is null);
        var profileView = new LearningProfileView { DataContext = profileVm };

        profileVm.ProfileSaved += profile =>
        {
            profileView.Close();
            if (mainVm is not null)
            {
                mainVm.ApplyLearningProfile(profile);
                return;
            }

            ShowMainWindow(desktop, profileService, profile, email, userId);
        };

        profileVm.Cancelled += profileView.Close;
        profileView.Show();
    }

    private async void ShowMainWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        LearningProfileService? profileService,
        LearningProfile? profile,
        string email,
        string? userId)
    {

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

        var mainVm = new MainViewModel(
            aiService,
            email,
            userId,
            config,
            persistence,
            new UpdateService(),
            profileService,
            profile);
        await mainVm.InitializeAsync();

        var mainWindow = new MainWindow
        {
            DataContext = mainVm,
        };
        mainVm.SignOutRequested += () => _ = SignOutAndShowLoginAsync(mainWindow, desktop);
        mainVm.EditLearningProfileRequested += () =>
            ShowLearningProfileWindow(
                desktop,
                profileService ?? new LearningProfileService(),
                mainVm.UserId,
                mainVm.LearningProfile,
                email,
                mainWindow,
                mainVm);
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
    }

    private async Task SignOutAndShowLoginAsync(MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        await _authService.SignOutAsync();
        ShowLoginWindow(desktop);
        mainWindow.Close();
    }

    private static void LogStartupError(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "learn-assist");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "startup-error.log"),
                $"{DateTime.UtcNow:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must not prevent the fallback window from opening.
        }
    }

    private void ShowVerifyEmailWindow(IClassicDesktopStyleApplicationLifetime desktop, string email, string emailAddressId, string userId)
    {
        var verifyVm = new VerifyEmailViewModel(_authService, email, emailAddressId, userId);
        var verifyView = new VerifyEmailView { DataContext = verifyVm };

        verifyVm.VerificationSucceeded += () =>
        {
            ShowAuthenticatedExperience(desktop);
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
