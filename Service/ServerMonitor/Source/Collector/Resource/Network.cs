using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system networking metrics
	public class Network : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Network" );

		// Holds the metrics from the latest update
		public double TransmitBytes { get; private set; } = 0;
		public double ReceiveBytes { get; private set; } = 0;

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			throw new NotImplementedException();
		}

		// Updates the metrics for Linux...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			throw new NotImplementedException();
		}

	}
}
