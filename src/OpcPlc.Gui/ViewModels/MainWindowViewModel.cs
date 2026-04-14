using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace OpcPlc.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    private string _serverStatus = "Stopped";
    public string ServerStatus
    {
        get => _serverStatus;
        set => this.RaiseAndSetIfChanged(ref _serverStatus, value);
    }

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
            (isBusy, status) => !isBusy && status != "Running");
        StartServerCommand = ReactiveCommand.CreateFromTask(StartServerAsync, canStart);

        var canStop = this.WhenAnyValue(x => x.IsBusy, x => x.ServerStatus,
            (isBusy, status) => !isBusy && status == "Running");
        StopServerCommand = ReactiveCommand.CreateFromTask(StopServerAsync, canStop);
    }

    private async Task StartServerAsync()
    {
        IsBusy = true;
        try
        {
            _opcPlcServer ??= new OpcPlc.OpcPlcServer();

            await Task.Run(async () =>
            {
                await _opcPlcServer.StartAsync(Array.Empty<string>()).ConfigureAwait(false);
            }).ConfigureAwait(false);

            ServerStatus = "Running";
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

            ServerStatus = "Stopped";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
