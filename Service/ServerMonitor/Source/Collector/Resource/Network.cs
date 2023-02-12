using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system networking metrics
	public class Network : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Network" );

		// Holds the exported Prometheus metrics
		public readonly Gauge SentBytes;
		public readonly Gauge ReceivedBytes;

		// Initialise the exported Prometheus metrics
		public Network( Config configuration ) {
			SentBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_network_sent_bytes", "Total bytes sent over the network, in bytes.", new GaugeConfiguration() {
				LabelNames = new[] { "interface" }
			} );
			ReceivedBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_network_received_bytes", "Total bytes received over the network, in bytes.", new GaugeConfiguration() {
				LabelNames = new[] { "interface" }
			} );

			SentBytes.Set( 0 );
			ReceivedBytes.Set( 0 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			throw new NotImplementedException();
		}

		// Updates the metrics for Linux...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing network interface statistics - https://stackoverflow.com/a/61893775
			using ( FileStream fileStream = new( "/proc/net/dev", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Skip the heading lines
					streamReader.ReadLine();
					streamReader.ReadLine();

					// Loop through the contents line by line...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 16 ) throw new Exception( "Network statistics file contains an invalid line (too few parts)" );

						// Parse the relevant parts
						string interfaceName = lineParts[ 0 ].TrimEnd( ':' );
						if ( long.TryParse( lineParts[ 1 ], out long receivedBytes ) != true ) throw new Exception( "Failed to parse received bytes as long" );
						if ( long.TryParse( lineParts[ 9 ], out long sentBytes ) != true ) throw new Exception( "Failed to parse sent bytes as long" );

						// Update the metrics
						ReceivedBytes.WithLabels( interfaceName ).Set( receivedBytes );
						SentBytes.WithLabels( interfaceName ).Set( sentBytes );

					} while ( !streamReader.EndOfStream );

				}
			}
		}

	}
}
