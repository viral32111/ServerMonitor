using System;
using System.ServiceProcess;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace ServerMonitor {

	[ SupportedOSPlatform( "windows" ) ]
	public class Service : ServiceBase {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Service" );

		// Properties from arguments
		public readonly Config Configuration;
		public readonly bool RunOnce;
		public readonly bool NoListen;
		public readonly Mode Mode;

		// Set the properties
		[ SupportedOSPlatform( "windows" ) ]
		public Service( Config configuration, bool runOnce, bool noListen, Mode mode ) {
			string modeName = mode switch {
				Mode.Collector => "Collector",
				Mode.Connector => "Connector",
				_ => throw new Exception( "Invalid mode" )
			};

			ServiceName = $"Server-Monitor-{ modeName }";

			this.Configuration = configuration;
			this.RunOnce = runOnce;
			this.NoListen = noListen;
			this.Mode = mode;
		}

		// Runs when the service is started...
		[ SupportedOSPlatform( "windows" ) ]
		protected override void OnStart( string[] arguments ) {
			logger.LogInformation( "Starting service" );

			if ( Mode == Mode.Collector ) Program.Collector.HandleCommand( Configuration, RunOnce );
			else if ( Mode == Mode.Connector ) Program.Connector.HandleCommand( Configuration, RunOnce, NoListen );
			else throw new Exception( "Invalid mode" );
		}

		// Runs when the service is stopped...
		[ SupportedOSPlatform( "windows" ) ]
		protected override void OnStop() {
			logger.LogInformation( "Stopping service" );

			Program.Collector.Stop();
			Program.Connector.Stop();
		}

	}

}
