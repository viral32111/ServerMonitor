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

		// Gets the path to the user's configuration file
		public static string GetUserFilePath() {
			// Windows: C:\Users\USERNAME\AppData\Local\ServerMonitor\config.json
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "ServerMonitor", FileName );

			// Linux: /home/USERNAME/.config/server-monitor/config.json
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), ".config", "server-monitor", FileName );

			// Anything else is unsupported
			} else throw new Exception( "Unsupported operating system" );
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
			} else throw new Exception( "Unsupported operating system" );
		}

		// Loads the configuration from JSON files & environment variables
		public static Config Load( string extraFilePath ) {
			logger.LogDebug( "System-wide configuration file: '{0}' (Exists: {1})", GetSystemFilePath(), File.Exists( GetSystemFilePath() ) );
			logger.LogDebug( "User configuration file: '{0}' (Exists: {1})", GetUserFilePath(), File.Exists( GetUserFilePath() ) );
			logger.LogDebug( "Extra configuration file: '{0}' (Exists: {1})", extraFilePath, File.Exists( extraFilePath ) );

			ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();

			// Load standard configuration files
			configurationBuilder.AddJsonFile( GetSystemFilePath(), optional: true, reloadOnChange: false );
			configurationBuilder.AddJsonFile( GetUserFilePath(), optional: true, reloadOnChange: false );

			// Load non-standard configuration file, if provided
			if ( extraFilePath != null ) configurationBuilder.AddJsonFile( extraFilePath, optional: true, reloadOnChange: false );

			// Load environment variables
			configurationBuilder.AddEnvironmentVariables( "SERVER_MONITOR_" );

			// Build the configuration
			Config? config = configurationBuilder.Build().Get<Config>();
			if ( config == null ) throw new Exception( "Failed to load configuration (malformed or missing properties?)" );

			return config;
		}

	}

	// Structure of the configuration file
	public sealed class Config {

		public required int Test { get; set; }

	}

}