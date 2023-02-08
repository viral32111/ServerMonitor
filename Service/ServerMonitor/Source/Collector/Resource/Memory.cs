using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system memory metrics
	public class Memory : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Memory" );

		// Holds the metrics from the latest update
		public double TotalBytes { get; private set; } = 0;
		public double FreeBytes { get; private set; } = 0;

		// Simply returns the utilization
		public double GetUsedBytes() => TotalBytes - FreeBytes;
		public double GetUsedPercentage() => ( GetUsedBytes() / TotalBytes ) * 100;

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Call the Windows API function to populate the structure with raw data - https://stackoverflow.com/a/105109
			MEMORYSTATUSEX memoryStatus = new();
			if ( !GlobalMemoryStatusEx( memoryStatus ) ) throw new Exception( "Failed to get system memory status" );

			// Cast the raw data to the appropriate types & store it in the properties
			TotalBytes = ( double ) memoryStatus.ullTotalPhys;
			FreeBytes = ( double ) memoryStatus.ullAvailPhys;
		}

		// Updates the metrics for Linux...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the psuedo-file to get the current memory information
			using ( FileStream fileStream = new( "/proc/meminfo", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Create a dictionary to hold the final memory information
					Dictionary<string, double> memoryInformation = new();

					// Read each line of the file until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split the line into the name & data (e.g., "MemTotal:       123456 kB")
						string[] lineParts = fileLine.Split( ":", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length != 2 ) throw new Exception( "Unexpected number of parts on file line" );
						string name = lineParts[ 0 ], data = lineParts[ 1 ];

						// Split the data into value & suffix
						string[] dataParts = data.Split( " ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( double.TryParse( dataParts[ 0 ], out double value ) != true ) throw new Exception( "Failed to parse data value as double" );

						// If there is a suffix, convert the value down to bytes
						if ( dataParts.Length == 2 ) {
							memoryInformation.Add( name, dataParts[ 1 ] switch {
								"kB" => value * 1024, // https://superuser.com/q/1737654
								_ => throw new Exception( "Unrecognised suffix" )
							} );

						// No suffix so just add the value as-is
						} else {
							memoryInformation.Add( name, value );
						}
					} while ( !streamReader.EndOfStream );

					TotalBytes = memoryInformation[ "MemTotal" ];
					FreeBytes = memoryInformation[ "MemFree" ] - memoryInformation[ "Cached" ] - memoryInformation[ "Buffers" ];
				}
			}
		}

		// C++ Windows API structure for GlobalMemoryStatusEx() - https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private class MEMORYSTATUSEX {
			public uint dwLength = ( uint ) Marshal.SizeOf( typeof( MEMORYSTATUSEX ) );
			public uint dwMemoryLoad;
			public ulong ullTotalPhys;
			public ulong ullAvailPhys;
			public ulong ullTotalPageFile;
			public ulong ullAvailPageFile;
			public ulong ullTotalVirtual;
			public ulong ullAvailVirtual;
			public ulong ullAvailExtendedVirtual;
		}

		// C++ Windows API function to get information about system memory - https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex
		// The PerformanceCounter class is not good enough, it does not return enough detail...
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool GlobalMemoryStatusEx( [ In, Out ] MEMORYSTATUSEX lpBuffer );

	}
}
