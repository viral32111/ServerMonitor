using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Prometheus;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system fan metrics
	public class Fan : Base {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Fan" );

		// Holds the metrics from the latest update
		public readonly Gauge SpeedRPM;

		// Initialise the exported Prometheus metrics
		public Fan( Config configuration ) {
			SpeedRPM = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_fan_speed_rpm", "Current fan speed, in revolutions per minute." );
			SpeedRPM.Set( 0 );
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
