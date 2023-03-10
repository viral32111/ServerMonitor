using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Collections.Generic;
using System.CommandLine; // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor {

	public static class Program {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Program" );

		// Create a HTTP client for everything to use - https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-7.0
		public static readonly HttpClient HttpClient = new();
		private static bool IsHTTPClientSetup = false;

		public static int Main( string[] arguments ) {

			// Get the directory that the executable DLL/binary is in - https://stackoverflow.com/a/66023223
			Assembly? executable = Assembly.GetEntryAssembly() ?? throw new Exception( "Failed to get this executable" );
			string executableDirectory = Path.GetDirectoryName( executable.Location ) ?? throw new Exception( "Failed to get this executable's directory" );
			logger.LogDebug( $"Executable directory: '{ executableDirectory }'" );

			// Create the root command, no handler though as only sub-commands are allowed
			RootCommand rootCommand = new( "The backend API/service for the server monitoring mobile app." );

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

			// Sub-command to start in "collector" mode
			Command collectorCommand = new( "collector", "Expose metrics to Prometheus from configured sources." );
			collectorCommand.SetHandler( ( string extraConfigurationFilePath, bool runOnce ) => {
				
				// Load the configuration
				Configuration.Load( extraConfigurationFilePath );
				logger.LogInformation( "Loaded the configuration" );

				// Call the handler
				Collector.Collector.HandleCommand( Configuration.Config!, runOnce );

			}, extraConfigurationFilePathOption, runOnceOption );
			rootCommand.AddCommand( collectorCommand );
	
			// Sub-command to start in "connection point" mode
			Command connectorCommand = new( "connector", "Serve metrics from Prometheus to the mobile app." );
			rootCommand.AddCommand( connectorCommand );

			// Option to not start listening for requests when in "connection point" mode
			Option<bool> noListenOption = new(
				name: "--no-listen",
				description: "Do not start the request listening loop.",
				getDefaultValue: () => false
			);
			connectorCommand.AddOption( noListenOption );

			// Handler for the "connection point" mode sub-command
			connectorCommand.SetHandler( ( string extraConfigurationFilePath, bool runOnce, bool noListen ) => {

				// Load the configuration
				Configuration.Load( extraConfigurationFilePath );
				logger.LogInformation( "Loaded the configuration" );

				// Call the handler
				new Connector.Connector().HandleCommand( Configuration.Config!, runOnce, noListen );

			}, extraConfigurationFilePathOption, runOnceOption, noListenOption );

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
			Program.HttpClient.DefaultRequestHeaders.TryAddWithoutValidation( "User-Agent", Configuration.Config!.HTTPClientUserAgent );

			// Add Cloudflare Access headers to the HTTP client, if configured
			if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenId ) == false ) Program.HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Id", Configuration.Config!.CloudflareAccessServiceTokenId );
			if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenSecret ) == false ) Program.HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Secret", Configuration.Config!.CloudflareAccessServiceTokenSecret );

			// Print all default request headers
			foreach ( KeyValuePair<string, IEnumerable<string>> header in Program.HttpClient.DefaultRequestHeaders ) logger.LogDebug( "Default HTTP client request header: '{0}' = '{1}'", header.Key, string.Join( ", ", header.Value ) );

			// Set the lock
			Program.IsHTTPClientSetup = true;

		}

	}

}
