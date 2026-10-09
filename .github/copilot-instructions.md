# Copilot instructions for learn-Assist

## Project profile

This repository is a single-project Avalonia desktop application targeting .NET 10. The project folder is `learn-Assist`, while the C# root namespace is `learn_Assist`. There is no solution file, DI container, test project, or dedicated lint configuration.

## Build, run, and validation

Run commands from the repository root:

- `dotnet build` — primary validation command.
- `dotnet run` — launch the application.
- `LEARN_ASSIST_FORCE_INSTALL=1 dotnet run` — force the first-run installer during development.
- `scripts/publish.sh` — local self-contained publish to the ignored `dist/` directory.

There is no single-test command because the repository currently has no test project. Release CI runs on `v*` tags and publishes self-contained `linux-x64` and `win-x64` artifacts.

## Architecture

- `Program.cs` bootstraps Avalonia and handles `--install-elevated <src> <target>` before normal settings or UI initialization. Do not move that branch.
- `App.axaml.cs` manually constructs views/view-models and wires authentication and navigation callbacks; do not introduce a DI container for new flows.
- `MainViewModel` composes the session list, chat, document list, and tutorial child view-models.
- `MainWindow.OnDataContextChanged` subscribes to child view-model events such as scrolling, import dialogs, and AI configuration. Keep those subscriptions out of constructors.
- `ViewLocator.cs` maps `*ViewModel` types to matching `*View` types by reflection over `ViewModelBase`.
- `Services/` contains authentication, installation/update, persistence, encryption, OAuth, and AI-provider boundaries. `AiServiceFactory` selects the provider from `ApiConfig`.
- AI providers are intentionally heterogeneous: `OpenAiService` uses the OpenAI SDK, while Anthropic, Gemini, Ollama, NVIDIA, and OpenCode use raw `HttpClient`/JSON integrations.
- Local authentication is the active desktop path through `LocalAuthService`; Clerk-related services remain for deferred integration. Google sign-in uses a loopback OAuth flow when configured.
- Session conversations persist as Markdown files, and AI configuration is encrypted in the user configuration directory. The UI falls back to `MockAiService` when no real AI configuration is available.

## Repository-specific conventions

- Use `[ObservableProperty]` and `[RelayCommand]` source generators from CommunityToolkit.Mvvm. Do not manually implement `INotifyPropertyChanged`.
- Async commands are declared as `[RelayCommand]` methods returning `Task`; generated command properties follow the `XxxCommand` naming convention.
- New views require both `.axaml` and `.axaml.cs`; code-behind calls `InitializeComponent()`.
- Use `xmlns:vm="using:learn_Assist.ViewModels"` in XAML design-time data contexts. View models live in `learn_Assist.ViewModels`; views live in `learn_Assist.Views`.
- Add environment-backed settings to `Models/AppSettings.cs` with `[ConfigurationKeyName("UPPERCASE_KEY")]` and the existing DataAnnotations validation pattern.
- Real environment variables take precedence over the optional, gitignored `.env`.
- Add new window flows through `App.axaml.cs`, and follow the existing event-callback pattern.
- Preserve the install marker, user/system install behavior, and self-update assumptions in `InstallationService` and `UpdateService`.

## MCP configuration

The repository includes `.vscode/mcp.json` with three servers:

- `github` — the hosted GitHub MCP server using the host's OAuth flow. Do not commit PATs or other credentials.
- `filesystem` — the official filesystem server scoped to `${workspaceFolder}`. Keep its allowed root limited to this repository.
- `uiinspect` — `UIInspect.MCP.Server`, a Windows-only .NET 10 MCP server for semantic UI Automation of Avalonia and other native applications. It requires an interactive Windows desktop and `dnx` from the .NET 10 SDK.

For Copilot CLI, the built-in GitHub MCP server is already available. Verify it with `/mcp show github-mcp-server`; enable extra toolsets only when needed. The repository `.vscode/mcp.json` is intended for MCP hosts that load workspace configuration; Copilot CLI users should configure filesystem and UIInspect in their user MCP configuration if the CLI does not load workspace files.

UIInspect must run in the same Windows logon session and at a sufficient integrity level for the app. It uses explicit local consent and semantic UI Automation references rather than blind coordinates. A safe verification flow is: start the app, discover the window, request consent, attach to the exact process, inspect the bounded UI tree, perform the minimum action, re-inspect after each action, and close the session. Do not use UI automation to handle secrets or type credentials.

## Important files for common changes

- Startup/navigation: `Program.cs`, `App.axaml.cs`
- Main-screen composition and event wiring: `ViewModels/MainViewModel.cs`, `Views/MainWindow.axaml.cs`
- Settings/environment variables: `Models/AppSettings.cs`
- AI provider selection/integrations: `Services/AiServiceFactory.cs`, `Services/Providers/`
- Authentication: `Services/LocalAuthService.cs`, `Services/ClerkAuthService.cs`, `Services/GoogleOAuthService.cs`
- Installation/update behavior: `Services/InstallationService.cs`, `Services/UpdateService.cs`
