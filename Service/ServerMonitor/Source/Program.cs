using System;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.CommandLine; // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor {

	public static class Program {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Program" );

		public static async Task<int> Main( string[] arguments ) {
			logger.LogInformation( "Well, hello there." );

			// https://stackoverflow.com/a/66023223
			Assembly? executable = Assembly.GetEntryAssembly() ?? throw new Exception( "Failed to get this executable" );
			string executableDirectory = Path.GetDirectoryName( executable.Location ) ?? throw new Exception( "Failed to get this executable's directory" );
			logger.LogDebug( $"Executable directory: '{ executableDirectory }'" );

			RootCommand rootCommand = new( "The backend API/service for the server monitoring mobile app." );

			// Option to let the user specify an extra configuration file in a non-standard location
			Option<string> extraConfigurationFilePathOption = new(
				name: "--config",
				description: "Path to an additional configuration file.",
				getDefaultValue: () => Path.Combine( executableDirectory, Configuration.FileName ) // Defaults to the same directory as the executable, makes development easier
			);
			rootCommand.AddOption( extraConfigurationFilePathOption );

			// TODO: Load configuration here instead of in each sub-command handler...

			// Sub-command to start in "collector" mode
			Command collectorCommand = new( "collector", "Expose metrics to Prometheus from configured sources." );
			collectorCommand.SetHandler( Collector.Collector.HandleCommand, extraConfigurationFilePathOption );
			rootCommand.AddCommand( collectorCommand );

			// Sub-command to start in "connection point" mode
			Command connectorCommand = new( "connector", "Serve metrics from Prometheus to the mobile app." );
			connectorCommand.SetHandler( Connector.Connector.HandleCommand, extraConfigurationFilePathOption );
			rootCommand.AddCommand( connectorCommand );

			// Sub-command to start then immediately exit just to test launching the executable
			Command testCommand = new( "test", "Test launching the executable." );
			testCommand.SetHandler( ( string extraConfigurationFilePath ) => {
				Configuration.Load( extraConfigurationFilePath );
				logger.LogInformation( "Loaded configuration." );

				logger.LogInformation( "Exiting..." );
				Environment.Exit( 0 );
			}, extraConfigurationFilePathOption );
			rootCommand.AddCommand( testCommand );

			return await rootCommand.InvokeAsync( arguments );
		}

	}

}
