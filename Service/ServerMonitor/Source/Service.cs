using System;
using System.ServiceProcess;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace ServerMonitor {

	[ SupportedOSPlatform( "windows" ) ]
	public class Service : ServiceBase {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Service" );

		// Set the name
		[ SupportedOSPlatform( "windows" ) ]
		public Service() => ServiceName = "ServerMonitor";

		// Runs when the service is started...
		[ SupportedOSPlatform( "windows" ) ]
		protected override void OnStart( string[] arguments ) {
			logger.LogInformation( "Starting service" );

			// Is this blocking?
			int exitCode = Program.ProcessArguments( arguments, true );

			Environment.Exit( exitCode );
		}

		// Runs when the service is stopped...
		[ SupportedOSPlatform( "windows" ) ]
		protected override void OnStop() {
			logger.LogInformation( "Stopping service" );

			Program.Collector.Stop();
			Program.Connector.Stop();

			Environment.Exit( 0 );
		}

	}

}
