# learn-Assist

AI-powered desktop learning assistant built with **Avalonia 12.1.0** and **.NET 10**.

## Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10 (`net10.0`) |
| UI Toolkit | Avalonia Desktop 12.1.0 |
| Theme | FluentTheme + Inter Font |
| MVVM | CommunityToolkit.Mvvm 8.4.2 (source generators) |
| Auth | Clerk 2.0 (`Clerk.BackendAPI`) |

## Project Structure

```
├── App.axaml / App.axaml.cs    Application entry — auth flow & window wiring
├── Program.cs                  DotEnv.Load() then starts Avalonia
├── ViewLocator.cs              *ViewModel → *View via reflection
│
├── Models/                     Domain models (ChatSession, ChatMessage, AuthResult, etc.)
├── ViewModels/                 View-models inheriting ViewModelBase : ObservableObject
├── Views/                      Avalonia XAML + code-behind (LoginView, MainWindow, etc.)
├── Services/                   IAuthService, IAiService, DotEnv loader
├── Converters/                 IValueConverter (message bubble alignment/color)
├── Assets/                     Icons and screenshots
└── Aspec.mdx                   Spec document (Spanish) describing implementation phases
```

## Setup

1. Create a `.env` file in the project root:

```
CLERK_SECRET_KEY=sk_test_xxx
CLERK_PUBLISHABLE_KEY=pk_test_xxx
```

2. Run:

```sh
dotnet build
dotnet run
```

## Auth (Fase 1)

Email/password authentication via **Clerk 2.0**. Flow:

1. **LoginView** — email + password form, authenticates via Clerk API.
2. **RegisterView** — name/email/password/confirm, creates user via Clerk.
3. **MainWindow** — three-panel UI, launched after successful auth.

> Email verification (`VerifyEmailView`) is built but not yet wired — registration currently goes directly to MainWindow.

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
| Clerk auth integration | Done |
| Email verification | Built but not wired |
| OAuth buttons (Google/Apple) | UI only, no handlers |
| Chat UI (three-panel) | Done |
| AI service (MockAiService) | Done |
| Real AI integration | Not started |
| Document import | Not started |
| Session persistence | Not started |