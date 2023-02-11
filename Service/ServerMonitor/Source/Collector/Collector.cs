using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using Prometheus; // https://github.com/prometheus-net/prometheus-net
using ServerMonitor.Collector.Resource;

namespace ServerMonitor.Collector {

	public static class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		// Entry-point for the "collector" sub-command...
		public static void HandleCommand( Config configuration ) {
			logger.LogInformation( "Launched in collector mode" );

			// Start the Prometheus metrics server
			MetricServer server = new(
				hostname: configuration.PrometheusListenAddress,
				port: configuration.PrometheusListenPort,
				url: configuration.PrometheusListenPath,
				useHttps: false
			);
			server.Start();
			logger.LogInformation( "Prometheus Metrics server listening on http://{0}:{1}/{2}", configuration.PrometheusListenAddress, configuration.PrometheusListenPort, configuration.PrometheusListenPath );

			// Create instances of each resource collector
			Memory memory = new( configuration );
			Processor processor = new( configuration );
			Uptime uptime = new( configuration );
			Disk disk = new( configuration );

			// This is all just for debugging
			while ( true ) {
				Console.WriteLine( new string( '-', 100 ) );

				memory.Update();
				double totalMemory = Math.Round( memory.TotalBytes.Value / 1024 / 1024, 2 );
				double freeMemory = Math.Round( memory.FreeBytes.Value / 1024 / 1024, 2 );
				double usedMemory = Math.Round( ( memory.TotalBytes.Value - memory.FreeBytes.Value ) / 1024 / 1024, 2 );
				double usedMemoryPercentage = Math.Round( ( memory.TotalBytes.Value - memory.FreeBytes.Value ) / memory.TotalBytes.Value * 100, 0 );
				double totalSwap = Math.Round( memory.SwapTotalBytes.Value / 1024 / 1024, 2 );
				double freeSwap = Math.Round( memory.SwapFreeBytes.Value / 1024 / 1024, 2 );
				double usedSwap = Math.Round( ( memory.SwapTotalBytes.Value - memory.SwapFreeBytes.Value ) / 1024 / 1024, 2 );
				double usedSwapPercentage = Math.Round( ( memory.SwapTotalBytes.Value - memory.SwapFreeBytes.Value ) / memory.SwapTotalBytes.Value * 100, 0 );
				logger.LogInformation( "Memory: {0} MiB / {1} MiB ({2} MiB free, {3}% usage)", usedMemory, totalMemory, freeMemory, usedMemoryPercentage );
				logger.LogInformation( "Swap/Page: {0} MiB / {1} MiB ({2} MiB free, {3}% usage)", usedSwap, totalSwap, freeSwap, usedSwapPercentage );

				processor.Update();
				double processorUsage = Math.Round( processor.Usage.Value, 1 );
				logger.LogInformation( "Processor: {0}%", processorUsage );

				uptime.Update();
				logger.LogInformation( "Uptime: {0} seconds", uptime.UptimeSeconds.Value );

				disk.Update();
				foreach ( string[] labelValues in disk.TotalBytes.GetAllLabelValues() ) {
					string driveLabel = labelValues[ 0 ];
					string driveFilesystem = labelValues[ 1 ];
					string driveMountpoint = labelValues[ 2 ];

					double totalDisk = Math.Round( disk.TotalBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value / 1024 / 1024 / 1024, 2 );
					double freeDisk = Math.Round( disk.FreeBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value / 1024 / 1024 / 1024, 2 );
					double usedDisk = Math.Round( ( disk.TotalBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value - disk.FreeBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value ) / 1024 / 1024 / 1024, 2 );
					double usedDiskPercentage = Math.Round( ( disk.TotalBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value - disk.FreeBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value ) / disk.TotalBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value * 100, 0 );
					logger.LogInformation( "Disk ({0}, {1}, {2}): {3} GiB / {4} GiB ({5} GiB free, {6}% usage)", driveLabel, driveFilesystem, driveMountpoint, usedDisk, totalDisk, freeDisk, usedDiskPercentage );
				}

				Thread.Sleep( 5000 ); // 5s
			}
		}

	}

}
