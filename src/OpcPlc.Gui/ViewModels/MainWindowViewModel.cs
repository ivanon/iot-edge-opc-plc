using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpcPlc.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
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
            _opcPlcServer ??= new OpcPlc.OpcPlcServer();

            await Task.Run(() => _opcPlcServer.StartAsync(Array.Empty<string>())).ConfigureAwait(false);

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
