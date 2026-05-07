using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpcPlc.Gui.Logging;
using OpcPlc.Gui.Services;
using OpcPlc.Gui.ViewModels.NodeEditor;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace OpcPlc.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxLogLines = 500;
    public enum ServerState
    {
        Stopped,
        Running,
        Error
    }

    private ServerState _serverStatus = ServerState.Stopped;
    public ServerState ServerStatus
    {
        get => _serverStatus;
        set
        {
            this.RaiseAndSetIfChanged(ref _serverStatus, value);
            this.RaisePropertyChanged(nameof(ServerStatusText));
        }
    }

    public string ServerStatusText => ServerStatus.ToString();

    private int _serverPort = 50000;
    public int ServerPort
    {
        get => _serverPort;
        set => this.RaiseAndSetIfChanged(ref _serverPort, value);
    }

    public ObservableCollection<string> LogLines { get; } = new ObservableCollection<string>();

    public NodeEditorViewModel NodeEditor { get; } = new NodeEditorViewModel();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public ReactiveCommand<Unit, Unit> StartServerCommand { get; }
    public ReactiveCommand<Unit, Unit> StopServerCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveNodesCommand { get; }

    private OpcPlc.OpcPlcServer? _opcPlcServer;
    private readonly NodesFileService _nodesFileService;

    public MainWindowViewModel()
    {
        var canStart = this.WhenAnyValue(x => x.IsBusy, x => x.ServerStatus,
            (isBusy, status) => !isBusy && status != ServerState.Running);
        StartServerCommand = ReactiveCommand.CreateFromTask(StartServerAsync, canStart);
        StartServerCommand.ThrownExceptions.Subscribe(ex => OnLogMessage($"[Error] Start server failed: {ex.Message}"));

        var canStop = this.WhenAnyValue(x => x.IsBusy, x => x.ServerStatus,
            (isBusy, status) => !isBusy && status == ServerState.Running);
        StopServerCommand = ReactiveCommand.CreateFromTask(StopServerAsync, canStop);
        StopServerCommand.ThrownExceptions.Subscribe(ex => OnLogMessage($"[Error] Stop server failed: {ex.Message}"));

        // Initialize node editor from nodesfile.json if available.
        _nodesFileService = new NodesFileService();
        var loadedRoot = _nodesFileService.Load();
        NodeEditor.Root = loadedRoot ?? new FolderItem { Name = "MyTelemetry" };

        SaveNodesCommand = ReactiveCommand.Create(() => _nodesFileService.Save(NodeEditor.Root));

        this.WhenAnyValue(x => x.ServerStatus)
            .Subscribe(status => NodeEditor.IsReadOnly = status == ServerState.Running);
    }

    private async Task StartServerAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsBusy = true);

        try
        {
            // Persist latest editor changes before launching server so runtime reads the newest nodesfile.
            SaveNodes();

            if (_opcPlcServer == null)
            {
                _opcPlcServer = new OpcPlc.OpcPlcServer();
                _opcPlcServer.LoggerFactoryConfigured += OnLoggerFactoryConfigured;
            }

            // StartAsync blocks until the server is stopped, so we must fire-and-forget.
            _ = Task.Run(async () =>
            {
                try
                {
                    await _opcPlcServer.StartAsync(["--autoaccept", "--ut", "--dca", "--ph", "0.0.0.0", "--pn", ServerPort.ToString(), "--nf", _nodesFileService.ResolvedPath]).ConfigureAwait(false);
                }
                catch
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ServerStatus = ServerState.Error;
                        IsBusy = false;
                    });
                    throw;
                }
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ServerStatus = ServerState.Running;
                IsBusy = false;
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ServerStatus = ServerState.Error;
                IsBusy = false;
            });
            throw;
        }
    }

    private void OnLoggerFactoryConfigured(ILoggerFactory loggerFactory)
    {
        loggerFactory.AddProvider(new GuiLoggerProvider(OnLogMessage));
    }

    private void OnLogMessage(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogLines.Add(message);
            while (LogLines.Count > MaxLogLines)
            {
                LogLines.RemoveAt(0);
            }
        });
    }

    private async Task StopServerAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsBusy = true);

        try
        {
            await Task.Run(() => _opcPlcServer?.Stop()).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ServerStatus = ServerState.Stopped;
                IsBusy = false;
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ServerStatus = ServerState.Error;
                IsBusy = false;
            });
            throw;
        }
    }

    public void SaveNodes()
    {
        _nodesFileService.Save(NodeEditor.Root);
    }
}
