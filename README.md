# learn-Assist

AI-powered desktop learning assistant built with **Avalonia 12.1.0** and **.NET 10**.

## Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10 (`net10.0`) |
| UI Toolkit | Avalonia Desktop 12.1.0 |
| Theme | FluentTheme + Inter Font |
| MVVM | CommunityToolkit.Mvvm 8.4.2 (source generators) |
| Auth | Clerk Backend API 2.0.0 (`CLERK_SECRET_KEY` via `.env`) |
| Settings | Microsoft.Extensions.Configuration + Options (validated, pydantic-settings-like) |

## Project Structure

```
├── App.axaml / App.axaml.cs    Application entry — auth flow & window wiring
├── Program.cs                  Starts Avalonia
├── ViewLocator.cs              *ViewModel → *View via reflection
│
├── Models/                     Domain models (ChatSession, ChatMessage, AuthResult, etc.)
├── ViewModels/                 View-models inheriting ViewModelBase : ObservableObject
├── Views/                      Avalonia XAML + code-behind (LoginView, MainWindow, etc.)
├── Services/                   IAuthService (ClerkAuthService), IAiService
├── Converters/                 IValueConverter (message bubble alignment/color)
├── Assets/                     Icons and screenshots
└── Aspec.md                    Spec document (Spanish) describing implementation phases
```

## Setup

The app reads its configuration from a `.env` file next to the executable (or from real environment variables, which take precedence) using `Microsoft.Extensions.Configuration` + `Options` with DataAnnotations validation — the .NET equivalent of pydantic-settings. On startup it validates required keys and fails with a clear message (before opening any window) if `CLERK_SECRET_KEY` is missing.

Create a `.env` in the project root (gitignored):

```sh
CLERK_SECRET_KEY=sk_test_...
```

Then run:

```sh
dotnet build
dotnet run
```

## Auth

Email/password authentication via **Clerk Backend API**, driven directly from the desktop app (`ClerkAuthService` reads the key from `AppSettings`). Flow:

1. **LoginView** — email + password form, authenticates via `ClerkAuthService` → Clerk.
2. **RegisterView** — email/password/confirm, creates the user via `ClerkAuthService`.
3. **VerifyEmailView** — sends an email code and verifies the `idn_...` email-address id.
4. **MainWindow** — three-panel UI, launched after successful login.

> Dev-instance sign-up is blocked by bot protection (`captcha_missing_token`). Fix in the Clerk dashboard: **User & Authentication → Attack Protection** → turn OFF **Bot sign-up protection**, or enable **Native API** under **Native applications**.

## Chat / AI (Fase 2)

Three-panel layout: session list (left) / chat (center) / document list (right).

- `IAiService` with `AskAsync()` — currently uses `MockAiService` (hardcoded response).
- Chat bubbles with role-based alignment (user right/blue, assistant left/white).
- Auto-scroll on new messages.

## Conventions

- View-model properties use `[ObservableProperty]` (no manual `INotifyPropertyChanged`).
- Commands use `[RelayCommand]` on `async Task` methods.
- Windows are wired in `App.axaml.cs` via event callbacks (no DI container).
- View namespace `learn_Assist.Views`; ViewModel namespace `learn_Assist.ViewModels`.

## Status

| Feature | Status |
|---------|--------|
| Login / Register | Done |
| Clerk auth (Backend API + `.env` settings) | Done |
| Email verification | Done (wired) |
| OAuth (Google/Apple) via Frontend API | Done (native browser + loopback flow) |
| Chat UI (three-panel) | Done |
| AI service (MockAiService) | Done |
| Real AI integration | Done |
| Document import | Done |
| Session persistence | Done |