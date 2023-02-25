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

			string[] systemServiceFileNames = Directory.GetFiles( "/usr/lib/systemd/system", "*.service" )
				.Select( servicePath => Path.GetFileNameWithoutExtension( servicePath ) )
				.ToArray();

			string[] userServiceFileNames = Directory.GetFiles( "/usr/lib/systemd/user", "*.service" )
				.Select( servicePath => Path.GetFileNameWithoutExtension( servicePath ) )
				.ToArray();

			logger.LogDebug( "System Services: {0}", systemServiceFileNames.Length );
			foreach (string serviceFileName in systemServiceFileNames) {
				Dictionary<string, Dictionary<string, string>> serviceFileData = ParseServiceFile( "/usr/lib/systemd/system/" + serviceFileName + ".service" );
				if ( !serviceFileData.ContainsKey( "Unit" ) ) {
					logger.LogWarning( " - {0}: No Unit section", serviceFileName );
				} else if ( !serviceFileData[ "Unit" ].ContainsKey( "Description" ) ) {
					logger.LogWarning( " - {0}: No Description key in Unit section", serviceFileName );
				} else {
					logger.LogDebug( " - {0}: {0}", serviceFileName, serviceFileData[ "Unit" ][ "Description" ] );
				}
			}

			logger.LogDebug( "User Services: {0}", userServiceFileNames.Length );
			foreach (string serviceFileName in userServiceFileNames) {
				Dictionary<string, Dictionary<string, string>> serviceFileData = ParseServiceFile( "/usr/lib/systemd/user/" + serviceFileName + ".service" );
				if ( !serviceFileData.ContainsKey( "Unit" ) ) {
					logger.LogWarning( " - {0}: No Unit section", serviceFileName );
				} else if ( !serviceFileData[ "Unit" ].ContainsKey( "Description" ) ) {
					logger.LogWarning( " - {0}: No Description key in Unit section", serviceFileName );
				} else {
					logger.LogDebug( " - {0}: {0}", serviceFileName, serviceFileData[ "Unit" ][ "Description" ] );
				}
			}
		}

		// Parses a systemd service file (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private static Dictionary<string, Dictionary<string, string>> ParseServiceFile( string filePath ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			string[] fileLines = File.ReadAllLines( filePath )
				.Where( line => !string.IsNullOrWhiteSpace( line ) ) // Skip empty lines
				.Where( line => !line.StartsWith( "#" ) ) // Skip comments
				.ToArray();

			Dictionary<string, Dictionary<string, string>> serviceProperties = new();

			string sectionName = "";
			foreach ( string fileLine in fileLines ) {

				// Match the line
				Match sectionMatch = Regex.Match( fileLine, @"^\[(.+)\]$" );
				Match propertyMatch = Regex.Match( fileLine, @"^([^=]+)\s?=\s?(.*)$" );

				// Update the section name
				if ( sectionMatch.Success ) sectionName = sectionMatch.Groups[ 1 ].Value.Trim();

				// Parse the property
				else if ( propertyMatch.Success ) {
					string propertyName = propertyMatch.Groups[ 1 ].Value.Trim();
					string propertyValue = propertyMatch.Groups[ 2 ].Value.Trim();

					if ( !serviceProperties.ContainsKey( sectionName ) ) {
						serviceProperties[ sectionName ] = new Dictionary<string, string>();
					}

					serviceProperties[ sectionName ][ propertyName ] = propertyValue;
					//logger.LogTrace( "[{0}] {1} = {2}", sectionName, propertyName, propertyValue );

				// Unknown?
				} else {
					logger.LogWarning( "Unrecognised service file line: '{0}'", fileLine );
				}
			}

			return serviceProperties;
		}

	}
}