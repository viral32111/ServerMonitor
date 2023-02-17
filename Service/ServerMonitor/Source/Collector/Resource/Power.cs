using System;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Prometheus;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system power metrics
	public class Power : Base {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Power" );

		// Holds the metrics from the latest update
		public readonly Gauge CurrentWattage;
		public readonly Gauge MaximumWattage;

		// Initialise the exported Prometheus metrics
		public Power( Config configuration ) {
			CurrentWattage = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_power_current_wattage", "Current system power usage, in watts." );
			MaximumWattage = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_power_maximum_wattage", "Maximum system power usage, in watts." );

			CurrentWattage.Set( 0 );
			CurrentWattage.Set( 0 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			throw new NotImplementedException();
		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			throw new NotImplementedException();
		}

	}

}
