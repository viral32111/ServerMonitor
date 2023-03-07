using System;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Extensions.Configuration; // https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

// https://stackoverflow.com/a/38790956
// https://developers.redhat.com/blog/2018/11/07/dotnet-special-folder-api-linux#environment_getfolderpath

namespace ServerMonitor {

	public static class Configuration {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Configuration" );

		// Name of the configuration file
		public static readonly string FileName = "config.json";

		// The loaded configuration
		public static Config? Config { get; private set; } = null;

		// Gets the path to the user's configuration file
		public static string GetUserFilePath() {
			// Windows: C:\Users\USERNAME\AppData\Local\ServerMonitor\config.json
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "ServerMonitor", FileName );

			// Linux: /home/USERNAME/.config/server-monitor/config.json
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), ".config", "server-monitor", FileName );

			// Anything else is unsupported
			} else throw new PlatformNotSupportedException( "Unsupported operating system" );
		}

		// Gets the path to the system-wide configuration file
		public static string GetSystemFilePath() {
			// Windows: C:\ProgramData\ServerMonitor\config.json
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), "ServerMonitor", FileName );

			// Linux: /etc/server-monitor/config.json
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				return Path.Combine( "/etc", "server-monitor", FileName ); // No SpecialFolder enum for /etc

			// Anything else is unsupported
			} else throw new PlatformNotSupportedException( "Unsupported operating system" );
		}

		// Loads the configuration from JSON files & environment variables
		public static void Load( string extraFilePath ) {
			logger.LogDebug( "System-wide configuration file: '{0}' (Exists: {1})", GetSystemFilePath(), File.Exists( GetSystemFilePath() ) );
			logger.LogDebug( "User configuration file: '{0}' (Exists: {1})", GetUserFilePath(), File.Exists( GetUserFilePath() ) );
			logger.LogDebug( "Extra configuration file: '{0}' (Exists: {1})", extraFilePath, File.Exists( extraFilePath ) );

			ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();

			// Load standard configuration files
			configurationBuilder.AddJsonFile( GetSystemFilePath(), optional: true, reloadOnChange: false );
			configurationBuilder.AddJsonFile( GetUserFilePath(), optional: true, reloadOnChange: false );

			// Load non-standard configuration file, if provided
			configurationBuilder.AddJsonFile( extraFilePath, optional: true, reloadOnChange: false );

			// Load environment variables (e.g., SERVER_MONITOR_TEST=4)
			configurationBuilder.AddEnvironmentVariables( "SERVER_MONITOR_" );

			// Build the configuration
			Config = configurationBuilder.Build().Get<Config>();
			if ( Config == null ) throw new Exception( "Failed to load configuration (malformed or missing properties?)" );
		}

	}

	// Structure of the configuration file
	public sealed class Config {

		// Prometheus metrics server
		public required string PrometheusListenAddress { get; set; } = "127.0.0.1";
		public required int PrometheusListenPort { get; set; } = 5000;
		public required string PrometheusListenPath { get; set; } = "metrics/";
		public required string PrometheusMetricsPrefix { get; set; } = "server_monitor_";

		// Resource metrics
		public required bool CollectProcessorMetrics { get; set; } = true;
		public required bool CollectMemoryMetrics { get; set; } = true;
		public required bool CollectDiskMetrics { get; set; } = true;
		public required bool CollectNetworkMetrics { get; set; } = true;
		public required bool CollectUptimeMetrics { get; set; } = true;
		public required bool CollectPowerMetrics { get; set; } = false;
		public required bool CollectFanMetrics { get; set; } = false;

		// Service metrics
		public required bool CollectServiceMetrics { get; set; } = true;

		// Docker metrics
		public required bool CollectDockerMetrics { get; set; } = true;
		public required string DockerEngineAPIAddress { get; set; } = "tcp://127.0.0.1:2375";
		public required float DockerEngineAPIVersion { get; set; } = 1.41f;

		// SNMP options
		public required bool CollectSNMPMetrics { get; set; } = true;
		public required string SNMPManagerListenAddress { get; set; } = "0.0.0.0";
		public required int SNMPManagerListenPort { get; set; } = 162;
		public required string SNMPCommunity { get; set; } = "public";
		public required SNMPAgent[] SNMPAgents { get; set; } = Array.Empty<SNMPAgent>();

		// Connection point options
		public required string ConnectorListenAddress { get; set; } = "127.0.0.1";
		public required int ConnectorListenPort { get; set; } = 8080;
		public required string ConnectorPublicUrl { get; set; } = "http://localhost:8080/";

	}

	public sealed class SNMPAgent {
		public required string Address { get; set; } = "localhost";
		public required int Port { get; set; } = 161;
	}

}
