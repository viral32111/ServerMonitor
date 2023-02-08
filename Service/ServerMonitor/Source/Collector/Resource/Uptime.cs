using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system uptime metrics
	public class Uptime : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Uptime" );

		public double UptimeSeconds { get; private set; } = 0;

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Get the uptime & store as seconds in the property
			TimeSpan uptime = TimeSpan.FromMilliseconds( GetTickCount64() );
			UptimeSeconds = uptime.TotalSeconds;
		}

		// Updates the metrics for Linux...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			using ( FileStream fileStream = new( "/proc/uptime", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Get the first line of the file
					string? fileLine = streamReader.ReadLine();
					if ( string.IsNullOrWhiteSpace( fileLine ) ) throw new Exception( "First line of file is empty or whitespace" );

					// Split the line into its components
					string[] lineParts = fileLine.Split( " ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
					if ( lineParts.Length != 2 ) throw new Exception( "File line has incorrect number of parts" );

					// Get the uptime & store as seconds in the property
					if ( double.TryParse( lineParts[ 0 ], out double uptime ) != true ) throw new Exception( "Failed to parse uptime as double" );
					UptimeSeconds = uptime;

				}
			}
		}

		// C++ Windows API function to get the milliseconds elapsed since system startup - https://stackoverflow.com/a/16673001
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private extern static UInt64 GetTickCount64();

	}

}
