using System;
using System.Threading;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using ServerMonitor.Collector.Resource;

namespace ServerMonitor.Collector {

	public static class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		public static void HandleCommand( Config configuration ) {
			logger.LogInformation( "Collector mode!" );

			// This is all just for debugging
			Memory memory = new();
			Processor processor = new();
			Uptime uptime = new();
			while ( true ) {
				memory.Update();
				double totalMemory = Math.Round( memory.TotalBytes / 1024 / 1024, 2 );
				double freeMemory = Math.Round( memory.FreeBytes / 1024 / 1024, 2 );
				double usedMemory = Math.Round( memory.GetUsedBytes() / 1024 / 1024, 2 );
				double usedMemoryPercentage = Math.Round( memory.GetUsedPercentage() );
				double totalSwap = Math.Round( memory.SwapTotalBytes / 1024 / 1024, 2 );
				double freeSwap = Math.Round( memory.SwapFreeBytes / 1024 / 1024, 2 );
				double usedSwap = Math.Round( memory.GetSwapUsedBytes() / 1024 / 1024, 2 );
				double usedSwapPercentage = Math.Round( memory.GetSwapUsedPercentage() );
				logger.LogInformation( "Memory: {0} MiB / {1} MiB ({2} MiB free, {2}% usage)", usedMemory, totalMemory, freeMemory, usedMemoryPercentage );
				logger.LogInformation( "Swap/Page: {0} MiB / {1} MiB ({2} MiB free, {2}% usage)", usedSwap, totalSwap, freeSwap, usedSwapPercentage );

				processor.Update();
				double processorUsage = Math.Round( processor.Usage, 1 );
				logger.LogInformation( "Processor: {0}%", processorUsage );

				uptime.Update();
				logger.LogInformation( "Uptime: {0} seconds", uptime.UptimeSeconds );

				Thread.Sleep( 1000 );
				Console.Write( "\n" );
			}
		}

	}

}
