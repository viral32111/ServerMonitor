using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector {
	public static class Services {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Services" );

		public static void List() {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) ListOnWindows();
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) ListOnLinux();
			else throw new Exception( "Unsupported operating system" );
		}

		[ SupportedOSPlatform( "windows" ) ]
		public static void ListOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			throw new NotImplementedException();
		}

		[ SupportedOSPlatform( "linux" ) ]
		public static void ListOnLinux() {
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
				logger.LogDebug( " - {0}: {0}", serviceFileName, serviceFileData[ "Unit" ][ "Description" ] );
			}

			logger.LogDebug( "User Services: {0}", userServiceFileNames.Length );
			foreach (string serviceFileName in userServiceFileNames) {
				Dictionary<string, Dictionary<string, string>> serviceFileData = ParseServiceFile( "/usr/lib/systemd/user/" + serviceFileName + ".service" );
				logger.LogDebug( " - {0}: {0}", serviceFileName, serviceFileData[ "Unit" ][ "Description" ] );
			}
		}

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