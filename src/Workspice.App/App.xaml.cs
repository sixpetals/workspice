using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Workspice.Application.Contracts;
using Workspice.Application.Services;
using Workspice.Infrastructure;

namespace Workspice.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private TrayIconHost? _trayIconHost;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    var executablePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    services.AddWorkspiceInfrastructure(executablePath);
                    services.AddSingleton<IWorkspiceState, WorkspiceStateService>();
                    services.AddSingleton<ISwitchOrchestrator, SwitchOrchestrator>();
                    services.AddSingleton<IUserInteractionService, WpfDialogService>();
                    services.AddSingleton<TrayIconHost>();
                })
                .Build();

            await _host.StartAsync();
            _trayIconHost = _host.Services.GetRequiredService<TrayIconHost>();
            await _trayIconHost.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Workspice 起動失敗", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_trayIconHost is not null)
        {
            _trayIconHost.Dispose();
        }

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
