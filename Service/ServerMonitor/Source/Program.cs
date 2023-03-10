using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.CommandLine; // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor {

	public static class Program {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Program" );

		// Setup a HTTP client for everything to use - https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-7.0
		public static readonly HttpClient HttpClient = new() {
			DefaultRequestHeaders = {
				{ "Accept", "application/json, */*" },
				{ "Connection", "close" }
			}
		};

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

				// Set the request timeout & user agent on the HTTP client
				HttpClient.Timeout = TimeSpan.FromSeconds( Configuration.Config!.HTTPClientTimeoutSeconds );
				HttpClient.DefaultRequestHeaders.Add( "User-Agent", Configuration.Config!.HTTPClientUserAgent );

				// Add Cloudflare Access headers to the HTTP client, if configured
				if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenId ) == false ) HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Id", Configuration.Config!.CloudflareAccessServiceTokenId );
				if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenSecret ) == false ) HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Secret", Configuration.Config!.CloudflareAccessServiceTokenSecret );

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

				// Set the request timeout & user agent on the HTTP client
				HttpClient.Timeout = TimeSpan.FromSeconds( Configuration.Config!.HTTPClientTimeoutSeconds );
				HttpClient.DefaultRequestHeaders.Add( "User-Agent", Configuration.Config!.HTTPClientUserAgent );

				// Add Cloudflare Access headers to the HTTP client, if configured
				if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenId ) == false ) HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Id", Configuration.Config!.CloudflareAccessServiceTokenId );
				if ( string.IsNullOrWhiteSpace( Configuration.Config!.CloudflareAccessServiceTokenSecret ) == false ) HttpClient.DefaultRequestHeaders.Add( "CF-Access-Client-Secret", Configuration.Config!.CloudflareAccessServiceTokenSecret );

				// Call the handler
				new Connector.Connector().HandleCommand( Configuration.Config!, runOnce, noListen );

			}, extraConfigurationFilePathOption, runOnceOption, noListenOption );

			return rootCommand.Invoke( arguments );
		}

	}

}
