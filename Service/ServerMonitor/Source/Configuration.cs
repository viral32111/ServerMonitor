using System;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Extensions.Configuration; // https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration

/* Paths for the standard configuration files:
 Windows (System): C:\ProgramData\ServerMonitor\config.json
 Windows (User): %USERPROFILE%\AppData\Local\ServerMonitor\config.json
 Linux (System): /etc/server-monitor/config.json
 Linux (User): ~/.config/server-monitor/config.json
*/

namespace ServerMonitor {

	public static class Configuration {

		// Name of the configuration file
		public static readonly string FileName = "config.json";

		// Gets the path to the user's configuration file
		public static string GetUserFilePath() {
			// Windows: %USERPROFILE%\AppData\Local\ServerMonitor\config.json
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "ServerMonitor", FileName );

			// Linux: ~/.config/server-monitor/config.json
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
			Console.WriteLine( "System-wide configuration file: '{0}' (exists: {1})", GetSystemFilePath(), File.Exists( GetSystemFilePath() ) );
			Console.WriteLine( "User configuration file: '{0}' (exists: {1})", GetUserFilePath(), File.Exists( GetUserFilePath() ) );
			Console.WriteLine( "Extra configuration file: '{0}' (exists: {1})", extraFilePath, File.Exists( extraFilePath ) );

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
