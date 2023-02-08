using System;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor.Connector {

	public static class Connector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );
		
		public static void HandleCommand( Config configuration ) {
			logger.LogInformation( "Connection point mode!" );
		}

	}

}
