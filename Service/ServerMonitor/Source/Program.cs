using System;
using System.IO;
using System.Reflection;
using System.CommandLine; // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor {

	public static class Program {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Program" );

		public static int Main( string[] arguments ) {

			// Get the directory that the executable DLL/binary is in - https://stackoverflow.com/a/66023223
			Assembly? executable = Assembly.GetEntryAssembly() ?? throw new Exception( "Failed to get this executable" );
			string executableDirectory = Path.GetDirectoryName( executable.Location ) ?? throw new Exception( "Failed to get this executable's directory" );
			logger.LogDebug( $"Executable directory: '{ executableDirectory }'" );

			// Create the root command, no handler though as only sub-commands are allowed
			RootCommand rootCommand = new( "The backend API/service for the server monitoring mobile app." );

			// Option to let the user specify an extra configuration file in a non-standard location
			Option<string> extraConfigurationFilePathOption = new(
				name: "--config",
				description: "Path to an additional configuration file.",
				getDefaultValue: () => Path.Combine( executableDirectory, Configuration.FileName ) // Defaults to the same directory as the executable, makes development easier
			);
			rootCommand.AddOption( extraConfigurationFilePathOption );

			// Option not run forever
			Option<bool> singleRunOrNoListenOption = new(
				name: "--once",
				description: "Run collector once then exit, or never start the connection-point listening loop, for testing.",
				getDefaultValue: () => false
			);
			rootCommand.AddOption( singleRunOrNoListenOption );

			// Sub-command to start in "collector" mode
			Command collectorCommand = new( "collector", "Expose metrics to Prometheus from configured sources." );
			collectorCommand.SetHandler( ( string extraConfigurationFilePath, bool singleRunOrNoListen ) =>
				HandleSubCommand( Collector.Collector.HandleCommand, extraConfigurationFilePath, singleRunOrNoListen ),
				extraConfigurationFilePathOption, singleRunOrNoListenOption
			);
			rootCommand.AddCommand( collectorCommand );
	
			// Sub-command to start in "connection point" mode
			Command connectorCommand = new( "connector", "Serve metrics from Prometheus to the mobile app." );
			connectorCommand.SetHandler( ( string extraConfigurationFilePath, bool singleRunOrNoListen ) =>
				HandleSubCommand( new Connector.Connector().HandleCommand, extraConfigurationFilePath, singleRunOrNoListen ),
				extraConfigurationFilePathOption, singleRunOrNoListenOption
			);
			rootCommand.AddCommand( connectorCommand );

			return rootCommand.Invoke( arguments );
		}

		// Intermediary handler for all sub-commands, loads the configuration & then calls real sub-command handler
		private static void HandleSubCommand( Action<Config, bool> subCommandHandler, string extraConfigurationFilePath, bool singleRunOrNoListen ) {

			// Load the configuration
			Configuration.Load( extraConfigurationFilePath );
			logger.LogInformation( "Loaded the configuration" );

			// Call the sub-command handler
			subCommandHandler.Invoke( Configuration.Config!, singleRunOrNoListen );

		}

	}

}
