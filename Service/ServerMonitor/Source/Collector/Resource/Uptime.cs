using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system uptime metrics
	public class Uptime : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Uptime" );

		// Holds the exported Prometheus metrics
		public readonly Gauge UptimeSeconds;

		// Initialise the exported Prometheus metrics
		public Uptime( Config configuration ) {
			UptimeSeconds = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_uptime_seconds", "System uptime, in seconds." );
			UptimeSeconds.Set( 0 );
			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Get the uptime & set the value for the exported Prometheus metric
			TimeSpan uptime = TimeSpan.FromMilliseconds( GetTickCount64() );
			UptimeSeconds.Set( uptime.TotalSeconds );
			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Updates the metrics for Linux...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			using ( FileStream fileStream = new( "/proc/uptime", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Get the first line of the file
					string? fileLine = streamReader.ReadLine();
					if ( string.IsNullOrWhiteSpace( fileLine ) ) throw new Exception( "First line of file is empty or whitespace" );

					// Split the line into its components
					string[] lineParts = fileLine.Split( " ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
					if ( lineParts.Length != 2 ) throw new Exception( "File line has incorrect number of parts" );

					// Get the uptime & set the value for the exported Prometheus metric
					if ( double.TryParse( lineParts[ 0 ], out double uptime ) != true ) throw new Exception( "Failed to parse uptime as double" );
					UptimeSeconds.Set( uptime );
					logger.LogDebug( "Updated Prometheus metrics" );

				}
			}
		}

		// C++ Windows API function to get the milliseconds elapsed since system startup - https://stackoverflow.com/a/16673001
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private extern static UInt64 GetTickCount64();

	}

}
