using Microsoft.Extensions.Logging;
using OpcPlc.Gui.Logging;
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

        var canStop = this.WhenAnyValue(x => x.IsBusy, x => x.ServerStatus,
            (isBusy, status) => !isBusy && status == ServerState.Running);
        StopServerCommand = ReactiveCommand.CreateFromTask(StopServerAsync, canStop);
    }

    private async Task StartServerAsync()
    {
        IsBusy = true;
        try
        {
            if (_opcPlcServer == null)
            {
                _opcPlcServer = new OpcPlc.OpcPlcServer();
                _opcPlcServer.LoggerFactoryConfigured += OnLoggerFactoryConfigured;
            }

            await Task.Run(async () => await _opcPlcServer.StartAsync(Array.Empty<string>()).ConfigureAwait(false)).ConfigureAwait(false);

            ServerStatus = ServerState.Running;
        }
        catch
        {
            ServerStatus = ServerState.Error;
            throw;
        }
        finally
        {
            IsBusy = false;
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
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                _opcPlcServer?.Stop();
            }).ConfigureAwait(false);

            ServerStatus = ServerState.Stopped;
        }
        catch
        {
            ServerStatus = ServerState.Error;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
