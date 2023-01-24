using System;
using System.Threading.Tasks;
using System.CommandLine;

namespace ServerMonitor {

	public static class Program {

		public static async Task<int> Main( string[] arguments ) {
			RootCommand rootCommand = new( "Server Monitor" );

			Command collectorCommand = new( "collector", "Collect metrics from configured sources." );
			collectorCommand.SetHandler( Collector.Collector.HandleCommand );
			rootCommand.AddCommand( collectorCommand );

			Command connectorCommand = new( "connector", "Send metrics from Prometheus to incoming app connections." );
			connectorCommand.SetHandler( Connector.Connector.HandleCommand );
			rootCommand.AddCommand( connectorCommand );

			return await rootCommand.InvokeAsync( arguments );
		}

	}

}
