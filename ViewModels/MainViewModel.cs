using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly string _userEmail;
    private readonly string _userKey;
    private readonly bool _hasAiConfig;
    private readonly LearningProfileService? _profileService;
    private SessionPersistenceService? _persistence;
    private readonly UpdateService? _updateService;
    private readonly ResourceModeStore _resourceModeStore;
    private readonly AiPermissionsStore _permissionsStore;
    private int _promptUpdateVersion;
    private Action? _configureAiRequested;
    private bool _configureAiPending;
    private bool _loadingResourceMode;

    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }
    public DocumentViewerViewModel DocumentViewer { get; }
    public TutorialViewModel Tutorial { get; }
    public ExamViewModel Exam { get; }
    public LearningProfile? LearningProfile { get; private set; }
    public string UserId => _userKey;

    public event Action? ConfigureAiRequested
    {
        add
        {
            _configureAiRequested += value;
            if (_configureAiPending && value is not null)
            {
                _configureAiPending = false;
                value();
            }
        }
        remove => _configureAiRequested -= value;
    }
    public event Action? RestartRequested;
    public event Action? SignOutRequested;
    public event Action? EditLearningProfileRequested;
    public event Action? UninstallRequested;
    public event Action? ExamRequested;
    public event Action? EditPermissionsRequested;
    public IRelayCommand SignOutCommand { get; }

    /// <summary>
    /// Permisos vigentes de la IA. Los edita el diálogo «Permisos de la IA» y
    /// se aplican al reconstruir el system prompt.
    /// </summary>
    public AiPermissions Permissions { get; private set; } = AiPermissions.Default;

    public string ThemeGlyph => ThemeService.IsDark ? "☀️" : "🌙";

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial string? UpdateMessage { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial bool IsCompactLayout { get; set; }

    [ObservableProperty]
    public partial bool IsSessionsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsResourcesVisible { get; set; } = true;

    /// <summary>
    /// Modo «Solo recursos»: cuando está activo, la IA solo puede afirmar
    /// hechos que aparezcan en los documentos marcados. La preferencia se
    /// guarda en <c>resource-mode.json</c> y se aplica al reconstruir el prompt.
    /// </summary>
    [ObservableProperty]
    public partial bool UseOnlyResources { get; set; }

    partial void OnUseOnlyResourcesChanged(bool value)
    {
        if (_loadingResourceMode)
            return;

        _resourceModeStore.Save(value);
        _ = UpdateChatSystemPromptAsync();
    }

    [ObservableProperty]
    public partial string LearningGoal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLearningSessionActive { get; set; }

    [ObservableProperty]
    public partial int LearningStep { get; set; } = 1;

/*     [ObservableProperty]
    public partial bool IsLearningSessionComplete { get; set; }

    public string LearningStepLabel => $"Paso {LearningStep} de 3";
    public string LearningProgressText => LearningStep switch
    {
        1 => "Comprende la idea",
        2 => "Practica con un ejemplo pequeño",
        _ => "Explícalo con tus propias palabras",
    };
    public string LearningActionText => LearningStep == 3 ? "Terminar sesión" : "Siguiente paso";
 */
    [ObservableProperty]
    public partial string? UpdateError { get; set; }

    public string UpdateButtonText => IsUpdating ? "Descargando..." : "Actualizar y reiniciar";

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(nameof(UpdateButtonText));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.ToggleAndRestart();
    }

    [RelayCommand]
    private void ReconfigureAi()
    {
        _configureAiRequested?.Invoke();
    }

    private void SignOut()
    {
        SignOutRequested?.Invoke();
    }

    [RelayCommand]
    private void Uninstall()
    {
        var ok = UninstallService.Uninstall(removeUserData: true);
        if (ok)
        {
            UninstallRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void ToggleSessions()
    {
        IsSessionsVisible = !IsSessionsVisible;
    }

    [RelayCommand]
    private void ToggleResources()
    {
        IsResourcesVisible = !IsResourcesVisible;
    }

    [RelayCommand]
    private async Task StartLearningSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(LearningGoal))
            LearningGoal = "A topic I want to understand";

        IsLearningSessionActive = true;
/*         IsLearningSessionComplete = false; */
        LearningStep = 1;
        Chat.AddWelcomeMessage();
        Chat.MessageText = $"Help me learn {LearningGoal.Trim()} in a short focused session.";
        Chat.SendMessageCommand.Execute(null);
        await SaveLearningProfileAsync();
    }

    [RelayCommand]
    private async Task StartQuickSessionAsync()
    {
        LearningGoal = "one useful idea in five minutes";
        await StartLearningSessionAsync();
    }

    [RelayCommand]
/*     private async Task CompleteLearningStepAsync()
    {
        if (LearningStep < 3)
        {
            LearningStep++;
        }
        else
        {
            IsLearningSessionComplete = true;
        }

        await SaveLearningProfileAsync();
    } */

 /*    [RelayCommand]
    private async Task EndLearningSessionAsync()
    {
        IsLearningSessionActive = false;
        IsLearningSessionComplete = false;
        LearningStep = 1;
        await SaveLearningProfileAsync();
    } */

/*     [RelayCommand] */
    private void NeedLearningHelp()
    {
        Chat.MessageText = $"Explain {LearningGoal} with a simpler example and one short practice question.";
    }

/*     partial void OnLearningStepChanged(int value)
    {
        OnPropertyChanged(nameof(LearningStepLabel));
        OnPropertyChanged(nameof(LearningProgressText));
        OnPropertyChanged(nameof(LearningActionText));
    }
 */
    public MainViewModel()
        : this(new MockAiService(), "preview@example.com")
    {
        IsLearningSessionActive = true;
        Tutorial.IsVisible = false;
        LearningGoal = "C# async and await";
        Chat.Messages.Add(new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Can you explain how async and await work in C#?",
        });
        Chat.Messages.Add(new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = """
                ## Async and await

                `async` lets a method perform asynchronous work without blocking the UI thread.

                ```csharp
                var result = await LoadAsync();
                ```

                The `await` keyword pauses this method until the operation completes, then continues with the result.
                """,
        });
    }

    public MainViewModel(
        IAiService aiService,
        string userEmail,
        string? userId = null,
        ApiConfig? config = null,
        SessionPersistenceService? persistence = null,
        UpdateService? updateService = null,
        LearningProfileService? profileService = null,
        LearningProfile? learningProfile = null,
        ResourceModeStore? resourceModeStore = null,
        AiPermissionsStore? permissionsStore = null)
    {
        SignOutCommand = new RelayCommand(SignOut);
        _userEmail = userEmail;
        _userKey = string.IsNullOrWhiteSpace(userId) ? userEmail.Trim().ToLowerInvariant() : userId;
        _hasAiConfig = config is not null;
        _profileService = profileService;
        _persistence = persistence;
        _updateService = updateService;
        _resourceModeStore = resourceModeStore ?? new ResourceModeStore();
        _permissionsStore = permissionsStore ?? new AiPermissionsStore();
        LearningProfile = learningProfile;

        Chat = new ChatViewModel(aiService, persistence);
        SessionList = new SessionListViewModel(persistence);
        DocumentList = new DocumentListViewModel();
        DocumentViewer = new DocumentViewerViewModel();
        Exam = new ExamViewModel(aiService, LearningProfile, DocumentList.Documents);
        Tutorial = new TutorialViewModel();

        // Cargar la preferencia sin guardarla de vuelta ni reconstruir el prompt
        // dos veces: el prompt se construye una sola vez al final del constructor.
        _loadingResourceMode = true;
        UseOnlyResources = _resourceModeStore.Load();
        _loadingResourceMode = false;

        // Los permisos se cargan antes de construir el primer prompt para que
        // la IA nunca reciba un documento no autorizado.
        Permissions = _permissionsStore.Load();

        _ = UpdateChatSystemPromptAsync();

        SessionList.CurrentUserEmail = userEmail;
        DocumentList.DocumentSelected += OnDocumentSelected;
        DocumentViewer.Closed += OnDocumentViewerClosed;
        SessionList.SessionSelected += OnSessionSelected;
        SessionList.NewSessionRequested += OnNewSessionRequested;

        DocumentList.Documents.CollectionChanged += async (_, _) => await UpdateChatSystemPromptAsync();
        DocumentList.ContentRepaired += () => _ = UpdateChatSystemPromptAsync();
        DocumentList.ContextSelectionChanged += () => _ = UpdateChatSystemPromptAsync();

        Tutorial.TutorialFinished += OnTutorialFinished;

        if (!TutorialStateService.HasBeenSeen(_userKey))
            Tutorial.IsVisible = true;
        else if (config is null)
        {
            _configureAiPending = true;
        }

        if (_updateService is { IsSupported: true })
            CheckForUpdates();
    }

    private void OnDocumentSelected(UserDocument document)
    {
        _ = DocumentViewer.OpenAsync(document);
    }

    private void OnDocumentViewerClosed()
    {
        DocumentList.SelectedDocument = null;
    }

    public async Task InitializeAsync()
    {
        if (_profileService is not null && !string.IsNullOrWhiteSpace(_userKey))
        {
            LearningProfile ??= await _profileService.LoadAsync(_userKey);
            if (LearningProfile is not null)
                ApplyLearningProfile(LearningProfile);
        }

        await SessionList.LoadSessionsAsync();
        await DocumentList.LoadDocumentsAsync();

        if (SessionList.ActiveSession is null)
        {
            var initialSession = SessionList.CreateNewSession();
            Chat.SetCurrentSession(initialSession);
        }
    }

    private async Task UpdateChatSystemPromptAsync(CancellationToken ct = default)
    {
        var updateVersion = Interlocked.Increment(ref _promptUpdateVersion);

        try
        {
            var level = MapToLearnerLevel(LearningProfile);
            var programContent = LearningProfile?.SemesterContent;

            // Filtrado real: un documento no autorizado nunca llega al prompt,
            // aunque esté marcado con «Usar en la IA» en el panel de recursos.
            var candidates = DocumentList.Documents
                .Where(d => d.IsIncludedInContext)
                .Where(d => !string.IsNullOrWhiteSpace(d.Content));
            var materials = AiPermissionPolicy.FilterDocuments(candidates, Permissions).ToList();

            var resourcesOnly = UseOnlyResources;

            // Actualizar el guardia del chat de inmediato, sin esperar a que
            // termine de construirse el prompt.
            Chat.SetResourceMode(resourcesOnly, materials.Count > 0);

            var built = await ProgramContextBuilder.Instance.BuildAsync(
                level,
                programContent,
                materials,
                ct,
                resourcesOnly);

            if (updateVersion != Volatile.Read(ref _promptUpdateVersion))
                return;

            var languageInstruction = string.Equals(
                LearningProfile?.PreferredLanguage,
                "en",
                StringComparison.OrdinalIgnoreCase)
                || (LearningProfile is null && LanguageService.IsEnglish)
                ? "Idioma de respuesta: English. Answer in natural English."
                : "Idioma de respuesta: Español. Responde en español estándar y conserva tildes, ñ y signos de puntuación.";

            var permissionBlock = AiPermissionPolicy.BuildPromptBlock(Permissions);

            Chat.SetSystemPrompt(
                $"{built.SystemPrompt}\n\n{permissionBlock}\n\n{languageInstruction}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static LearnerLevel MapToLearnerLevel(LearningProfile? profile)
    {
        if (profile is null)
            return LearnerLevel.Student;

        return profile.ProfileType switch
        {
            LearningProfileType.University => LearnerLevel.University,
            _ => LearnerLevel.Student,
        };
    }

    private async void CheckForUpdates()
    {
        try
        {
            var update = await _updateService!.CheckForUpdatesAsync();
            if (update is { IsUpdateAvailable: true })
            {
                IsUpdateAvailable = true;
                UpdateMessage = $"Actualización disponible: v{update.LatestVersion}. Reinicia la aplicación para instalarla.";
            }
        }
        catch
        {
            // Updates must never break the app — fail silently.
        }
    }

    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (_updateService is not { IsSupported: true } || !IsUpdateAvailable)
            return;

        IsUpdating = true;
        UpdateError = null;

        try
        {
            await _updateService.StageUpdateAsync();
            UpdateMessage = "Actualización lista. Reiniciando para aplicarla...";
            RestartRequested?.Invoke();
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        IsUpdateAvailable = false;
        UpdateError = null;
    }

    [RelayCommand]
    private void EditLearningProfile()
    {
        EditLearningProfileRequested?.Invoke();
    }

    [RelayCommand]
    private void EditPermissions()
    {
        EditPermissionsRequested?.Invoke();
    }

    /// <summary>
    /// Guarda los permisos confirmados y reconstruye el prompt con el nuevo
    /// filtro de documentos. Lo llama el diálogo «Permisos de la IA».
    /// </summary>
    public async Task ApplyPermissionsAsync(AiPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Permissions = permissions.Clone();
        _permissionsStore.Save(Permissions);
        OnPropertyChanged(nameof(Permissions));

        await UpdateChatSystemPromptAsync();
    }

    [RelayCommand]
    private void OpenExam()
    {
        ExamRequested?.Invoke();
    }

    public void ApplyLearningProfile(LearningProfile profile)
    {
        LearningProfile = profile;
        LearningGoal = profile.LearningGoal;
        LearningStep = profile.LearningStep;
        IsLearningSessionActive = profile.IsLearningSessionActive;
/*         IsLearningSessionComplete = profile.IsLearningSessionComplete; */
        OnPropertyChanged(nameof(LearningProfile));

        _ = UpdateChatSystemPromptAsync();
    }

    private void OnTutorialFinished()
    {
        TutorialStateService.MarkSeen(_userKey);

        if (!_hasAiConfig)
            _configureAiRequested?.Invoke();
    }

    public async Task ApplyConfigAsync(ApiConfig config)
    {
        var aiService = AiServiceFactory.Create(config);

        SessionPersistenceService? persistence = null;
        if (!string.IsNullOrWhiteSpace(_userKey))
        {
            persistence = SessionPersistenceService.ForUser(config?.SessionsDirectory, _userKey);
            _persistence = persistence;
            await SessionList.SetPersistenceAsync(persistence);
        }

        Chat.SetAiService(aiService, persistence);
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _promptUpdateVersion);
        Chat.Dispose();
    }

    private void OnSessionSelected(Models.ChatSession session)
    {
        IsLearningSessionActive = true;
/*         IsLearningSessionComplete = false; */
        Chat.LoadSession(session);
    }

    private async Task SaveLearningProfileAsync()
    {
        if (_profileService is null || string.IsNullOrWhiteSpace(_userKey))
            return;

        var profile = LearningProfile ?? new LearningProfile { UserId = _userKey };
        profile.LearningGoal = LearningGoal;
        profile.IsLearningSessionActive = IsLearningSessionActive;
        profile.LearningStep = LearningStep;
/*         profile.IsLearningSessionComplete = IsLearningSessionComplete; */
        await _profileService.SaveAsync(profile);
        LearningProfile = profile;
    }

    private void OnNewSessionRequested()
    {
        var session = SessionList.CreateNewSession();
        Chat.SetCurrentSession(session);
        Chat.AddWelcomeMessage();
    }
}
