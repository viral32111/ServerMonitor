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
	public class Services : Base {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Services" );

		// Holds the exported Prometheus metrics
		public readonly Gauge StatusCode;
		public readonly Gauge ExitCode;
		public readonly Counter UptimeSeconds;

		// Initialise the exported Prometheus metrics
		public Services( Config configuration ) {
			StatusCode = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_service_code_status", "Service status code", new GaugeConfiguration {
				LabelNames = new[] { "service", "name", "description" }
			} );
			ExitCode = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_service_code_exit", "Service exit code", new GaugeConfiguration {
				LabelNames = new[] { "service", "name", "description" }
			} );
			UptimeSeconds = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_service_uptime_seconds", "Service uptime, in seconds", new CounterConfiguration {
				LabelNames = new[] { "service", "name", "description" }
			} );

			StatusCode.Set( 0 );
			UptimeSeconds.IncTo( 0 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

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
				if ( process == null ) throw new Exception( "Service has no process" );
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
					string creationDateText = processManagementObject[ "CreationDate" ].ToString() ?? throw new Exception( "Service has no creation date" );
					return ( DateTime.Now - ManagementDateTimeConverter.ToDateTime( creationDateText ) ).TotalSeconds;
				}
			}

			throw new Exception( "Service has no uptime" );
		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Loop through all system services...
			foreach ( string serviceName in GetServiceNames( "system" ) ) {
				
				// Skip template services?
				if ( serviceName.EndsWith( "@" ) ) {
					//logger.LogWarning( "Service '{0}' is a template? Skipping...", serviceName );
					continue;
				}

				// Get information & live data about this service
				Dictionary<string, Dictionary<string, string>> serviceInformation = ParseServiceFile( "system", serviceName );
				Dictionary<string, string> serviceData = GetServiceData( serviceName );

				// Try to get the description of this service from the unit section
				string serviceDescription = "";
				if ( serviceInformation.TryGetValue( "Unit", out Dictionary<string, string>? unitSection ) == true && unitSection != null ) {
					if ( unitSection.TryGetValue( "Description", out string? description ) == true && string.IsNullOrWhiteSpace( description ) == false ) {
						serviceDescription = description;
					} else {
						logger.LogWarning( "Service '{0}' has no description in unit section", serviceName );
					}
				} else {
					logger.LogWarning( "Service '{0}' has no unit section", serviceName );
				}

				// Try to get the process ID of this service from the service section
				int? processIdentifier = null;
				if ( serviceInformation.TryGetValue( "Service", out Dictionary<string, string>? serviceSection ) == true && serviceSection != null ) {
					if ( serviceSection.TryGetValue( "PIDFile", out string? processIdentifierFilePath ) == true && string.IsNullOrWhiteSpace( processIdentifierFilePath ) == false ) {
						if ( File.Exists( processIdentifierFilePath ) == true ) {
							processIdentifier = int.Parse( File.ReadAllLines( processIdentifierFilePath )[ 0 ] );
						} else {
							logger.LogWarning( "Service '{0}' process identifier file '{1}' does not exist", serviceName, processIdentifierFilePath );
						}
					} else {
						//logger.LogWarning( "Service '{0}' has no process identifier file path in service section", serviceName );
					}
				} else {
					logger.LogWarning( "Service '{0}' has no service section", serviceName );
				}

				// Fallback to the service data if we don't have a process identifier yet
				int mainProcessIdentifier = int.Parse( serviceData[ "MainPID" ] );
				int executingProcessIdentifier = int.Parse( serviceData[ "ExecMainPID" ] );
				if ( mainProcessIdentifier != 0 ) processIdentifier ??= mainProcessIdentifier;
				if ( executingProcessIdentifier != 0 ) processIdentifier ??= executingProcessIdentifier;

				// Status from the service data
				int serviceStatus = serviceData[ "ActiveState" ] switch {
					"inactive" => 0,
					"active" => 1,
					"reloading" => 2,
					"failed" => 3,
					"exited" => 4,
					_ => throw new Exception( $"Unrecognised service status: '{ serviceData[ "ActiveState" ] }'" )
				};

				// Exit code from the service data
				int serviceExitCode = int.Parse( serviceData[ "ExecMainStatus" ] );

				if ( processIdentifier != null && processIdentifier != 0 ) {
					logger.LogDebug( "Service '{0}': PID: {1}, Status: {2}, Exit Code: {3}", serviceName, processIdentifier, serviceStatus, serviceExitCode );
				} else {
					logger.LogWarning( "Service '{0}': Status: {2}, Exit Code: {3}", serviceName, serviceStatus, serviceExitCode );
				}

				// Update the exported Prometheus metrics
				// NOTE: Linux has no display names, so we use the service name for both
				StatusCode.WithLabels( serviceName, serviceName, serviceDescription ).Set( serviceStatus );
				ExitCode.WithLabels( serviceName, serviceName, serviceDescription ).Set( serviceExitCode );
				UptimeSeconds.WithLabels( serviceName, serviceName, serviceDescription ).IncTo( 0 ); // TODO
			}

			// Loop through all user services...
			/*foreach ( string serviceName in GetServiceNames( "user" ) ) {
				Dictionary<string, Dictionary<string, string>> serviceInformation = ParseServiceFile( "user", serviceName );

				// Try to get the description of this service from the unit section
				if ( serviceInformation.TryGetValue( "Unit", out Dictionary<string, string>? unitSection ) == false || unitSection == null ) {
					logger.LogWarning( "Service {0} has no unit section", serviceName );
					continue;
				};
				if ( unitSection.TryGetValue( "Description", out string? description ) == false || description == null ) {
					logger.LogWarning( "Service {0} has no description", serviceName );
					continue;
				}

				// Update the exported Prometheus metrics
				// NOTE: Linux has no display names, so we use the service name for both
				StatusCode.WithLabels( serviceName, serviceName, description ).Set( 0 ); // TODO
				ExitCode.WithLabels( serviceName, serviceName, description ).Set( 0 ); // TODO
				UptimeSeconds.WithLabels( serviceName, serviceName, description ).IncTo( 0 ); // TODO
			}*/

			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Gets a list of systemd service names (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private static string[] GetServiceNames( string systemOrUser ) => Directory.GetFiles( Path.Combine( "/usr/lib/systemd/", systemOrUser ), "*.service" )
			.Select( servicePath => Path.GetFileNameWithoutExtension( servicePath ) )
			.ToArray();

		// Parses a systemd service file (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private static Dictionary<string, Dictionary<string, string>> ParseServiceFile( string systemOrUser, string serviceName ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

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

					if ( !serviceProperties.ContainsKey( sectionName ) ) {
						serviceProperties[ sectionName ] = new Dictionary<string, string>();
					}

					serviceProperties[ sectionName ][ propertyName ] = propertyValue;

				// Unrecognised line
				} else logger.LogWarning( "Unrecognised service file line: '{0}'", fileLine );
			}

			return serviceProperties;
		}

		// Gets information about a systemd service (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private Dictionary<string, string> GetServiceData( string serviceName ) {
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

			command.Start();
			string outputText = command.StandardOutput.ReadToEnd();
			string errorText = command.StandardError.ReadToEnd();
			command.WaitForExit();

			if ( command.ExitCode != 0 ) throw new Exception( $"Command failed with exit code { command.ExitCode }: '{ errorText }'" );

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