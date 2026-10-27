using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using learn_Assist.Controls;
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
    // Identifica la solicitud de scroll más reciente y evita intentos obsoletos.
    private int _scrollRequestVersion;
    private double _lastMessagesExtentHeight = -1;
    private int _stableMessagesLayoutPasses;
    private bool _scrollToBottomPending;
    private int _scrollLayoutPasses;

    /// <summary>
    /// Inicializa los controles generados por XAML y conecta el seguimiento
    /// del tamaño de la ventana para actualizar el diseño responsive.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        MessageTextBox.AddHandler(InputElement.KeyDownEvent, OnMessageTextBoxKeyDown,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        UpdateInternalLogo();
        ThemeService.ThemeChanged += OnThemeChanged;
        Closed += OnClosed;
        Opened += OnOpened;
        KeyDown += OnKeyDown;
        MarkdownView.RenderCompleted += OnMarkdownRendered;
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
        Opened -= OnOpened;
        KeyDown -= OnKeyDown;
        MessageTextBox.RemoveHandler(InputElement.KeyDownEvent, OnMessageTextBoxKeyDown);
        MarkdownView.RenderCompleted -= OnMarkdownRendered;
        if (MessagesScroll is not null)
            MessagesScroll.LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Tutorial.IsVisible)
            Dispatcher.UIThread.Post(() => TutorialOverlay?.Focus(), DispatcherPriority.Loaded);

        if (DataContext is MainViewModel { IsLearningSessionActive: true })
            OnScrollToBottom(true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainViewModel vm || !vm.Tutorial.IsVisible)
            return;

        e.Handled = true;
        vm.Tutorial.SkipCommand.Execute(null);
    }

    private void OnMarkdownRendered()
    {
        if (DataContext is MainViewModel vm
            && vm.IsLearningSessionActive
            && vm.Chat.Messages.Count > 0)
        {
            OnScrollToBottom(true);
        }
    }

    /// <summary>
    /// Handles the delete session button click.
    /// </summary>
    private void OnDeleteSessionClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SessionList is not null)
        {
            // Get the session from the parent ItemsControl
            if (sender is global::Avalonia.Controls.Control control &&
                control.Parent is global::Avalonia.Controls.ItemsControl itemsControl &&
                itemsControl.Items.Count > 0)
            {
                var session = itemsControl.Items[itemsControl.Items.Count - 1] as ChatSession;
                if (session is not null)
                {
                    vm.SessionList.DeleteSession(session);
                }
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Desuscribir el ViewModel anterior evita eventos duplicados y fugas.
        if (_previousVm is not null)
        {
            _previousVm.Chat.ScrollToBottomRequested -= OnScrollToBottom;
            _previousVm.Chat.PropertyChanged -= OnChatPropertyChanged;
            _previousVm.DocumentList.ImportDialogRequested -= OnImportDialog;
            _previousVm.ConfigureAiRequested -= OnConfigureAi;
            _previousVm.RestartRequested -= OnRestartRequested;
            _previousVm.UninstallRequested -= OnUninstallRequested;
            _previousVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            // El code-behind atiende eventos que requieren controles o ventanas:
            // scroll, diálogos, reinicio y cambios del layout.
            vm.Chat.ScrollToBottomRequested += OnScrollToBottom;
            vm.Chat.PropertyChanged += OnChatPropertyChanged;
            vm.DocumentList.ImportDialogRequested += OnImportDialog;
            vm.ConfigureAiRequested += OnConfigureAi;
            vm.RestartRequested += OnRestartRequested;
            vm.UninstallRequested += OnUninstallRequested;
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

    private void OnChatPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.IsLoading))
            OnScrollToBottom(true);
    }

    /// <summary>
    /// Recalcula la visibilidad y el ancho de las barras laterales cuando
    /// cambia el tamaño de la ventana.
    /// </summary>
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            UpdateResponsiveLayout(vm);

            if (vm.IsLearningSessionActive)
                OnScrollToBottom(true);
        }
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
    private void OnScrollToBottom(bool force)
    {
        if (!force)
            return;

        var requestVersion = ++_scrollRequestVersion;
        _lastMessagesExtentHeight = -1;
        _stableMessagesLayoutPasses = 0;
        _scrollToBottomPending = true;
        _scrollLayoutPasses = 0;
        if (MessagesScroll is not null)
        {
            MessagesScroll.LayoutUpdated -= OnLayoutUpdated;
            MessagesScroll.LayoutUpdated += OnLayoutUpdated;
        }
        Dispatcher.UIThread.Post(
            () => ScrollMessagesToBottom(requestVersion),
            DispatcherPriority.Loaded);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_scrollToBottomPending)
            ScrollMessagesToBottom(_scrollRequestVersion);
    }

    private void ScrollMessagesToBottom(int requestVersion)
    {
        if (requestVersion != _scrollRequestVersion || MessagesScroll is null)
            return;

        MessagesScroll.ScrollToEnd();
        _scrollLayoutPasses++;

        var extentHeight = MessagesScroll.Extent.Height;
        if (Math.Abs(extentHeight - _lastMessagesExtentHeight) < 0.5)
            _stableMessagesLayoutPasses++;
        else
            _stableMessagesLayoutPasses = 0;

        _lastMessagesExtentHeight = extentHeight;

        if (extentHeight <= 0
            || MessagesScroll.Viewport.Height <= 0
            || _stableMessagesLayoutPasses < 3
            || _scrollLayoutPasses < 8)
            return;

        _scrollToBottomPending = false;
        MessagesScroll.LayoutUpdated -= OnLayoutUpdated;
    }

    /// <summary>
    /// Envía el mensaje al pulsar Enter; Shift+Enter mantiene el salto de línea.
    /// </summary>
    private void OnMessageTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (e.KeyModifiers & KeyModifiers.Shift) != 0)
            return;

        e.Handled = true;
        if (DataContext is MainViewModel vm &&
            vm.Chat.SendMessageCommand.CanExecute(null))
        {
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

    private void OnUninstallRequested()
    {
        // UninstallService handles the removal; close the window so the
        // installer/worker can replace or delete the binary.
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
            await vm.DocumentList.AddDocumentAsync(result!);
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
            await vm.ApplyConfigAsync(dialog.Result);
    }
}
