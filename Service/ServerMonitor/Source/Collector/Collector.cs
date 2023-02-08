using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

using ServerMonitor.Collector.Resource;

namespace ServerMonitor.Collector {

	public static class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		public static void HandleCommand( string extraConfigurationFilePath ) {
			logger.LogInformation( "Collector mode!" );

			Config configuration = Configuration.Load( extraConfigurationFilePath );
			logger.LogInformation( "Loaded configuration. Test = {0}", configuration.Test );

			Memory memory = new();
			while ( true ) {
				memory.Update();

				double totalMemory = Math.Round( memory.TotalBytes / 1024 / 1024, 2 );
				double freeMemory = Math.Round( memory.FreeBytes / 1024 / 1024, 2 );
				double usedMemory = Math.Round( memory.GetUsedBytes() / 1024 / 1024, 2 );
				double usedMemoryPercentage = Math.Round( memory.GetUsedPercentage() );
				logger.LogInformation( "Memory: {0} MiB / {1} MiB ({2} MiB free, {2}% usage)", usedMemory, totalMemory, freeMemory, usedMemoryPercentage );

				Thread.Sleep( 1000 );
			}
		}

	}

}
