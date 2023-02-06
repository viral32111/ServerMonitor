using System;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor.Collector {

	public static class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );
		
		public static void HandleCommand( string extraConfigurationFilePath ) {
			logger.LogInformation( "Collector mode!" );

			Config configuration = Configuration.Load( extraConfigurationFilePath );
			logger.LogInformation( "Loaded configuration. Test = {0}", configuration.Test );
		}

	}

}
