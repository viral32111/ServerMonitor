using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system processor metrics
	public class Processor {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Processor" );

		// Holds the metrics from the latest update
		public double Usage { get; private set; }

		// Calls the appropriate update function depending on the operating system...
		public void Update() {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) UpdateOnWindows();
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) UpdateOnLinux();
			else throw new Exception( "Unsupported operating system" );
		}

		// Updates the metrics for Windows...
		private void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Get processor usage from the Performance Monitor interface - https://stackoverflow.com/a/278088
			// NOTE: Two samples required with 1 second wait to get an accurate reading
			PerformanceCounter cpuCounter = new( "Processor", "% Processor Time", "_Total" );
			cpuCounter.NextValue();
			Thread.Sleep( 1000 );
			Usage = cpuCounter.NextValue();
		}

		// Updates the metrics for Linux...
		private void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			int previousTotal = 0;
			int previousIdle = 0;

			// Loop twice
			for ( int iteration = 0; iteration < 2; iteration++ ) {

				// Read the psuedo-file to get the current processor statistics
				using ( FileStream fileStream = new( "/proc/stat", FileMode.Open, FileAccess.Read ) ) {
					using ( StreamReader streamReader = new( fileStream ) ) {

						// Get the first line of the file
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Ensure this is the relevant line
						if ( !fileLine.StartsWith( "cpu " ) ) throw new Exception( "Unrecognised file line" );
						fileLine = fileLine.Substring( 4 );

						// Split the line into its components
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 4 ) throw new Exception( "File line has incorrect number of parts" );

						// Get the individual times - https://linux.die.net/man/5/proc
						if ( int.TryParse( lineParts[ 0 ], out int userTime ) != true ) throw new Exception( "Failed to parse user time as integer" );
						if ( int.TryParse( lineParts[ 1 ], out int niceTime ) != true ) throw new Exception( "Failed to parse nice time as integer" );
						if ( int.TryParse( lineParts[ 2 ], out int systemTime ) != true ) throw new Exception( "Failed to parse system time as integer" );
						if ( int.TryParse( lineParts[ 3 ], out int idleTime ) != true ) throw new Exception( "Failed to parse idle time as integer" );
						int totalTime = userTime + niceTime + systemTime + idleTime;

						// https://askubuntu.com/a/450136
						int idleTimeDelta = idleTime - previousIdle;
						int totalTimeDelta = totalTime - previousTotal;
						Usage = ( float ) ( 1000 * ( totalTimeDelta - idleTimeDelta ) / ( float ) totalTimeDelta + 5 ) / 10;

						// Update previous values
						previousIdle = idleTime;
						previousTotal = totalTime;

						// Wait before next sample
						Thread.Sleep( 1000 );

					}
				}

			}

		}

	}
}
