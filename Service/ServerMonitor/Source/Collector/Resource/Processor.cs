using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Management;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system processor metrics
	public class Processor : Base {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Processor" );

		// Holds the exported Prometheus metrics
		public readonly Gauge Usage;
		public readonly Gauge Temperature; // TODO
		public readonly Gauge Frequency;

		// Initialise the exported Prometheus metrics
		public Processor( Config configuration ) {
			Usage = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_usage", "Processor usage, as percentage." );
			Temperature = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_temperature", "Processor temperature, in degrees Celsius." );
			Frequency = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_frequency", "Processor frequency, in hertz." );
			Usage.Set( 0 );
			Temperature.Set( 0 );
			Frequency.Set( 0 );
			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Get processor usage from the Performance Monitor interface - https://stackoverflow.com/a/278088
			// NOTE: Two samples required with 1 second wait to get an accurate reading
			PerformanceCounter cpuCounter = new( "Processor", "% Processor Time", "_Total" );
			cpuCounter.NextValue();
			Thread.Sleep( 1000 );
			float processorUsage = cpuCounter.NextValue();

			// Get processor frequency from the WMI interface - https://stackoverflow.com/a/6923927
			uint currentFrequency = 0;
			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( "SELECT * FROM Win32_Processor" ).Get() ) {
				currentFrequency = Convert.ToUInt32( managementObject[ "CurrentClockSpeed" ].ToString() );
			}

			// TODO: Temperature

			// Set the values for the exported Prometheus metrics
			Usage.Set( processorUsage );
			Frequency.Set( currentFrequency );
			Temperature.Set( 0 );
			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// https://stackoverflow.com/a/3114251
		/*[ SupportedOSPlatform( "windows" ) ]
		private void GetTemperature() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature" ).Get() ) {
				double currentTemperature = Convert.ToDouble( managementObject[ "CurrentTemperature" ].ToString() );
				double temperatureSelsius = ( currentTemperature - 2732 ) / 10.0;

				logger.LogDebug( "Temperature of '{0}': {1} C ({2})", managementObject[ "InstanceName" ].ToString(), temperatureSelsius, currentTemperature );
			}
		}*/

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Get processor usage
			double processorUsage = GetProcessorUsage();
			Usage.Set( processorUsage );

			// TODO: Temperature
			Temperature.Set( 0 );

			// Get processor frequency
			double processorFrequency = GetProcessorFrequency();
			Frequency.Set( processorFrequency );

			logger.LogDebug( "Updated Prometheus metrics" );

		}

		// Gets the processor times (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private int[] GetProcessorTimes() => File.ReadAllLines( "/proc/stat" ) // Read the psuedo-file for processor statistics - https://linux.die.net/man/5/proc
			.Where( line => line.StartsWith( "cpu " ) ) // Get just the relevant line
			.Select( line => line.Substring( 4 ) ) // Remove the prefix
			.Select( line => line.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split the line up into values
			.Select( values => values.Select( part => int.Parse( part ) ).ToArray() ) // Convert the values to integers
			.First(); // Get the first array

		// Gets the processor usage percentage (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private double GetProcessorUsage() {

			// Get the processor times
			int[] firstTimes = GetProcessorTimes();
			int firstIdleTime = firstTimes[ 3 ];
			int firstTotalTime = firstTimes.Sum();

			// Wait a second to get a more accurate reading
			Thread.Sleep( 1000 );

			// Get the processor times again
			int[] secondTimes = GetProcessorTimes();
			int secondIdleTime = secondTimes[ 3 ];
			int secondTotalTime = secondTimes.Sum();

			// Calculate the processor usage from the differences in times - https://askubuntu.com/a/450136
			int totalTimeDifference = secondTotalTime - firstTotalTime;
			int idleTimeDifference = secondIdleTime - firstIdleTime;
			return ( 1000 * ( totalTimeDifference - idleTimeDifference ) / totalTimeDifference + 5 ) / 10.0;
		}

		// Gets the average processor frequency across all cores in MHz (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private double GetProcessorFrequency() => File.ReadAllLines( "/proc/cpuinfo" ) // Read the psuedo-file for processor information - https://linux.die.net/man/5/proc
			.Where( line => !string.IsNullOrWhiteSpace( line ) ) // Skip empty lines
			.Select( line => line.Split( ":", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split the line up into key & value pairs
			.Where( parts => parts[ 0 ] == "cpu MHz" ) // Skip anything that isn't relevant
			.Average( parts => double.Parse( parts[ 1 ] ) ); // Calculate the average frequency

	}

}
