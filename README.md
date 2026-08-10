# learn-Assist

AI-powered desktop learning assistant built with **Avalonia 12.1.0** and **.NET 10**.

## Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10 (`net10.0`) |
| UI Toolkit | Avalonia Desktop 12.1.0 |
| Theme | FluentTheme + Inter Font |
| MVVM | CommunityToolkit.Mvvm 8.4.2 (source generators) |
| Auth | Clerk Backend API 2.0.0 (`CLERK_SECRET_KEY` via `.env`) + Frontend API (OAuth) |
| AI Providers | OpenAI SDK, Anthropic, Google Gemini, Ollama (raw `HttpClient`) |
| Settings | Microsoft.Extensions.Configuration + Options (validated, pydantic-settings-like) |

## Project Structure

```
├── Program.cs                  Avalonia bootstrap + elevated install worker (--install-elevated)
├── App.axaml / App.axaml.cs     Auth flow & window wiring (event callbacks, no DI)
├── ViewLocator.cs              *ViewModel → *View via reflection on ViewModelBase subtypes
│
├── Models/                     Domain models (AuthResult, UserSession, ChatMessage,
│                               ChatSession, UserDocument, ApiConfig, AppSettings, InstallInfo)
├── ViewModels/                 View-models inheriting ViewModelBase : ObservableObject
│                               (Main, SessionList, Chat, DocumentList, Tutorial,
│                                Login, Register, VerifyEmail, ImportDocument, ApiConfig, Install)
├── Views/                      Avalonia XAML + code-behind windows/controls
├── Services/                   IAuthService (ClerkAuthService), IAiService (Mock + 4 providers),
│                               ConfigEncryption, SessionPersistenceService, AiServiceFactory,
│                               InstallationService, OAuth: FapiOAuthClient, OAuthLoopbackListener, OAuthFlow
├── Services/Providers/         OpenAiService, AnthropicService, GeminiService, OllamaService
├── Converters/                 IValueConverter (message bubble alignment/color, content-type emoji)
├── Assets/                     Icons (avalonia-logo.ico/.png) and screenshots
├── scripts/publish.sh          Local self-contained build → dist/ (gitignored)
└── .github/workflows/release.yml  CI: linux-x64 + win-x64 single-file artifacts on v* tag
```

## Setup

The app reads its configuration from a `.env` file next to the executable (or from real environment variables, which take precedence) using `Microsoft.Extensions.Configuration` + `Options` with DataAnnotations validation — the .NET equivalent of pydantic-settings. On startup it validates required keys and fails with a clear message (before opening any window) if `CLERK_SECRET_KEY` is missing.

Create a `.env` in the project root (gitignored):

```sh
# Required — Backend API auth (email/password)
CLERK_SECRET_KEY=sk_test_...

# Optional — Native OAuth (Google/Apple). Buttons hidden if unset.
CLERK_PUBLISHABLE_KEY=pk_test_...

# Optional — OAuth loopback port (default 53174)
OAUTH_REDIRECT_PORT=53174
```

Then run:

```sh
dotnet build
dotnet run          # normal launch (install wizard appears on first run)
LEARN_ASSIST_FORCE_INSTALL=1 dotnet run   # force the install wizard in dev
```

> No tests. Release artifacts are produced by CI on `v*` tag push (linux-x64 + win-x64 self-contained single-file). Local equivalent: `scripts/publish.sh` → `dist/`.

## Startup & Installation (first-run wizard)

On startup `App.OnFrameworkInitializationCompleted` calls `InstallationService.IsInstalled()`; if not installed (or `LEARN_ASSIST_FORCE_INSTALL=1` in dev) the **InstallView** wizard runs **before** login.

- Copies the running binary (`Environment.ProcessPath`) to a user dir (`~/.local/share/learn-assist` on Linux, `%LOCALAPPDATA%\learn-assist` on Windows) or a system dir (`/opt/learn-assist` via `pkexec`, `C:\Program Files\LearnAssist` via UAC `runas` relaunch).
- Menu entry/shortcut is **always user-level** (Linux `.desktop` → `~/.local/share/applications/`; Windows `.lnk` via PowerShell + `WScript.Shell` COM to the Start Menu), even in system scope, to minimize privilege surface.
- Marker file `~/.config/learn-assist/install.json` (scope + binary path + date); `IsInstalled()` checks the marker **and** that the binary still exists — stale markers offer reinstalls.
- Elevated worker: `Program.cs` branches on `--install-elevated <source> <target>` **before** loading settings / starting Avalonia. Windows UAC relaunch uses `Verb = "runas"`. Don't reorder `Program.Main`.
- Linux `.desktop` icon is the embedded `Assets/avalonia-logo.png`, read at runtime via `AssetLoader` (`avares://...`).

## Auth

### Email / password (Backend API — direct)

`ClerkAuthService` talks straight to Clerk Backend API 2.0.0 from the desktop app; the secret comes from the validated `AppSettings`.

1. **LoginView** — email + password form, authenticates via `ClerkAuthService`.
2. **RegisterView** — email/password/confirm (8+ char password), creates the user.
3. **VerifyEmailView** — sends an email code and verifies the `idn_...` email-address id; routes back to login → main.
4. **MainWindow** — three-panel UI, launched after successful login.

> ⚠️ Dev-instance sign-up is blocked by bot protection (`captcha_missing_token`). Fix in the Clerk dashboard: **User & Authentication → Attack Protection** → turn OFF **Bot sign-up protection**, or enable **Native API** under **Native applications**.

### Native OAuth (Google/Apple — Frontend API + loopback)

- `FapiOAuthClient` talks to the Clerk **Frontend API** (publishable key → FAPI host derived by base64-decoding the key), `OAuthLoopbackListener` listens on `127.0.0.1:<port>/callback`, `OAuthFlow` orchestrates: FAPI creates sign-in → system browser opens the authorize URL → loopback captures `created_session_id` → `ClerkAuthService.AdoptOAuthSession(createdSessionId)` re-validates via Backend API and adopts the session.
- The FAPI is used **only** for the OAuth redirect; all session/user validation goes through the Backend API.
- OAuth flow resolves both sign-in and sign-up (Clerk creates the account if it doesn't exist), so the Register view reuses the same `oauth_*` strategies.
- Clerk dashboard requirements: **Native API** ON under **Native applications**, Google + Apple **connections** enabled, and `http://127.0.0.1:53174/callback` registered as a **Redirect URL**.
- OAuth buttons are hidden unless `CLERK_PUBLISHABLE_KEY` is set; email/password works without it.

> ⚠️ Security tradeoff (user decision): the `CLERK_SECRET_KEY` ships with the app's runtime (`.env` beside the binary or a real env var) — not harder to extract than before. `ConfigEncryption` is unrelated: it encrypts the AI-provider config only.

## Chat / AI

Three-panel layout: **session list (left) / chat (center) / document list (right)**.

- `IAiService` (`AskAsync(message, history)`) with `MockAiService` (hardcoded response) for testing.
- Production providers in `Services/Providers/`:
  - **OpenAiService** — uses the official `OpenAI` NuGet SDK (`ChatClient`).
  - **AnthropicService**, **GeminiService**, **OllamaService** — call native endpoints via raw `HttpClient` + `JsonDocument`. Ollama is local (`http://localhost:11434`, model `llama3.2`, API key optional).
  - Provider implementations are heterogeneous — don't assume SDK usage when editing.
- `AiServiceFactory.Create(config)` returns the correct provider based on `ApiConfig.Provider`.
- Provider selection + API key configured via `ApiConfigView` (modal dialog triggered by `MainViewModel.ConfigureAiRequested`) → encrypted via `ConfigEncryption` (AES-256 + PBKDF2-derived key) to `~/.config/learn-assist/config.enc`.
- `TutorialViewModel` shows a step-through overlay on **first login only** (static `_isFirstLogin` flag in `MainViewModel`). Flow on first login: tutorial → API config dialog → chat. On return with config: chat skips straight to active.
- `SessionPersistenceService` saves/loads `.md` conversation files in a user-chosen directory (default `~/.config/learn-assist/sessions/`); `SessionListViewModel` falls back to hardcoded sample data if empty.
- `ChatViewModel` auto-saves after each assistant response when persistence is configured.
- Chat bubbles with role-based alignment (user right/blue, assistant left/white) + auto-scroll on new messages.

## Architecture

- `MainViewModel` composes child VMs: `SessionList`, `Chat`, `DocumentList`, `Tutorial` — new panels follow this pattern.
- `MainWindow.OnDataContextChanged` subscribes to child VM events (scroll, import dialog, etc.) — **not** the constructor.
- `App.axaml.cs` manually wires windows/VMs via event callbacks — **no DI container**.
- `ImportDocumentView` is a modal dialog that returns `UserDocument?`; file-picker filter patterns live in `GetFileFilters()`.
- Sessions and documents use hardcoded sample data as fallback when no persisted data exists.

## Conventions

- View-model properties use `[ObservableProperty]` source generator (no manual `INotifyPropertyChanged`).
- Commands use `[RelayCommand]` on `async Task` methods (generates `XxxCommand` properties automatically).
- New views need both `.axaml` and `.axaml.cs`; the code-behind calls `InitializeComponent()`.
- Windows/VMs are wired in `App.axaml.cs` using event callbacks (no DI container).
- `.axaml` files use `xmlns:vm="using:learn_Assist.ViewModels"` for design-time DataContext.
- ViewModel namespace `learn_Assist.ViewModels`; View namespace `learn_Assist.Views`.
- `IAuthService` interface in `Services/`; swap implementations for testing.

## Status

| Feature | Status |
|---------|--------|
| Login / Register (email + password) | Done |
| Clerk Backend API + typed `.env` settings | Done |
| Email verification | Done (wired) |
| Native OAuth (Google/Apple) via Frontend API | Done (browser + loopback flow) |
| First-run install wizard (user + system scope) | Done |
| Chat UI (three-panel) | Done |
| AI service (MockAiService) | Done |
| Real AI integration (OpenAI, Anthropic, Gemini, Ollama) | Done |
| API config encryption (AES-256 + PBKDF2) | Done |
| Document import | Done |
| Session persistence (`.md` autosave) | Done |
| First-login tutorial overlay | Done |
