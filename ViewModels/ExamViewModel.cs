using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

/// <summary>
/// ViewModel para generar, cargar y guardar exámenes a partir del programa
/// de estudios y los documentos importados del usuario.
/// </summary>
public partial class ExamViewModel : ViewModelBase
{
    private readonly ExamService _examService;
    private readonly LearningProfile? _learningProfile;
    private readonly IReadOnlyList<UserDocument>? _documents;
    private readonly IWebContentReader _webContentReader;

    /// <summary>
    /// Crea una instancia con el servicio de IA dado.
    /// </summary>
    /// <param name="aiService">Servicio de IA usado para generar el examen.</param>
    /// <param name="learningProfile">Perfil del aprendiz (opcional).</param>
    /// <param name="documentos">Documentos importados del usuario (opcional).</param>
    /// <param name="webContentReader">Lector de contenido web (opcional, para pruebas).</param>
    public ExamViewModel(IAiService aiService, LearningProfile? learningProfile = null, IReadOnlyList<UserDocument>? documentos = null, IWebContentReader? webContentReader = null)
    {
        _examService = new ExamService(aiService ?? throw new ArgumentNullException(nameof(aiService)));
        _learningProfile = learningProfile;
        _documents = documentos;
        _webContentReader = webContentReader ?? WebContentReader.Instance;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Constructor para tiempo de diseño. Usa el servicio de IA simulado
    /// (<see cref="MockAiService"/>) equivalente a <see cref="ExamService.Instance"/>.
    /// </summary>
    public ExamViewModel() : this(new MockAiService())
    {
    }

    [ObservableProperty]
    public partial int QuestionCount { get; set; } = 5;

    [ObservableProperty]
    public partial QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

    [ObservableProperty]
    public partial bool UseProgramContent { get; set; } = true;

    [ObservableProperty]
    public partial bool UseDocuments { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeSolutions { get; set; }

    [ObservableProperty]
    public partial Exam? CurrentExam { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? ExamTitle { get; set; }

    /// <summary>
    /// URL nueva que el usuario quiere agregar como fuente.
    /// </summary>
    [ObservableProperty]
    public partial string? NewUrl { get; set; }

    private bool IsEnglish => LanguageService.IsEnglish;

    public string WindowTitle => IsEnglish ? "Create Exam" : "Crear Examen";
    public string ExamSectionTitle => IsEnglish ? "Create Exam" : "Crear examen";
    public string ExamTitleLabel => IsEnglish ? "Exam Title" : "Título del examen";
    public string QuestionCountLabel => IsEnglish ? "Number of Questions" : "Número de preguntas";
    public string QuestionTypeLabel => IsEnglish ? "Question Type" : "Tipo de pregunta";
    public string UseProgramContentContent => IsEnglish ? "Use study program" : "Usar programa de estudios";
    public string UseDocumentsContent => IsEnglish ? "Use imported documents" : "Usar documentos importados";
    public string IncludeSolutionsContent => IsEnglish ? "Include solutions" : "Incluir soluciones";
    public string UseUrlsContent => IsEnglish ? "Use web pages (URL)" : "Usar páginas web (URL)";
    public string AddUrlSectionTitle => IsEnglish ? "Add URL" : "Agregar URL";
    public string NewUrlPlaceholder => IsEnglish ? "https://example.com/article" : "https://ejemplo.com/artículo";
    public string AddUrlButtonContent => IsEnglish ? "Add" : "Agregar";
    public string UrlsAddedLabel => IsEnglish ? "Added URLs:" : "URLs agregadas:";
    public string RemoveUrlButtonContent => "×";
    public string GenerateButtonContent => IsEnglish ? "Generate Exam" : "Generar examen";
    public string ClearButtonContent => IsEnglish ? "Clear" : "Limpiar";
    public string SaveExamButtonContent => IsEnglish ? "Save Exam" : "Guardar examen";
    public string LoadExamSectionTitle => IsEnglish ? "Load Existing Exam" : "Cargar examen existente";
    public string LoadExamButtonContent => IsEnglish ? "Load" : "Cargar";
    public string ExamQuestionsFallback => IsEnglish ? "Exam Questions" : "Preguntas del examen";
    public string QuestionsCountFormat => IsEnglish ? "({0} questions)" : "({0} preguntas)";

    /// <summary>
    /// Texto formateado del conteo de preguntas, traducido según el idioma.
    /// </summary>
    public string QuestionsCountText =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? string.Empty
            : string.Format(QuestionsCountFormat, CurrentExam.Questions.Count);
    public string CorrectFeedback => IsEnglish ? "Correct!" : "¡Correcto!";
    public string IncorrectPrefix => IsEnglish ? "Incorrect. Correct answer: " : "Incorrecto. Respuesta correcta: ";
    public string SolutionPrefix => IsEnglish ? "Correct answer: " : "Respuesta correcta: ";
    public string ScoreSectionTitle => IsEnglish ? "Result" : "Resultado";
    public string VerifyButtonContent => IsEnglish ? "Verify Answers" : "Verificar respuestas";
    public string ScoreNoQuestions => IsEnglish ? "No questions" : "Sin preguntas";
    public string VerifyExcellent => IsEnglish ? "Excellent! You answered all {0} questions correctly." : "¡Excelente! Respondiste correctamente todas las {0} preguntas.";
    public string VerifyProgress => IsEnglish ? "You have {0} of {1} correct answers." : "Llevas {0} de {1} respuestas correctas.";
    public string GenerateNoSource => IsEnglish ? "Select at least one source: the program, the documents, or at least one URL." : "Seleccione al menos una fuente: el programa, los documentos o al menos una URL.";
    public string GenerateNoProgramContent => IsEnglish ? "No study program content available to generate the exam." : "No hay contenido de programa disponible para generar el examen.";
    public string GenerateNoDocuments => IsEnglish ? "No documents available to generate the exam." : "No hay documentos disponibles para generar el examen.";
    public string GenerateFailed => IsEnglish ? "Could not generate the exam: {0}" : "No se pudo generar el examen: {0}";
    public string SaveFailed => IsEnglish ? "Could not save the exam: {0}" : "No se pudo guardar el examen: {0}";
    public string LoadNotFound => IsEnglish ? "The requested exam was not found." : "No se encontró el examen solicitado.";
    public string LoadFailed => IsEnglish ? "Could not load the exam: {0}" : "No se pudo cargar el examen: {0}";
    public string AddUrlInvalid => IsEnglish ? "Enter a valid URL." : "Ingrese una URL válida.";
    public string AddUrlScheme => IsEnglish ? "The URL must start with http:// or https://" : "La URL debe comenzar con http:// o https://";
    public string AddUrlDuplicate => IsEnglish ? "This URL was already added." : "Esta URL ya fue agregada.";
    public string GeneratingOverlay => IsEnglish ? "Generating exam..." : "Generando examen...";

    /// <summary>
    /// URLs agregadas como fuentes adicionales para el examen.
    /// El contenido se descarga al generar el examen.
    /// </summary>
    public ObservableCollection<string> Urls { get; } = [];

    /// <summary>
    /// Indica si se debe intentar descargar contenido de las URLs agregadas.
    /// </summary>
    [ObservableProperty]
    public partial bool UseUrls { get; set; }

    /// <summary>
    /// Todos los valores del enum <see cref="QuestionType"/>, para binding de enum en XAML.
    /// </summary>
    public IReadOnlyList<QuestionType> QuestionTypes { get; } =
        Enum.GetValues<QuestionType>();

    /// <summary>
    /// Indica si se puede generar un examen en este momento.
    /// </summary>
    public bool CanGenerate =>
        !IsLoading
        && (UseProgramContent || UseDocuments || (UseUrls && Urls.Count > 0))
        && (!UseProgramContent || !string.IsNullOrWhiteSpace(_learningProfile?.SemesterContent))
        && (!UseDocuments || (_documents?.Any(d => d.IsIncludedInContext && !string.IsNullOrWhiteSpace(d.Content)) ?? false))
        && (!UseUrls || Urls.Count > 0);

    private bool CanSaveExam => CurrentExam is not null && !IsLoading;

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ExamSectionTitle));
        OnPropertyChanged(nameof(ExamTitleLabel));
        OnPropertyChanged(nameof(QuestionCountLabel));
        OnPropertyChanged(nameof(QuestionTypeLabel));
        OnPropertyChanged(nameof(UseProgramContentContent));
        OnPropertyChanged(nameof(UseDocumentsContent));
        OnPropertyChanged(nameof(IncludeSolutionsContent));
        OnPropertyChanged(nameof(UseUrlsContent));
        OnPropertyChanged(nameof(AddUrlSectionTitle));
        OnPropertyChanged(nameof(NewUrlPlaceholder));
        OnPropertyChanged(nameof(AddUrlButtonContent));
        OnPropertyChanged(nameof(UrlsAddedLabel));
        OnPropertyChanged(nameof(GenerateButtonContent));
        OnPropertyChanged(nameof(ClearButtonContent));
        OnPropertyChanged(nameof(SaveExamButtonContent));
        OnPropertyChanged(nameof(LoadExamSectionTitle));
        OnPropertyChanged(nameof(LoadExamButtonContent));
        OnPropertyChanged(nameof(ExamQuestionsFallback));
        OnPropertyChanged(nameof(QuestionsCountFormat));
        OnPropertyChanged(nameof(CorrectFeedback));
        OnPropertyChanged(nameof(IncorrectPrefix));
        OnPropertyChanged(nameof(SolutionPrefix));
        OnPropertyChanged(nameof(ScoreSectionTitle));
        OnPropertyChanged(nameof(VerifyButtonContent));
        OnPropertyChanged(nameof(ScoreNoQuestions));
        OnPropertyChanged(nameof(VerifyExcellent));
        OnPropertyChanged(nameof(VerifyProgress));
        OnPropertyChanged(nameof(GenerateNoSource));
        OnPropertyChanged(nameof(GenerateNoProgramContent));
        OnPropertyChanged(nameof(GenerateNoDocuments));
        OnPropertyChanged(nameof(GenerateFailed));
        OnPropertyChanged(nameof(SaveFailed));
        OnPropertyChanged(nameof(LoadNotFound));
        OnPropertyChanged(nameof(LoadFailed));
        OnPropertyChanged(nameof(AddUrlInvalid));
        OnPropertyChanged(nameof(AddUrlScheme));
        OnPropertyChanged(nameof(AddUrlDuplicate));
        OnPropertyChanged(nameof(GeneratingOverlay));
        OnPropertyChanged(nameof(QuestionsCountText));
    }

    private bool CanLoadExam => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    public async Task GenerateExamAsync()
    {
        if (IsLoading)
            return;

        ErrorMessage = null;

        if (!UseProgramContent && !UseDocuments && !(UseUrls && Urls.Count > 0))
        {
            ErrorMessage = GenerateNoSource;
            return;
        }

        var level = MapToLearnerLevel(_learningProfile);
        var programContent = UseProgramContent ? _learningProfile?.SemesterContent ?? string.Empty : string.Empty;
        var documents = UseDocuments
            ? _documents?.Where(d => d.IsIncludedInContext).ToList() ?? []
            : [];

        // Descargar contenido de las URLs agregadas (en paralelo, acumulando errores)
        if (UseUrls && Urls.Count > 0)
        {
            ErrorMessage = null;
            var urlErrors = new List<string>();

            async Task<UserDocument?> FetchUrlDocumentAsync(string url)
            {
                var result = await _webContentReader.FetchAsync(url);
                if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
                {
                    urlErrors.Add(result.Error ?? $"No se pudo extraer contenido de: {url}");
                    return null;
                }
                return new UserDocument
                {
                    Id = Guid.NewGuid(),
                    Name = $"[Web] {url}",
                    Type = "web",
                    Content = result.Text,
                    LocalPath = url,
                    ContentType = DocumentContentType.Document
                };
            }

            var fetchTasks = Urls.Select(FetchUrlDocumentAsync).ToList();
            await Task.WhenAll(fetchTasks);

            foreach (var task in fetchTasks)
            {
                var doc = task.Result;
                if (doc is not null)
                    documents.Add(doc);
            }

            if (urlErrors.Count > 0)
            {
                var succeeded = fetchTasks.Count(t => t.Result is not null);
                ErrorMessage = urlErrors.Count == 1
                    ? urlErrors[0]
                    : $"Se extrajo de {succeeded} de {fetchTasks.Count} URL(es). Errores: {string.Join("; ", urlErrors)}";
            }

            if (documents.Count == 0 && !UseProgramContent && (_documents?.Count ?? 0) == 0)
            {
                return;
            }
        }

        if (UseProgramContent && string.IsNullOrWhiteSpace(programContent))
        {
            ErrorMessage = GenerateNoProgramContent;
            return;
        }

        if (UseDocuments && documents.Count == 0)
        {
            ErrorMessage = GenerateNoDocuments;
            return;
        }

        IsLoading = true;
        try
        {
            var count = Math.Clamp(QuestionCount, 1, 20);
            var exam = await _examService.GenerateExamAsync(
                level,
                programContent,
                documents,
                count,
                QuestionType,
                IncludeSolutions);

            CurrentExam = exam;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(GenerateFailed, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveExam))]
    public async Task SaveExamAsync()
    {
        if (CurrentExam is null)
            return;

        ErrorMessage = null;
        IsLoading = true;
        try
        {
            CurrentExam.UserId = _learningProfile?.UserId ?? string.Empty;
            CurrentExam.Title = string.IsNullOrWhiteSpace(ExamTitle)
                ? CurrentExam.Title
                : ExamTitle.Trim();

            await _examService.SaveExamAsync(CurrentExam);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(SaveFailed, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadExam))]
    public async Task LoadExamAsync(string examId)
    {
        if (string.IsNullOrWhiteSpace(examId))
            return;

        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var exam = await _examService.LoadExamAsync(examId);
            CurrentExam = exam;
            ErrorMessage = exam is null ? LoadNotFound : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(LoadFailed, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Porcentaje de preguntas respondidas correctamente (0-100).
    /// Solo cuenta preguntas con respuesta correcta conocida (modo solución o IA la proporcionó).
    /// </summary>
    public double ScorePercentage =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? 0
            : (double)CurrentExam.Questions.Count(q => q.IsCorrect) / CurrentExam.Questions.Count * 100;

    /// <summary>
    /// Texto formateado del puntaje para mostrar en la UI (ej. "67% — 2 de 3 correctas").
    /// </summary>
    public string ScoreText =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? ScoreNoQuestions
            : $"{ScorePercentage:F0}% — {CorrectAnswersCount} de {TotalQuestionsCount} correctas";

    /// <summary>
    /// Número de preguntas respondidas correctamente.
    /// </summary>
    public int CorrectAnswersCount =>
        CurrentExam is null ? 0 : CurrentExam.Questions.Count(q => q.IsCorrect);

    /// <summary>
    /// Número total de preguntas del examen actual.
    /// </summary>
    public int TotalQuestionsCount =>
        CurrentExam is null ? 0 : CurrentExam.Questions.Count;

    /// <summary>
    /// Indica si todas las preguntas han sido respondidas.
    /// </summary>
    public bool AllQuestionsAnswered =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? false
            : CurrentExam.Questions.All(q => q.IsAnswered);

    [RelayCommand]
    public void Clear()
    {
        CurrentExam = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void VerifyExam()
    {
        // La verificación es reactiva: IsCorrect e IsIncorrect son propiedades computadas
        // que se evalúan automáticamente cuando SelectedOptionIndex o UserAnswer cambian.
        // Este comando existe para notificar al usuario que se han evaluado las respuestas.
        ErrorMessage = CorrectAnswersCount == TotalQuestionsCount
            ? string.Format(VerifyExcellent, TotalQuestionsCount)
            : string.Format(VerifyProgress, CorrectAnswersCount, TotalQuestionsCount);
    }

    [RelayCommand]
    private void AddUrl()
    {
        var url = NewUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            ErrorMessage = AddUrlInvalid;
            return;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = AddUrlScheme;
            return;
        }

        if (Urls.Contains(url))
        {
            ErrorMessage = AddUrlDuplicate;
            return;
        }

        Urls.Add(url);
        NewUrl = null;
        ErrorMessage = null;
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveUrl(string url)
    {
        if (url is not null)
        {
            Urls.Remove(url);
            OnPropertyChanged(nameof(CanGenerate));
            GenerateExamCommand.NotifyCanExecuteChanged();
        }
    }

    private static bool IsValidUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static LearnerLevel MapToLearnerLevel(LearningProfile? profile) =>
        profile?.ProfileType == LearningProfileType.University
            ? LearnerLevel.University
            : LearnerLevel.Student;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
        SaveExamCommand.NotifyCanExecuteChanged();
        LoadExamCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseProgramContentChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseDocumentsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
    }

    private Exam? _previousExam;

    partial void OnCurrentExamChanged(Exam? value)
    {
        // Desuscribirse de las preguntas del examen anterior para evitar fugas de memoria
        if (_previousExam is not null)
        {
            foreach (var q in _previousExam.Questions)
                q.PropertyChanged -= OnQuestionPropertyChanged;
        }

        // Suscribirse a las preguntas del nuevo examen
        if (value is not null)
        {
            foreach (var q in value.Questions)
                q.PropertyChanged += OnQuestionPropertyChanged;
        }

        _previousExam = value;

        SaveExamCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ScorePercentage));
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(CorrectAnswersCount));
        OnPropertyChanged(nameof(TotalQuestionsCount));
        OnPropertyChanged(nameof(AllQuestionsAnswered));
        OnPropertyChanged(nameof(QuestionsCountText));
    }

    private void OnQuestionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Cuando cambia SelectedOptionIndex o UserAnswer de cualquier pregunta,
        // notificar que las propiedades computadas del ViewModel cambiaron.
        if (e.PropertyName is nameof(ExamQuestion.SelectedOptionIndex)
            or nameof(ExamQuestion.UserAnswer)
            or nameof(ExamQuestion.IsCorrect)
            or nameof(ExamQuestion.IsIncorrect)
            or nameof(ExamQuestion.IsAnswered))
        {
            OnPropertyChanged(nameof(ScorePercentage));
            OnPropertyChanged(nameof(ScoreText));
            OnPropertyChanged(nameof(CorrectAnswersCount));
            OnPropertyChanged(nameof(TotalQuestionsCount));
            OnPropertyChanged(nameof(AllQuestionsAnswered));
        }
    }
}
