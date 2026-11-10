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
    private bool IsEnglish => LanguageService.IsEnglish;

    public string WorkspaceLabel => IsEnglish ? "Your workspace" : "Tu espacio de trabajo";
    public string ChatHistoryLabel => IsEnglish ? "Chat history" : "Historial de chats";
    public string NewChatTip => IsEnglish ? "Start a new chat" : "Iniciar un chat nuevo";
    public string ShowHistoryTip => IsEnglish ? "Show chat history" : "Mostrar historial de chats";
    public string ShowResourcesTip => IsEnglish ? "Show resources" : "Mostrar recursos";
    public string ThemeTip => IsEnglish ? "Toggle light/dark theme" : "Cambiar tema claro/oscuro";
    public string ConfigureAiTip => IsEnglish ? "Configure AI provider" : "Configurar proveedor de IA";
    public string SignOutTip => IsEnglish ? "Sign out" : "Cerrar sesión";
    public string UninstallTip => IsEnglish ? "Uninstall application" : "Desinstalar aplicación";
    public string LearningWorkspaceLabel => IsEnglish ? "Learning workspace ·" : "Espacio de aprendizaje ·";
    public string OneStepLabel => IsEnglish ? "One clear step at a time" : "Un paso claro a la vez";
    public string ContinueLearningLabel => IsEnglish ? "Continue learning" : "Continúa aprendiendo";
    public string ChooseGoalLabel => IsEnglish
        ? "Choose one goal. Learn it, practice it, and leave with a clear next step."
        : "Elige un objetivo. Apréndelo, practícalo y termina con un siguiente paso claro.";
    public string UnderstandLabel => IsEnglish ? "What do you want to understand?" : "¿Qué quieres comprender?";
    public string SpecificGoalLabel => IsEnglish
        ? "Keep it specific. The app will guide you through one focused session."
        : "Sé específico. La aplicación te guiará en una sesión enfocada.";
    public string LearningGoalPlaceholder => IsEnglish
        ? "For example: React component state"
        : "Por ejemplo: estado de un componente de React";
    public string StartLearningLabel => IsEnglish ? "Start learning session" : "Iniciar sesión de aprendizaje";
    public string QuickSessionLabel => IsEnglish
        ? "Start a quick 5-minute session"
        : "Iniciar una sesión rápida de 5 minutos";
    public string UnderstandStepLabel => IsEnglish ? "1  Understand" : "1  Comprender";
    public string UnderstandDescription => IsEnglish
        ? "A short explanation without the noise."
        : "Una explicación breve y directa.";
    public string PracticeStepLabel => IsEnglish ? "2  Practice" : "2  Practicar";
    public string PracticeDescription => IsEnglish
        ? "A small task that makes it stick."
        : "Una pequeña tarea para fijarlo.";
    public string ExplainStepLabel => IsEnglish ? "3  Explain" : "3  Explicar";
    public string ExplainDescription => IsEnglish
        ? "Say it back and know what comes next."
        : "Explícalo con tus palabras y descubre qué sigue.";
    public string ResourcesLabel => IsEnglish ? "Resources" : "Recursos";
    public string AddUrlLabel => IsEnglish ? "Add URL" : "Agregar URL";
    public string UseInAiLabel => IsEnglish ? "Use in AI" : "Usar en la IA";
    public string ThinkingLabel => IsEnglish ? "Thinking..." : "Pensando...";
    public string MessagePlaceholder => IsEnglish ? "What do you want to learn?" : "¿Qué quieres aprender?";
    public string CopyLabel => IsEnglish ? "Copy" : "Copiar";
    public string ResourceOnlyLabel => IsEnglish ? "Resources only" : "Solo recursos";
    public string SessionCompleteLabel => IsEnglish
        ? "Nice work. You completed this focused session. Return when you are ready for the next idea."
        : "Buen trabajo. Completaste esta sesión enfocada. Regresa cuando estés listo para la siguiente idea.";
    public string DeleteSessionTip => IsEnglish ? "Delete session" : "Eliminar sesión";
    public string AiPermissionsTip => IsEnglish ? "AI permissions" : "Permisos de la IA";
    public string CreateExamTip => IsEnglish ? "Create exam" : "Crear examen";
    public string NewUrlPlaceholder => IsEnglish ? "https://example.com/article" : "https://ejemplo.com/artículo";

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

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WorkspaceLabel));
        OnPropertyChanged(nameof(ChatHistoryLabel));
        OnPropertyChanged(nameof(NewChatTip));
        OnPropertyChanged(nameof(ShowHistoryTip));
        OnPropertyChanged(nameof(ShowResourcesTip));
        OnPropertyChanged(nameof(ThemeTip));
        OnPropertyChanged(nameof(ConfigureAiTip));
        OnPropertyChanged(nameof(SignOutTip));
        OnPropertyChanged(nameof(UninstallTip));
        OnPropertyChanged(nameof(LearningWorkspaceLabel));
        OnPropertyChanged(nameof(OneStepLabel));
        OnPropertyChanged(nameof(ContinueLearningLabel));
        OnPropertyChanged(nameof(ChooseGoalLabel));
        OnPropertyChanged(nameof(UnderstandLabel));
        OnPropertyChanged(nameof(SpecificGoalLabel));
        OnPropertyChanged(nameof(LearningGoalPlaceholder));
        OnPropertyChanged(nameof(StartLearningLabel));
        OnPropertyChanged(nameof(QuickSessionLabel));
        OnPropertyChanged(nameof(UnderstandStepLabel));
        OnPropertyChanged(nameof(UnderstandDescription));
        OnPropertyChanged(nameof(PracticeStepLabel));
        OnPropertyChanged(nameof(PracticeDescription));
        OnPropertyChanged(nameof(ExplainStepLabel));
        OnPropertyChanged(nameof(ExplainDescription));
        OnPropertyChanged(nameof(ResourcesLabel));
        OnPropertyChanged(nameof(AddUrlLabel));
        OnPropertyChanged(nameof(UseInAiLabel));
        OnPropertyChanged(nameof(ThinkingLabel));
        OnPropertyChanged(nameof(MessagePlaceholder));
        OnPropertyChanged(nameof(CopyLabel));
        OnPropertyChanged(nameof(ResourceOnlyLabel));
        OnPropertyChanged(nameof(SessionCompleteLabel));
        OnPropertyChanged(nameof(UpdateButtonText));
        OnPropertyChanged(nameof(UpdateMessage));
        OnPropertyChanged(nameof(DeleteSessionTip));
        OnPropertyChanged(nameof(AiPermissionsTip));
        OnPropertyChanged(nameof(CreateExamTip));
        OnPropertyChanged(nameof(NewUrlPlaceholder));
        Tutorial.RefreshLanguage();
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
        LanguageService.LanguageChanged += OnLanguageChanged;

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
                UpdateMessage = IsEnglish
                    ? $"Update available: v{update.LatestVersion}. Restart the app to install it."
                    : $"Actualización disponible: v{update.LatestVersion}. Reinicia la aplicación para instalarla.";
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
            UpdateMessage = IsEnglish
                ? "Update ready. Restarting to apply it..."
                : "Actualización lista. Reiniciando para aplicarla...";
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
        LanguageService.LanguageChanged -= OnLanguageChanged;
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
