using System.Windows;
using InfiniteCanvas.App.Logging;
using Serilog;

namespace InfiniteCanvas.App;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		Log.Logger = SerilogHost.Logger;
		Log.Information("Application starting");
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Log.Information("Application exiting");
		SerilogHost.Shutdown();
		base.OnExit(e);
	}
}
