using System;
using System.Threading.Tasks;

// https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using System.CommandLine;

// https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
using Microsoft.Extensions.Configuration;

namespace ServerMonitor {

	public static class Program {

		public static async Task<int> Main( string[] arguments ) {
			// Load settings
			Settings settings = LoadSettings();
			Console.WriteLine( $"Test = { settings.Test }" );

			// Create commands for the different modes
			RootCommand rootCommand = new( "Server Monitor" );

			Command collectorCommand = new( "collector", "Collect metrics from configured sources." );
			collectorCommand.SetHandler( Collector.Collector.HandleCommand );
			rootCommand.AddCommand( collectorCommand );

			Command connectorCommand = new( "connector", "Send metrics from Prometheus to incoming app connections." );
			connectorCommand.SetHandler( Connector.Connector.HandleCommand );
			rootCommand.AddCommand( connectorCommand );

			return await rootCommand.InvokeAsync( arguments );
		}

		// Loads our settings from JSON file & environment variables
		private static Settings LoadSettings() {
			IConfiguration configuration = new ConfigurationBuilder()
				.AddJsonFile( "appsettings.json" )
				.AddEnvironmentVariables()
				.Build();

			Settings? settings = configuration.GetRequiredSection( "Settings" ).Get<Settings>();
			if ( settings == null ) throw new Exception( "Settings is null (could be malformed or invalid)." );

			return settings;
		}

	}

}

