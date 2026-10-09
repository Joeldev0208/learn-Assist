using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class MainWindow : Window
{
    // ViewModel anterior para retirar suscripciones antes de enlazar uno nuevo.
    private MainViewModel? _previousVm;
    // Grid cuyas columnas se ajustan para el diseño responsive.
    private Grid? _workspaceGrid;
    // Breakpoint aplicado anteriormente; evita reasignaciones innecesarias.
    private int? _lastBreakpoint;

    /// <summary>
    /// Inicializa los controles generados por XAML y conecta el seguimiento
    /// del tamaño de la ventana para actualizar el diseño responsive.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        UpdateInternalLogo();
        ThemeService.ThemeChanged += OnThemeChanged;
        Closed += OnClosed;
        _workspaceGrid = this.FindControl<Grid>("WorkspaceGrid");
        SizeChanged += OnWindowSizeChanged;
    }

    private void OnThemeChanged()
    {
        Dispatcher.UIThread.Post(UpdateInternalLogo);
    }

    private void UpdateInternalLogo()
    {
        var assetName = ThemeService.IsDark
            ? "learn-assist-logo-dark.png"
            : "learn-assist-logo-light.png";
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var uri = new Uri($"avares://{assemblyName}/Assets/{assetName}");

        using var stream = AssetLoader.Open(uri);
        LogoInterno.Source = new Bitmap(stream);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        Closed -= OnClosed;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Desuscribir el ViewModel anterior evita eventos duplicados y fugas.
        if (_previousVm is not null)
        {
            _previousVm.Chat.ScrollToBottomRequested -= OnScrollToBottom;
            _previousVm.DocumentList.ImportDialogRequested -= OnImportDialog;
            _previousVm.ConfigureAiRequested -= OnConfigureAi;
            _previousVm.RestartRequested -= OnRestartRequested;
            _previousVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            // El code-behind atiende eventos que requieren controles o ventanas:
            // scroll, diálogos, reinicio y cambios del layout.
            vm.Chat.ScrollToBottomRequested += OnScrollToBottom;
            vm.DocumentList.ImportDialogRequested += OnImportDialog;
            vm.ConfigureAiRequested += OnConfigureAi;
            vm.RestartRequested += OnRestartRequested;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            _previousVm = vm;
            UpdateResponsiveLayout(vm);
        }
        else
        {
            _previousVm = null;
            _lastBreakpoint = null;
        }
    }

    /// <summary>
    /// Recalcula la visibilidad y el ancho de las barras laterales cuando
    /// cambia el tamaño de la ventana.
    /// </summary>
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            UpdateResponsiveLayout(vm);
    }

    /// <summary>
    /// Sincroniza las columnas del Grid cuando el ViewModel cambia la
    /// visibilidad del historial o de los recursos.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSessionsVisible) or nameof(MainViewModel.IsResourcesVisible)
            && sender is MainViewModel vm)
            ApplyGridColumns(vm);
    }

    /// <summary>
    /// Selecciona el breakpoint actual y actualiza las propiedades de layout
    /// que controlan qué paneles laterales se muestran.
    /// </summary>
    private void UpdateResponsiveLayout(MainViewModel vm)
    {
        if (_workspaceGrid is null || _workspaceGrid.ColumnDefinitions.Count < 5)
            return;

        // Compacto: solo chat; mediano: chat + sesiones; amplio: tres paneles.
        var isCompact = Bounds.Width < 900;
        var isMedium = Bounds.Width < 1200;
        var breakpoint = isCompact ? 0 : isMedium ? 1 : 2;
        vm.IsCompactLayout = isCompact;

        if (_lastBreakpoint != breakpoint)
        {
            if (isCompact)
            {
                vm.IsSessionsVisible = false;
                vm.IsResourcesVisible = false;
            }
            else if (isMedium)
            {
                vm.IsSessionsVisible = true;
                vm.IsResourcesVisible = false;
            }
            else
            {
                vm.IsSessionsVisible = true;
                vm.IsResourcesVisible = true;
            }

            _lastBreakpoint = breakpoint;
        }

        ApplyGridColumns(vm);
    }

    /// <summary>
    /// Aplica físicamente al Grid los anchos correspondientes al estado
    /// visible de cada barra lateral.
    /// </summary>
    private void ApplyGridColumns(MainViewModel vm)
    {
        if (_workspaceGrid is null || _workspaceGrid.ColumnDefinitions.Count < 5)
            return;

        _workspaceGrid.ColumnDefinitions[0].Width =
            vm.IsSessionsVisible ? new GridLength(260) : new GridLength(0);
        _workspaceGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        _workspaceGrid.ColumnDefinitions[4].Width =
            vm.IsResourcesVisible ? new GridLength(220) : new GridLength(0);
        _workspaceGrid.ColumnDefinitions[1].Width =
            vm.IsSessionsVisible ? new GridLength(1) : new GridLength(0);
        _workspaceGrid.ColumnDefinitions[3].Width =
            vm.IsResourcesVisible ? new GridLength(1) : new GridLength(0);
    }

    /// <summary>
    /// Desplaza la lista de mensajes hasta el último mensaje en el hilo de UI.
    /// </summary>
    private void OnScrollToBottom()
    {
        // El mensaje debe estar medido antes de calcular el final del scroll.
        Dispatcher.UIThread.Post(
            () => MessagesScroll?.ScrollToEnd(),
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Envía el mensaje al pulsar Enter; Shift+Enter mantiene el salto de línea.
    /// </summary>
    private void OnMessageTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (e.KeyModifiers & KeyModifiers.Shift) != 0)
            return;

        if (DataContext is MainViewModel vm &&
            vm.Chat.SendMessageCommand.CanExecute(null))
        {
            e.Handled = true;
            vm.Chat.SendMessageCommand.Execute(null);
        }
    }

    /// <summary>
    /// Inicia de forma asíncrona el diálogo para importar un documento.
    /// </summary>
    private void OnImportDialog()
    {
        _ = ShowImportDialogAsync();
    }

    /// <summary>
    /// Abre la configuración del proveedor de IA en el hilo de UI.
    /// </summary>
    private void OnConfigureAi()
    {
        Dispatcher.UIThread.Post(() => _ = ShowApiConfigDialogAsync());
    }

    /// <summary>
    /// Cierra la ventana para que el actualizador pueda reemplazar el binario.
    /// </summary>
    private void OnRestartRequested()
    {
        // The apply-update.cmd script waits for this window to close before
        // swapping the binary, then relaunches the new version.
        Close();
    }

    /// <summary>
    /// Crea y muestra el diálogo de importación y agrega el documento resultante.
    /// </summary>
    private async Task ShowImportDialogAsync()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var importVm = new ImportDocumentViewModel();
        var dialog = new ImportDocumentView
        {
            DataContext = importVm,
        };

        var result = await dialog.ShowDialog<UserDocument?>(this);
        if (result is not null)
            vm.DocumentList.AddDocument(result!);
    }

    /// <summary>
    /// Carga la configuración existente, muestra el diálogo de IA y aplica
    /// la configuración confirmada al ViewModel principal.
    /// </summary>
    private async Task ShowApiConfigDialogAsync()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var existing = ConfigEncryption.LoadConfig();
        var configVm = existing is not null ? new ApiConfigViewModel(existing) : new ApiConfigViewModel();
        var dialog = new ApiConfigView
        {
            DataContext = configVm,
        };

        await dialog.ShowDialog<ApiConfig?>(this);

        if (dialog.Result is not null)
            vm.ApplyConfig(dialog.Result);
    }
}
