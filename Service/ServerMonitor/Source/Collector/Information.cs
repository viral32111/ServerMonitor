using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system uptime metrics
	public class Information : Base {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Information" );

		// Holds the exported Prometheus metrics
		public readonly Counter UptimeSeconds;

		// Initialise the exported Prometheus metrics
		public Information( Config configuration ) : base( configuration ) {
			UptimeSeconds = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_uptime_seconds", "System uptime, in seconds.", new CounterConfiguration {
				LabelNames = new string[] { "name", "os", "architecture", "version" }
			} );
			UptimeSeconds.IncTo( -1 );
			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics
		public override void Update() {

			// Get information about the system
			string name = Environment.MachineName;
			string operatingSystem = RuntimeInformation.OSDescription;
			string architecture = RuntimeInformation.OSArchitecture.ToString();
			string version = Environment.OSVersion.VersionString;

			// Get the uptime of the system
			TimeSpan uptime;
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) uptime = TimeSpan.FromMilliseconds( GetTickCount64() );
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) uptime = TimeSpan.FromSeconds( GetUptimeSeconds() );
			else throw new PlatformNotSupportedException( "Unsupported operating system" );

			// Update the exported Prometheus metrics
			UptimeSeconds.WithLabels( name, operatingSystem, architecture, version ).IncTo( uptime.TotalSeconds );
			logger.LogDebug( "Updated Prometheus metrics" );

		}

		// Reads the system uptime from the /proc/uptime file
		[ SupportedOSPlatform( "linux" ) ]
		private double GetUptimeSeconds() => File.ReadAllLines( "/proc/uptime" )
			.Where( line => string.IsNullOrWhiteSpace( line ) == false ) // Skip empty lines
			.Select( line => line.Split( " ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split the line into parts
			.First() // Get the first line
			.Select( linePart => double.Parse( linePart ) ) // Parse the line parts as doubles
			.First(); // Get the first part

		// C++ Windows API function to get the milliseconds elapsed since system startup - https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64, https://stackoverflow.com/a/16673001
		[ return: MarshalAs( UnmanagedType.U8 ) ]
		[ SupportedOSPlatform( "windows" ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private extern static UInt64 GetTickCount64();

	}

}
