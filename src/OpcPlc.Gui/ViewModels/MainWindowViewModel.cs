using Microsoft.Extensions.Logging;
using OpcPlc.Gui.Logging;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
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

    public ObservableCollection<string> LogLines { get; } = new ObservableCollection<string>();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public ReactiveCommand<Unit, Unit> StartServerCommand { get; }
    public ReactiveCommand<Unit, Unit> StopServerCommand { get; }

    private OpcPlc.OpcPlcServer? _opcPlcServer;

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
    }

    private async Task StartServerAsync()
    {
        RxApp.MainThreadScheduler.Schedule(() => IsBusy = true);

        try
        {
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
                    await _opcPlcServer.StartAsync(["--autoaccept"]).ConfigureAwait(false);
                }
                catch
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        ServerStatus = ServerState.Error;
                        IsBusy = false;
                    });
                    throw;
                }
            });

            // Immediately reflect that the server launch was initiated.
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                ServerStatus = ServerState.Running;
                IsBusy = false;
            });
        }
        catch
        {
            RxApp.MainThreadScheduler.Schedule(() =>
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
        RxApp.MainThreadScheduler.Schedule(System.Reactive.Unit.Default, (_, __) =>
        {
            LogLines.Add(message);
            while (LogLines.Count > MaxLogLines)
            {
                LogLines.RemoveAt(0);
            }
            return System.Reactive.Disposables.Disposable.Empty;
        });
    }

    private async Task StopServerAsync()
    {
        RxApp.MainThreadScheduler.Schedule(() => IsBusy = true);

        try
        {
            await Task.Run(() => _opcPlcServer?.Stop()).ConfigureAwait(false);

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                ServerStatus = ServerState.Stopped;
                IsBusy = false;
            });
        }
        catch
        {
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                ServerStatus = ServerState.Error;
                IsBusy = false;
            });
            throw;
        }
    }
}
