using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system power metrics
	public class Power : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Power" );

		// Holds the metrics from the latest update
		public double CurrentWattage { get; private set; } = 0;
		public double MaximumWattage { get; private set; } = 0;

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
