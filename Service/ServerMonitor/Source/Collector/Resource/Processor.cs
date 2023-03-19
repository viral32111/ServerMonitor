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
		public readonly Gauge Temperature;
		public readonly Gauge Frequency;

		// Initialise the exported Prometheus metrics
		public Processor( Config configuration ) : base( configuration ) {
			Usage = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_usage", "Processor usage, as percentage." );
			Temperature = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_temperature", "Processor temperature, in degrees Celsius." );
			Frequency = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_frequency", "Processor frequency, in hertz." );
			Usage.Set( -1 );
			Temperature.Set( -1 );
			Frequency.Set( -1 );
			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

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

			// Try to get system temperature
			double currentTemperature = -1;
			try {
				currentTemperature = GetSystemTemperature( "ACPI\\ThermalZone\\TZ00_0" ) ?? GetSystemTemperature( "ACPI\\ThermalZone\\TZ10_0" ) ?? -1;
			} catch ( ManagementException exception ) {
				logger.LogWarning( "Getting the system temperature is not supported on this system! ({0})", exception.ErrorCode );
			}

			// Set the values for the exported Prometheus metrics
			Usage.Set( processorUsage );
			Temperature.Set( currentTemperature );
			Frequency.Set( currentFrequency );
			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Gets the temperature for a given system thermal zone - https://stackoverflow.com/a/3114251
		// NOTE: This is very manufacturer dependent, and may not work on all systems
		[ SupportedOSPlatform( "windows" ) ]
		private double? GetSystemTemperature( string thermalZone ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// Get each thermal zone temperature from the WMI interface
			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature" ).Get() ) {
				double currentTemperature = Convert.ToDouble( managementObject[ "CurrentTemperature" ].ToString() );
				double temperatureCelsius = ( currentTemperature - 2732 ) / 10.0;
				logger.LogDebug( "Temperature of '{0}': {1} C ({2})", managementObject[ "InstanceName" ].ToString(), temperatureCelsius, currentTemperature );

				if ( managementObject[ "InstanceName" ].ToString() == thermalZone ) return temperatureCelsius;
			}

			// Null if the thermal zone wasn't found
			return null;
		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			// Get processor usage & frequency
			Usage.Set( GetProcessorUsage() );
			Frequency.Set( GetProcessorFrequency() );

			// Get processor temperature from package sensor, but fallback to motherboard sensor if it doesn't exist
			Temperature.Set(
				GetProcessorTemperature( "x86_pkg_temp" ) ??
				GetProcessorTemperature( "acpitz" ) ??
				-1
			);

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

		// Gets the processor usage, as a percentage (for Linux)
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

		// Gets the average processor frequency across all cores, in MHz (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private double GetProcessorFrequency() => File.ReadAllLines( "/proc/cpuinfo" ) // Read the psuedo-file for processor information - https://linux.die.net/man/5/proc
			.Where( line => !string.IsNullOrWhiteSpace( line ) ) // Skip empty lines
			.Select( line => line.Split( ":", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split the line up into key & value pairs
			.Where( parts => parts[ 0 ] == "cpu MHz" ) // Skip anything that isn't relevant
			.Average( parts => double.Parse( parts[ 1 ] ) ); // Calculate the average frequency

		// Gets the processor temperature in degrees celsius (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private double? GetProcessorTemperature( string sensorType ) => Directory.GetDirectories( "/sys/class/thermal/", "thermal_zone*" ) // Get all thermal zones
			.Where( directoryPath => File.Exists( Path.Combine( directoryPath, "type" ) ) ) // The zone must have a type
			.Where( directoryPath => File.ReadAllLines( Path.Combine( directoryPath, "type" ) )[ 0 ] == sensorType ) // acpitz is the motherboard sensor, x86_pkg_temp is the CPU sensor
			.Where( directoryPath => File.Exists( Path.Combine( directoryPath, "temp" ) ) ) // The zone must have a temperature
			.Select( directoryPath => File.ReadAllLines( Path.Combine( directoryPath, "temp" ) )[ 0 ] ) // Get the temperature value
			.Select( temperatureValue => double.Parse( temperatureValue ) / 1000.0 ) // Convert the temperature to degrees celsius
			.Select<double, double?>( temperature => temperature > 0 ? temperature : null ) // If the temperature is 0 or below, it's likely invalid
			.FirstOrDefault(); // Return the value, or null if the sensor wasn't found

	}

}
