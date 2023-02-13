using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system networking metrics
	public class Network : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Network" );

		// Holds the exported Prometheus metrics
		public readonly Counter SentBytes;
		public readonly Counter ReceivedBytes;

		// Initialise the exported Prometheus metrics
		public Network( Config configuration ) {
			SentBytes = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_resource_network_sent_bytes", "Total bytes sent over the network, in bytes.", new CounterConfiguration() {
				LabelNames = new[] { "interface" }
			} );
			ReceivedBytes = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_resource_network_received_bytes", "Total bytes received over the network, in bytes.", new CounterConfiguration() {
				LabelNames = new[] { "interface" }
			} );

			SentBytes.IncTo( 0 );
			ReceivedBytes.IncTo( 0 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Loop through each network interface's statistics - https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface.getallnetworkinterfaces
			foreach ( NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces() ) {
				IPInterfaceStatistics ipStatistics = networkInterface.GetIPStatistics();

				// Set the values for the exported Prometheus metrics
				ReceivedBytes.WithLabels( networkInterface.Name ).IncTo( ipStatistics.BytesReceived );
				SentBytes.WithLabels( networkInterface.Name ).IncTo( ipStatistics.BytesSent );
				logger.LogDebug( "Updated Prometheus metrics" );

			}

		}

		// Updates the exported Prometheus metrics (for Linux)
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

						// Set the values for the exported Prometheus metrics
						ReceivedBytes.WithLabels( interfaceName ).IncTo( receivedBytes );
						SentBytes.WithLabels( interfaceName ).IncTo( sentBytes );
						logger.LogDebug( "Updated Prometheus metrics" );

					} while ( !streamReader.EndOfStream );

				}
			}
		}

	}
}
