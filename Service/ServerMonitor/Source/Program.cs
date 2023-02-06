using System;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.CommandLine; // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial

namespace ServerMonitor {

	public static class Program {

		public static async Task<int> Main( string[] arguments ) {
			Assembly? executable = Assembly.GetEntryAssembly() ?? throw new Exception( "Failed to get this executable" );
			string executableDirectory = Path.GetDirectoryName( executable.Location ) ?? throw new Exception( "Failed to get this executable's directory" );

			RootCommand rootCommand = new( "Server Monitor" );

			// Option to let the user specify an extra configuration file in a non-standard location
			Option<string> extraConfigurationFilePathOption = new(
				name: "--config",
				description: "Path to an additional configuration file.",
				getDefaultValue: () => Path.Combine( executableDirectory, Configuration.FileName ) // Defaults to the same directory as the executable, makes development easier
			);
			rootCommand.AddOption( extraConfigurationFilePathOption );

			// Sub-command to start in "collector" mode
			Command collectorCommand = new( "collector", "Expose metrics to Prometheus from configured sources." );
			collectorCommand.SetHandler( Collector.Collector.HandleCommand, extraConfigurationFilePathOption );
			rootCommand.AddCommand( collectorCommand );

			// Sub-command to start in "connection point" mode
			Command connectorCommand = new( "connector", "Serve metrics from Prometheus to the mobile app." );
			connectorCommand.SetHandler( Connector.Connector.HandleCommand, extraConfigurationFilePathOption );
			rootCommand.AddCommand( connectorCommand );

			return await rootCommand.InvokeAsync( arguments );
		}

	}

}
