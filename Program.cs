using System;
using System.IO;
using Avalonia;
using DotNetEnv;
using DotNetEnv.Configuration;
using learn_Assist.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace learn_Assist;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--install-elevated")
        {
            RunElevatedInstall(args[1], args[2]);
            return;
        }

        AppSettings.Current = LoadSettings();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Elevated worker mode (invoked on Windows by <c>InstallationService</c>
    /// via a UAC "runas" relaunch, or re-used on Linux): copies the binary to
    /// a system location without opening any window, then exits.
    /// </summary>
    private static void RunElevatedInstall(string source, string target)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);

            var temp = target + ".tmp";
            System.IO.File.Copy(source, temp, overwrite: true);
            System.IO.File.Move(temp, target, overwrite: true);
            System.IO.File.WriteAllText(target + ".installed", "ok");
            Environment.Exit(0);
        }
        catch
        {
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Loads settings from <c>.env</c> (via DotNetEnv) and real environment
    /// variables (which take precedence), binds them into the validated
    /// <see cref="AppSettings"/> POCO, and fails with a clear message if a
    /// required key is missing — all before the UI is created.
    /// </summary>
    private static AppSettings LoadSettings()
    {
        var builder = new ConfigurationBuilder();
        if (File.Exists(".env"))
            builder.AddDotNetEnv(".env", LoadOptions.DEFAULT);
        builder.AddEnvironmentVariables();

        var config = builder.Build();

        var services = new ServiceCollection();
        services.AddOptions<AppSettings>()
            .Bind(config)
            .ValidateDataAnnotations();

        using var provider = services.BuildServiceProvider();
        try
        {
            return provider.GetRequiredService<IOptions<AppSettings>>().Value;
        }
        catch (OptionsValidationException)
        {
            throw new InvalidOperationException(
                "Missing required configuration. Add CLERK_SECRET_KEY to your .env file and restart the app.");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}