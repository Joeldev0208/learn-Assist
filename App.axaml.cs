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
            FireAndForget(() => CompleteInstallationAsync(desktop, installView));
        };

        installView.Show();
    }

    private async Task CompleteInstallationAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        InstallView installView)
    {
        await _authService.SignOutAsync();
        ShowLoginWindow(desktop);
        installView.Close();
    }

    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_authService.IsAuthenticated)
        {
            FireAndForget(() => ShowAuthenticatedExperienceAsync(desktop));
            return;
        }

        var loginVm = new LoginViewModel(_authService);
        var loginView = new LoginView { DataContext = loginVm };

        loginVm.LoginSucceeded += () => FireAndForget(async () =>
        {
            await ShowAuthenticatedExperienceAsync(desktop);
            loginView.Close();
        });

        loginView.Show();
    }

    private async Task ShowAuthenticatedExperienceAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var user = _authService.CurrentUser;
            var email = user?.Email ?? "user@example.com";
            var userId = user?.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                await ShowMainWindowAsync(desktop, null, null, email, null);
                return;
            }

            var profileService = new LearningProfileService();
            var profile = await profileService.LoadAsync(userId);

            if (profile is null || !profile.IsCompleted)
            {
                ShowLearningProfileWindow(desktop, profileService, userId, profile, email);
                return;
            }

            await ShowMainWindowAsync(desktop, profileService, profile, email, userId);
        }
        catch (Exception ex)
        {
            LogStartupError(ex);
            await ShowMainWindowAsync(desktop, null, null, _authService.CurrentUser?.Email ?? "user@example.com", null);
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

        profileVm.ProfileSaved += profile => FireAndForget(async () =>
        {
            if (mainVm is not null)
            {
                profileView.Close();
                mainVm.ApplyLearningProfile(profile);
                return;
            }

            await ShowMainWindowAsync(desktop, profileService, profile, email, userId);
            profileView.Close();
        });

        profileVm.Cancelled += profileView.Close;
        profileView.Show();
    }

    private async Task ShowMainWindowAsync(
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
        }
        else
        {
            aiService = new MockAiService();
        }

        if (!string.IsNullOrWhiteSpace(userId))
            persistence = SessionPersistenceService.ForUser(config?.SessionsDirectory, userId);

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
        mainVm.SignOutRequested += () => FireAndForget(() => SignOutAndShowLoginAsync(mainWindow, desktop));
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

    private static async void FireAndForget(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogStartupError(ex);
        }
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

}
