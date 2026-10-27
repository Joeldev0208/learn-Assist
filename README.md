# learn-Assist

AI-powered desktop learning assistant built with **Avalonia 12.1.0** and **.NET 10**.

## Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10 (`net10.0`) |
| UI Toolkit | Avalonia Desktop 12.1.0 |
| Theme | FluentTheme + Inter Font |
| MVVM | CommunityToolkit.Mvvm 8.4.2 (source generators) |
| Auth | Local PC accounts (`LocalAuthService`: PBKDF2-SHA256 hashes in SQLite, no cloud, no email verification) + optional Google OAuth (Google Cloud) |
| AI Providers | OpenAI SDK, Anthropic, Google Gemini, Ollama, NVIDIA, OpenCode, MockAiService |
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
│                                Login, Register, ImportDocument, ApiConfig, Install,
│                                Uninstall)
├── Views/                      Avalonia XAML + code-behind windows/controls
├── Services/                   LocalAuthService (PBKDF2 auth in SQLite), IAiService (Mock + 6 providers),
│                               ConfigEncryption, SessionPersistenceService, AiServiceFactory,
│                               InstallationService, UninstallService (User/System scope, Windows/Linux),
│                               TeacherProfileService, ProgramContextBuilder, FileContentReader,
│                               OAuth: GoogleOAuthService, OAuthFlow, OAuthLoopbackListener
├── Services/Providers/         OpenAiService, AnthropicService, GeminiService, OllamaService, NvidiaService, OpenCodeService
├── prompts/                    Embedded teacher-profile rules-files (teacher-profile.md,
│                               teacher-profile.university.md) for the AI system prompt
├── Converters/                 IValueConverter (message bubble alignment/color, content-type emoji)
├── Assets/                     Icons (avalonia-logo.ico/.png) and screenshots
├── scripts/publish.sh          Local self-contained build → dist/ (gitignored)
└── .github/workflows/release.yml  CI: linux-x64 + win-x64 single-file artifacts on v* tag
```

## Setup

The app reads its configuration from a `.env` file next to the executable (or from real environment variables, which take precedence) using `Microsoft.Extensions.Configuration` + `Options` with DataAnnotations validation — the .NET equivalent of pydantic-settings. On startup it validates settings and fails with a clear message if required keys are missing.

**Authentication is local** — no `CLERK_SECRET_KEY` is needed. The app uses `LocalAuthService` backed by SQLite (`%APPDATA%/learn-assist/users.json` on Windows, `~/.config/learn-assist/` on Linux) with PBKDF2-SHA256 password hashes (never plaintext). There is no email verification; registration goes straight to the app.

Create a `.env` in the project root (gitignored):

```sh
# Optional — Clerk keys (kept so Clerk can be re-enabled later; not needed for local auth)
# CLERK_SECRET_KEY=sk_test_...
# CLERK_PUBLISHABLE_KEY=pk_test_...

# Optional — Google Cloud OAuth (shows the Google sign-in button when configured).
# From Google Cloud Console → Credentials → OAuth client (Desktop app).
# Must register http://127.0.0.1:53174/callback as an authorized redirect URI.
GOOGLE_CLIENT_ID=xxxxx.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-...

# ...or instead of the two above: point directly at the JSON you downloaded
# (OAuth client → download JSON), e.g. client_secret_xxx.apps.googleusercontent.com.json
#GOOGLE_CLIENT_SECRET_FILE=/ruta/a/client_secret_xxx.json

# Optional — OAuth loopback port (default 53174)
OAUTH_REDIRECT_PORT=53174

# Optional — Disable injecting LearningProfile into the AI system prompt
#LEARN_ASSIST_PROFILE_CONTEXT=0
```

Then run:

```sh
dotnet build
dotnet run          # normal launch (install wizard appears on first run)
LEARN_ASSIST_FORCE_INSTALL=1 dotnet run   # force the install wizard in dev
```

> No tests. Release artifacts are produced by CI on `v*` tag push (linux-x64 + win-x64 self-contained single-file). Local equivalent: `scripts/publish.sh` → `dist/`.

## GitHub Packages (NuGet)

The project also publishes a **NuGet package to GitHub Packages**, so it shows up under the repository's **Packages** tab (`https://github.com/Joeldev0208?tab=packages`).

- **Feed:** `https://nuget.pkg.github.com/Joeldev0208/index.json`
- **Package ID:** `learn-assist` (metadata in `learn-Assist.csproj`)
- **Trigger:** `.github/workflows/packages.yml` runs on push to `master` and on `v*` tags. Tag builds use the tag as the version (`v1.2.3` → `1.2.3`); branch builds use a prerelease (`1.0.0-ci.<run>`).

### Install the package

GitHub Packages requires authentication even for public packages. Add the feed once:

```sh
dotnet nuget add source --username Joeldev0208 --password <YOUR_PAT> \
  --store-password-in-clear-text --name github \
  "https://nuget.pkg.github.com/Joeldev0208/index.json"
```

Then reference it in a project:

```xml
<PackageReference Include="learn-assist" Version="1.0.0" />
```

> The token needs the `read:packages` scope (`write:packages` to publish). Create it at GitHub → Settings → Developer settings → Personal access tokens. `NuGet.config` in this repo already maps `learn-assist*` to the GitHub feed so public dependencies still come from nuget.org.

### Publish manually

```sh
dotnet pack learn-Assist.csproj -c Release -o packages
dotnet nuget push "packages/*.nupkg" \
  --source "https://nuget.pkg.github.com/Joeldev0208/index.json" \
  --api-key <YOUR_PAT>
```

> Packages are **private by default**. The workflow tries to make them public for public repos; you can also change visibility later in the package's settings on GitHub.

## Docker / Windows Defender Workaround

En **Windows 11**, el binario self-contained de `learn-Assist` puede ser **bloqueado por Windows Defender** (SmartScreen). Para evitar el bloqueo, usa el `Dockerfile` incluido en el repositorio:

```sh
# 1. Publicar el binario linux-x64 self-contained
dotnet publish -c Release -r linux-x64 --self-contained true -o publish

# 2. Construir la imagen
docker build -t learn-assist .

# 3. Ejecutar (Windows Defender ya no interviene)
docker run --rm -it \
  -e DISPLAY=host.docker.internal:0 \
  -v "${APPDATA}/learn-assist:/root/.config/learn-assist" \
  learn-assist
```

### Autoinicio en Windows 11

Para que la app se ejecute automáticamente al encender el PC:

```sh
# 1. Crear el contenedor con política de reinicio (una sola vez)
docker run -d \
  --name learn-assist \
  --restart always \
  -e DISPLAY=host.docker.internal:0 \
  -v "${APPDATA}/learn-assist:/root/.config/learn-assist" \
  learn-assist
```

```sh
# 2. Configurar Docker Desktop para que arranque con Windows
#    Docker Desktop → Settings → General → ✅ "Start Docker Desktop when you sign in"
```

Con `--restart always` + Docker Desktop iniciándose con Windows, el contenedor se lanza solo en cada arranque.

```sh
# Comandos útiles de gestión
docker ps -a --filter name=learn-assist   # ver estado
docker stop learn-assist                    # detener
docker start learn-assist                   # iniciar
docker logs learn-assist                    # ver logs
docker rm learn-assist                       # eliminar el contenedor
```

Requisitos: [Docker Desktop](https://www.docker.com/products/docker-desktop/) con backend WSL2 y un servidor X para la UI (WSLg en Win11 funciona out-of-the-box, o usa [X410](https://x410.dev/) / VcXsrv).

## Startup & Installation (first-run wizard)

On startup `App.OnFrameworkInitializationCompleted` calls `InstallationService.IsInstalled()`; if not installed (or `LEARN_ASSIST_FORCE_INSTALL=1` in dev) the **InstallView** wizard runs **before** login.

- Copies the running binary (`Environment.ProcessPath`) to a user dir (`~/.local/share/learn-assist` on Linux, `%LOCALAPPDATA%\learn-assist` on Windows) or a system dir (`/opt/learn-assist` via `pkexec`, `C:\Program Files\LearnAssist` via UAC `runas` relaunch).
- Menu entry/shortcut is **always user-level** (Linux `.desktop` → `~/.local/share/applications/`; Windows `.lnk` via PowerShell + `WScript.Shell` COM to the Start Menu), even in system scope, to minimize privilege surface.
- Marker file `~/.config/learn-assist/install.json` (scope + binary path + date); `IsInstalled()` checks the marker **and** that the binary still exists — stale markers offer reinstalls.
- Elevated worker: `Program.cs` branches on `--install-elevated <source> <target>` **before** loading settings / starting Avalonia. Windows UAC relaunch uses `Verb = "runas"`. Don't reorder `Program.Main`.
- Linux `.desktop` icon is the embedded `Assets/avalonia-logo.png`, read at runtime via `AssetLoader` (`avares://...`).

## Authentication (local accounts)

Auth runs on `Services/LocalAuthService.cs` (`IAuthService`): accounts in SQLite (`%APPDATA%/learn-assist/users.json` on Windows, `~/.config/learn-assist/` on Linux), PBKDF2-SHA256 hashes (never plaintext), no email verification — register goes straight to main.

### Email / password (local)

1. **LoginView** — email + password form, authenticates via `LocalAuthService.SignInAsync`.
2. **RegisterView** — email/password/confirm (8+ char password), creates the local account directly.
3. **VerifyEmailView** — wired but **unused** for local accounts (no email verification).
4. **MainWindow** — three-panel UI, launched after successful login.

### Google (Google Cloud OAuth 2.0 — direct)

**Google Cloud OAuth is the default for the Google button.** It runs a direct RFC 8252 authorization-code flow with Google Cloud credentials and creates a local passwordless account for new emails:

- `GoogleOAuthService` builds the authorize URL with `state` + loopback `redirect_uri` (`http://127.0.0.1:<OAUTH_REDIRECT_PORT>/callback`), opens the system browser, captures `code`+`state` via `OAuthLoopbackListener`, exchanges the code for an access token, and fetches `userinfo` (validating `email_verified`).
- `LocalAuthService.SignInWithGoogleAsync(email, name)`: existing email → new session; new email → creates a local account (no password, linked to Google), then signs in.
- No `GOOGLE_CLIENT_ID`/`GOOGLE_CLIENT_SECRET` → the Google buttons are hidden; email/password still works. Alternatively, set `GOOGLE_CLIENT_SECRET_FILE` to the path of the `client_secret_*.json` downloaded from the Google Cloud Console and the credentials are read from it (direct env vars take precedence).

### Remembered local session

After a successful local or Google sign-in, Learn-Assist stores a session record in SQLite. It stores only the local user id, email, and a random session id; passwords are never written to this file. On the next launch, the record is checked against the users table and the app opens directly in the main window when valid. The sign-out button in the main window removes the remembered session so another account can be used.

### Apple (Clerk FAPI)

Apple sign-in is **disabled** for local accounts. `ClerkAuthService.cs` is kept in the repo unused so Clerk can be re-enabled later for a cloud feature.

## Chat / AI

Three-panel layout: **session list (left) / chat (center) / document list (right)**.

- `IAiService` (`AskAsync(message, history)`) with `MockAiService` (hardcoded response) for testing.
- Production providers in `Services/Providers/`:
  - **OpenAiService** — uses the official `OpenAI` NuGet SDK (`ChatClient`).
  - **AnthropicService**, **GeminiService**, **OllamaService**, **NvidiaService**, **OpenCodeService** — call native endpoints via raw `HttpClient` + `JsonDocument`. Ollama is local (`http://localhost:11434`, model `llama3.2`, API key optional). NVIDIA uses the OpenAI-compatible endpoint `https://integrate.api.nvidia.com/v1` (model `nvidia/llama-3.3-nemotron-super-49b-v1`). OpenCode uses its OpenAI-compatible Zen endpoint `https://opencode.ai/zen/v1` (model `big-pickle`) by default; both URL and model can be changed in the AI configuration dialog.
  - Provider implementations are heterogeneous — don't assume SDK usage when editing.
- `AiServiceFactory.Create(config)` returns the correct provider based on `ApiConfig.Provider`.
- Provider selection + API key configured via `ApiConfigView` (modal dialog triggered by `MainViewModel.ConfigureAiRequested`) → encrypted via `ConfigEncryption` (AES-256 + PBKDF2-derived key) to `%APPDATA%/learn-assist/config.enc` (Windows) / `~/.config/learn-assist/config.enc` (Linux).
- `TutorialViewModel` shows a step-through overlay on **first login only** (static `_isFirstLogin` flag in `MainViewModel`). Flow on first login: tutorial → API config dialog → chat. On return with config: chat skips straight to active.
- `SessionPersistenceService` saves/loads `.md` conversation files in a user-chosen directory (default `~/.config/learn-assist/sessions/`); `SessionListViewModel` falls back to hardcoded sample data if empty.
- `ChatViewModel` auto-saves after each assistant response when persistence is configured.
- Chat bubbles with role-based alignment (user right/blue, assistant left/white) + auto-scroll on new messages.

## Learning Profile & AI System Prompt

On first login (or when the profile is incomplete), the user fills a **Learning Profile** (`LearningProfileView`). The profile is saved to SQLite and includes:

- **ProfileType** (Student, University, Professional, Personal) → maps to `LearnerLevel.Student` or `LearnerLevel.University`
- **SemesterContent** — syllabus text extracted from imported files (.txt, .md, .pdf, .docx, .xlsx)
- Other learning preferences

The AI system prompt is built by `ProgramContextBuilder` with 4 blocks:

1. **System** — base assistant behavior
2. **TeacherProfile** — conduct rules from `prompts/teacher-profile.md` (secondary level) or `prompts/teacher-profile.university.md` (university level)
3. **Program** — the user's `SemesterContent` (syllabus)
4. **Material** — imported documents (`documents.json`)

The teacher-profile rules-files are embedded as resources (`<EmbeddedResource Include="prompts/**" />`) and loaded by `TeacherProfileService`. They are cached per level with SHA-256 hash invalidation.

## Uninstall

A dedicated **Uninstall** button (🗑️) is available in the main window header. Clicking it opens a confirmation dialog with the option to remove user data (config + accounts). `UninstallService` handles both **User** and **System** install scopes on **Windows** and **Linux**:

- Removes the installed binary
- Removes menu entry / shortcut
- Removes icon
- Removes marker file (`install.json`)
- Optionally removes user data (config.enc + users.db)

## Architecture

- `MainViewModel` composes child VMs: `SessionList`, `Chat`, `DocumentList`, `Tutorial` — new panels follow this pattern.
- `MainWindow.OnDataContextChanged` subscribes to child VM events (scroll, import dialog, configure-AI, uninstall) — **not** the constructor.
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
| Login / Register (email + password, local accounts) | Done |
| Local auth (PBKDF2-SHA256, SQLite, no email verification) | Done |
| Google OAuth (Google Cloud, auto-creates local account for new emails) | Done |
| Apple (Clerk FAPI) | Disabled (local accounts only) |
| First-run install wizard (user + system scope) | Done |
| Uninstall (User/System scope, Windows + Linux) | Done |
| Chat UI (three-panel) | Done |
| AI service (MockAiService + 6 real providers) | Done |
| API config encryption (AES-256 + PBKDF2) | Done |
| Document import (documents.json persistence) | Done |
| SemesterContent persistence (SQLite learn_assist.db) | Done |
| Session persistence (.md autosave) | Done |
| Teacher-profile prompts (secondary + university) | Done |
| First-login tutorial overlay | Done |
