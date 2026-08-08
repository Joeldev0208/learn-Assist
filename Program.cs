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
        AppSettings.Current = LoadSettings();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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