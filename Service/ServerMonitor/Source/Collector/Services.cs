using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.ServiceProcess;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector {

	// Encapsulates collecting system service metrics
	public class Services : Base {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Services" );

		// Holds the exported Prometheus metrics
		public readonly Gauge StatusCode;
		public readonly Gauge ExitCode;
		public readonly Counter UptimeSeconds;

		// Initialise the exported Prometheus metrics
		public Services( Config configuration ) : base( configuration ) {
			StatusCode = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_service_status_code", "Service status code", new GaugeConfiguration {
				LabelNames = new[] { "service", "name", "description" }
			} );
			ExitCode = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_service_exit_code", "Service exit code", new GaugeConfiguration {
				LabelNames = new[] { "service", "name", "description" }
			} );
			UptimeSeconds = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_service_uptime_seconds", "Service uptime, in seconds", new CounterConfiguration {
				LabelNames = new[] { "service", "name", "description" }
			} );

			StatusCode.Set( -1 );
			UptimeSeconds.IncTo( -1 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// Loop through all non-driver services - https://learn.microsoft.com/en-us/dotnet/api/system.serviceprocess.servicecontroller?view=dotnet-plat-ext-7.0
			foreach ( ServiceController service in ServiceController.GetServices() ) {

				// Get other information about this service, if it has one - https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-service, https://stackoverflow.com/a/989866, https://stackoverflow.com/a/1574184
				ManagementObject serviceManagementObject = new( $"Win32_Service.Name='{ service.ServiceName }'" );
				string description = serviceManagementObject[ "Description" ]?.ToString() ?? "";
				if ( int.TryParse( serviceManagementObject[ "ProcessId" ]?.ToString(), out int processId ) == false ) {
					logger.LogWarning( "Service {0} has no process ID", service.ServiceName );
					continue;
				}
				if ( int.TryParse( serviceManagementObject[ "ExitCode" ]?.ToString(), out int exitCode ) == false ) {
					logger.LogWarning( "Service {0} has no exit code", service.ServiceName );
					continue;
				}

				// Get the uptime of the process for this service
				Process process = Process.GetProcessById( processId );
				if ( process == null ) throw new Exception( $"No process with process ID '{ processId }' for service '{ service.ServiceName }'" );
				double uptimeSeconds = GetProcessUptime( process );

				// Update the exported Prometheus metrics
				StatusCode.WithLabels( service.ServiceName, service.DisplayName, description ).Set( ( int ) service.Status );
				ExitCode.WithLabels( service.ServiceName, service.DisplayName, description ).Set( exitCode );
				UptimeSeconds.WithLabels( service.ServiceName, service.DisplayName, description ).IncTo( uptimeSeconds );
			}

			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Gets the uptime of a process, in seconds (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		private double GetProcessUptime( Process process ) {
			// First try using the start time property...
			try {
				return ( DateTime.Now - process.StartTime ).TotalSeconds;

			// Sometimes we get access denied, so fallback to searching the WMI - https://stackoverflow.com/a/31792
			} catch ( Win32Exception ) {
				foreach ( ManagementObject processManagementObject in new ManagementObjectSearcher( "root/CIMV2", "SELECT * FROM Win32_Process WHERE ProcessId = " + process.Id ).Get() ) {
					string creationDateText = processManagementObject[ "CreationDate" ].ToString() ?? throw new Exception( $"Process '{ process.ProcessName }' has no creation date" );
					return ( DateTime.Now - ManagementDateTimeConverter.ToDateTime( creationDateText ) ).TotalSeconds;
				}
			}

			throw new Exception( $"No way to get uptime for process '{ process.ProcessName }'" );
		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			// Update the metrics for both system & user services
			UpdateServicesMetrics( "system" );
			UpdateServicesMetrics( "user" );
			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Updates the metrics for all systemd services in a group (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private void UpdateServicesMetrics( string systemOrUser ) {

			// Read the system uptime from the uptime file, for calculating process uptime
			long systemUptimeSeconds = File.ReadAllLines( "/proc/uptime" )
				.First() // Only the first line
				.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) // Split into parts
				.Select( linePart => ( long ) Math.Round( double.Parse( linePart ), 0 ) ) // Convert part to rounded number
				.First(); // Get the first part (the system uptime)

			// Loop through all services...
			foreach ( string serviceName in GetServiceNames( systemOrUser ) ) {

				// Get information & live data about this service
				Dictionary<string, Dictionary<string, string>> serviceInformation = ParseServiceFile( systemOrUser, serviceName );
				Dictionary<string, string> serviceData = GetServiceData( serviceName );

				// Try to get the description of this service from the information
				string serviceDescription = "";
				if ( serviceInformation.TryGetValue( "Unit", out Dictionary<string, string>? unitSection ) == true && unitSection != null ) {
					if ( unitSection.TryGetValue( "Description", out string? description ) == true && string.IsNullOrWhiteSpace( description ) == false ) {
						serviceDescription = description;
					}
				}

				// Try to get the process ID of this service from the information
				int processIdentifier = 0;
				if ( serviceInformation.TryGetValue( "Service", out Dictionary<string, string>? serviceSection ) == true && serviceSection != null ) {
					if ( serviceSection.TryGetValue( "PIDFile", out string? processIdentifierFilePath ) == true && string.IsNullOrWhiteSpace( processIdentifierFilePath ) == false ) {
						if ( File.Exists( processIdentifierFilePath ) == true ) {
							processIdentifier = int.Parse( File.ReadAllLines( processIdentifierFilePath )[ 0 ] );
						}
					}
				}

				// Fallback to using the service data if we don't have a process identifier yet
				int mainProcessIdentifier = int.Parse( serviceData[ "MainPID" ] );
				int executingProcessIdentifier = int.Parse( serviceData[ "ExecMainPID" ] );
				if ( mainProcessIdentifier != 0 && processIdentifier == 0 ) processIdentifier = mainProcessIdentifier;
				if ( executingProcessIdentifier != 0 && processIdentifier == 0 ) processIdentifier = executingProcessIdentifier;

				// Parse the status text from the service data, if we have a valid process identifier
				int serviceStatus = 0; // Default to inactive
				if ( processIdentifier != 0 ) serviceStatus = serviceData[ "ActiveState" ] switch {
					"inactive" => 0,
					"active" => 1,
					"reloading" => 2,
					"failed" => 3,
					"exited" => 4,
					_ => throw new Exception( $"Unrecognised status '{ serviceData[ "ActiveState" ] }' for service '{ serviceName }'" )
				};

				// Parse the exit code from the service data
				int serviceExitCode = int.Parse( serviceData[ "ExecMainStatus" ] );

				// Calculate how long the process has been running for, if we have a valid process identifier - https://stackoverflow.com/a/16736599
				long processUptimeSeconds = 0;
				if ( processIdentifier != 0 ) {
					
					// If the process statistics file exists...
					string statisticsFilePath = Path.Combine( "/proc", processIdentifier.ToString(), "stat" );
					if ( File.Exists( statisticsFilePath ) == true ) {

						// Read the start time from the process statistics file
						long processStartTimeTicks = long.Parse( File.ReadAllLines( statisticsFilePath )
							.First() // Only the first line
							.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) // Split into parts
							.Skip( 21 ).First() ); // Only the 22nd part

						// Get the system clock ticks per second
						long systemClockTicks = Mono.Unix.Native.Syscall.sysconf( Mono.Unix.Native.SysconfName._SC_CLK_TCK );

						// Calculate the process uptime in seconds
						processUptimeSeconds = systemUptimeSeconds - ( processStartTimeTicks / systemClockTicks );
					
					}

				}

				// Update the exported Prometheus metrics
				// NOTE: Linux has no display names, so we use the service name for both
				StatusCode.WithLabels( serviceName, serviceName, serviceDescription ).Set( serviceStatus );
				ExitCode.WithLabels( serviceName, serviceName, serviceDescription ).Set( serviceExitCode );
				UptimeSeconds.WithLabels( serviceName, serviceName, serviceDescription ).IncTo( processUptimeSeconds );

			}

		}

		// Gets a list of systemd service names (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private static string[] GetServiceNames( string systemOrUser ) => Directory.GetFiles( Path.Combine( "/usr/lib/systemd/", systemOrUser ), "*.service" )
			.Select( servicePath => Path.GetFileNameWithoutExtension( servicePath ) )
			.Where( serviceName => !serviceName.EndsWith( "@" ) ) // Skip templates
			.ToArray();

		// Parses a systemd service file (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private static Dictionary<string, Dictionary<string, string>> ParseServiceFile( string systemOrUser, string serviceName ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			// Read the service file
			string[] fileLines = File.ReadAllLines( Path.Combine( "/usr/lib/systemd/", systemOrUser, $"{ serviceName }.service" ) )
				.Where( line => !string.IsNullOrWhiteSpace( line ) ) // Skip empty lines
				.Where( line => !line.StartsWith( "#" ) ) // Skip comments
				.ToArray();

			// Create a dictionary to store all the properties
			Dictionary<string, Dictionary<string, string>> serviceProperties = new();

			// Loop through each line...
			string sectionName = "";
			foreach ( string fileLine in fileLines ) {

				// The line will either be a new section or a property, similar to INI files
				Match sectionMatch = Regex.Match( fileLine, @"^\[(.+)\]$" );
				Match propertyMatch = Regex.Match( fileLine, @"^([^=]+)\s?=\s?(.*)$" );

				// Update the section name, if we have a new section
				if ( sectionMatch.Success ) sectionName = sectionMatch.Groups[ 1 ].Value.Trim();

				// Parse the property, if we have a property
				else if ( propertyMatch.Success ) {
					string propertyName = propertyMatch.Groups[ 1 ].Value.Trim();
					string propertyValue = propertyMatch.Groups[ 2 ].Value.Trim();

					// Create the section in the dictionary if it doesn't exist
					if ( !serviceProperties.ContainsKey( sectionName ) ) serviceProperties[ sectionName ] = new Dictionary<string, string>();

					serviceProperties[ sectionName ][ propertyName ] = propertyValue;

				// Unrecognised line
				} else logger.LogWarning( "Unrecognised service file line: '{0}'", fileLine );
			}

			return serviceProperties;
		}

		// Gets current data about a systemd service (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private Dictionary<string, string> GetServiceData( string serviceName ) {

			// Create the 'systemctl show' command for this service to get all the data
			Process command = new() {
				StartInfo = new() {
					FileName = "systemctl",
					Arguments = $"show { serviceName }",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};

			// Run the command & store all output
			command.Start();
			string outputText = command.StandardOutput.ReadToEnd();
			string errorText = command.StandardError.ReadToEnd();
			command.WaitForExit();

			// Fail if the command failed
			if ( command.ExitCode != 0 ) throw new Exception( $"Command '{ command.StartInfo.FileName }' '{ command.StartInfo.Arguments }' failed with exit code { command.ExitCode } ({ errorText })" );

			// Parse the command output into a dictionary
			return outputText.Split( "\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries )
				.Where( line => !string.IsNullOrWhiteSpace( line ) )
				.Select( line => line.Split( '=', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ) )
				.Where( lineParts => lineParts.Length == 2 )
				.GroupBy( lineParts => lineParts[ 0 ] )
				.Select( group => group.First() )
				.ToDictionary( lineParts => lineParts[ 0 ], lineParts => lineParts[ 1 ] );

		}

	}
}