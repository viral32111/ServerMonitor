using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Diagnostics;
using System.CommandLine; // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using Mono.Unix.Native; // https://github.com/mono/mono.posix
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor {

	public static class Program {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Program" );

		// Create a HTTP client for everything to use - https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-7.0
		public static readonly HttpClient HttpClient = new();
		private static bool IsHTTPClientSetup = false;

		// Instances of each mode
		public static readonly Collector.Collector Collector = new();
		public static readonly Connector.Connector Connector = new();

		// Current version
		public static string Version { get; private set; } = null!;

		public static int Main( string[] arguments ) {

			// Get the directory that the executable DLL/binary is in - https://stackoverflow.com/a/66023223
			Assembly? executable = Assembly.GetEntryAssembly() ?? throw new Exception( "Failed to get this executable" );
			string executableDirectory = Path.GetDirectoryName( executable.Location ) ?? throw new Exception( "Failed to get this executable's directory" );
			logger.LogDebug( $"Executable directory: '{ executableDirectory }'" );

			// Get the version of the executable
			Version = FileVersionInfo.GetVersionInfo( executable.Location )?.FileVersion ?? throw new Exception( "Failed to get this executable's version" );
			logger.LogDebug( $"Executable version: '{ Version }'" );

			// Create the root command, no handler though as only sub-commands are allowed
			RootCommand rootCommand = new( "The backend metrics exporter & RESTful API service for the Android app." );

			// Option to let the user specify an extra configuration file in a non-standard location
			Option<string> extraConfigurationFilePathOption = new(
				name: "--config",
				description: "Path to an additional configuration file.",
				getDefaultValue: () => Path.Combine( executableDirectory, Configuration.FileName ) // Defaults to the same directory as the executable, makes development easier
			);
			rootCommand.AddOption( extraConfigurationFilePathOption );

			// Option not run forever
			Option<bool> runOnceOption = new(
				name: "--once",
				description: "Collect metrics once, or respond to one connection request, then exit.",
				getDefaultValue: () => false
			);
			rootCommand.AddOption( runOnceOption );

			// Option for running as a service
			Option<bool> runAsServiceOption = new(
				name: "--service",
				description: "Run as a service, rather than a command-line program. Do not specify this manually.",
				getDefaultValue: () => false
			);
			rootCommand.AddOption( runAsServiceOption );

			// Sub-command to start in "collector" mode
			Command collectorCommand = new( "collector", "Export metrics to Prometheus from configured sources." );
			collectorCommand.SetHandler( ( string extraConfigurationFilePath, bool runOnce, bool runningAsService ) => {

				// Fail if we're not running as administrator/root
				if ( IsRunningAsAdmin() == false ) {
					logger.LogError( "This program must be run as administrator/root" );
					Environment.Exit( 1 );
					return;
				}

				// Load the configuration
				Configuration.Load( extraConfigurationFilePath );
				logger.LogInformation( "Loaded the configuration" );

				// If we're running as a system service...
				if ( runningAsService == true ) {
					logger.LogDebug( "Running as a system service" );

					// Start from the service base on Windows, or just call the handler on Linux
					if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) == true ) {
						#pragma warning disable CA1416 // IDE doesn't understand the check above...
						using ( Service service = new( Configuration.Config!, runOnce, false, Mode.Collector ) ) ServiceBase.Run( service );
						#pragma warning restore CA1416
					} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) == true ) {
						Collector.HandleCommand( Configuration.Config!, runOnce );
					} else throw new PlatformNotSupportedException( "This operating system is not supported." );

				// Manually call the handler if we're running in an interactive CLI
				} else {
					logger.LogDebug( "Running as command-line program" );
					Collector.HandleCommand( Configuration.Config!, runOnce );
				}

			}, extraConfigurationFilePathOption, runOnceOption, runAsServiceOption );
			rootCommand.AddCommand( collectorCommand );
	
			// Sub-command to start in "connector" mode
			Command connectorCommand = new( "connector", "Serve metrics from Prometheus to the Android app." );
			rootCommand.AddCommand( connectorCommand );

			// Option to not start listening for requests when in "connector" mode
			Option<bool> noListenOption = new(
				name: "--no-listen",
				description: "Do not start the request listening loop.",
				getDefaultValue: () => false
			);
			connectorCommand.AddOption( noListenOption );

			// Handler for the "connector" mode sub-command
			connectorCommand.SetHandler( ( string extraConfigurationFilePath, bool runOnce, bool noListen, bool runningAsService ) => {

				// Load the configuration
				Configuration.Load( extraConfigurationFilePath );
				logger.LogInformation( "Loaded the configuration" );

				// If we're running as a system service...
				if ( runningAsService == true ) {
					logger.LogDebug( "Running as a system service" );

					// Start from the service base on Windows, or just call the handler on Linux
					if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) == true ) {
						#pragma warning disable CA1416 // IDE doesn't understand the check above...
						using ( Service service = new( Configuration.Config!, runOnce, noListen, Mode.Connector ) ) ServiceBase.Run( service );
						#pragma warning restore CA1416
					} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) == true ) {
						Connector.HandleCommand( Configuration.Config!, runOnce, noListen );
					} else throw new PlatformNotSupportedException( "This operating system is not supported." );

				// Manually call the handler if we're running in an interactive CLI
				} else {
					logger.LogDebug( "Running as command-line program" );
					Connector.HandleCommand( Configuration.Config!, runOnce, noListen );
				}

			}, extraConfigurationFilePathOption, runOnceOption, noListenOption, runAsServiceOption );

			return rootCommand.Invoke( arguments );
		}

		// Sets up the HTTP client
		public static void SetupHTTPClient() {

			// Do not run if already setup
			if ( Program.IsHTTPClientSetup == true ) return;

			// Set the request timeout
			Program.HttpClient.Timeout = TimeSpan.FromSeconds( Configuration.Config!.HTTPClientTimeoutSeconds );

			// Wipe all default headers
			Program.HttpClient.DefaultRequestHeaders.Clear();

			// Add headers to accept JSON & close the connection after the request
			Program.HttpClient.DefaultRequestHeaders.Add( "Accept", "application/json, */*" );
			Program.HttpClient.DefaultRequestHeaders.Add( "Connection", "close" );

			// Add our custom user agent, without running it through the normalisation process
			Program.HttpClient.DefaultRequestHeaders.TryAddWithoutValidation( "User-Agent", Configuration.Config!.HTTPClientUserAgent.Replace( "{VERSION}", Version ) );

			// Add Cloudflare Access headers to the HTTP client, if configured
			if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenId ) == false ) Program.HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Id", Configuration.Config!.CloudflareAccessServiceTokenId );
			if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenSecret ) == false ) Program.HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Secret", Configuration.Config!.CloudflareAccessServiceTokenSecret );

			// Print all default request headers
			foreach ( KeyValuePair<string, IEnumerable<string>> header in Program.HttpClient.DefaultRequestHeaders ) logger.LogDebug( "Default HTTP client request header: '{0}' = '{1}'", header.Key, string.Join( ", ", header.Value ) );

			// Set the lock
			Program.IsHTTPClientSetup = true;

		}

		// Checks if this application is running as administrator/root, which is required for some of the metrics we're collecting
		private static bool IsRunningAsAdmin() {
			// Windows - https://stackoverflow.com/a/11660205
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				WindowsIdentity identity = WindowsIdentity.GetCurrent();
				WindowsPrincipal principal = new WindowsPrincipal( identity );
				return principal.IsInRole( WindowsBuiltInRole.Administrator );

			// Linux - https://github.com/dotnet/runtime/issues/25118#issuecomment-367407469
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				return Syscall.getuid() == 0;

			} else throw new PlatformNotSupportedException( "This operating system is not supported." );
		}

	}

	public enum Mode {
		Collector,
		Connector
	}

}
