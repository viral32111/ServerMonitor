using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system swap/paging metrics
	public class Swap : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Swap" );

		// Holds the metrics from the latest update
		public double TotalBytes { get; private set; } = 0;
		public double FreeBytes { get; private set; } = 0;

		// Simply returns the utilization
		public double GetUsedBytes() => TotalBytes - FreeBytes;
		public double GetUsedPercentage() => ( GetUsedBytes() / TotalBytes ) * 100;

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			throw new NotImplementedException();
		}

			// Updates the metrics for Windows...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			throw new NotImplementedException();
		}

	}
}
