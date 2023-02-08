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
		public double SwapTotalBytes { get; private set; } = 0;
		public double SwapFreeBytes { get; private set; } = 0;

		// Simply returns the utilization
		public double GetUsedBytes() => TotalBytes - FreeBytes;
		public double GetUsedPercentage() => ( GetUsedBytes() / TotalBytes ) * 100;
		public double GetSwapUsedBytes() => SwapTotalBytes - SwapFreeBytes;
		public double GetSwapUsedPercentage() => ( GetSwapUsedBytes() / SwapTotalBytes ) * 100;

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Call the Windows API functions to populate the structures with raw data - https://stackoverflow.com/a/105109
			MEMORYSTATUSEX memoryStatus = new();
			//PERFORMANCE_INFORMATION performanceInformation = new();
			if ( !GlobalMemoryStatusEx( memoryStatus ) ) throw new Exception( "Failed to get system memory status" );
			//if ( !K32GetPerformanceInfo( performanceInformation, performanceInformation.cb ) ) throw new Exception( "Failed to get system performance information" );

			// Cast the raw data to the appropriate types & store it in the properties
			TotalBytes = ( double ) memoryStatus.ullTotalPhys;
			FreeBytes = ( double ) memoryStatus.ullAvailPhys;
			//SwapTotalBytes = ( double ) performanceInformation.CommitLimit - performanceInformation.CommitTotal;
			//SwapFreeBytes = ( double ) performanceInformation.PageSize;
			SwapTotalBytes = ( double ) memoryStatus.ullTotalPageFile - memoryStatus.ullTotalPhys;
			SwapFreeBytes = ( double ) memoryStatus.ullAvailPageFile - memoryStatus.ullAvailPhys;

			/*logger.LogDebug( "CommitTotal: {0}", performanceInformation.CommitTotal );
			logger.LogDebug( "CommitLimit: {0}", performanceInformation.CommitLimit );
			logger.LogDebug( "CommitPeak: {0}", performanceInformation.CommitPeak );
			logger.LogDebug( "PhysicalTotal: {0}", performanceInformation.PhysicalTotal );
			logger.LogDebug( "PhysicalAvailable: {0}", performanceInformation.PhysicalAvailable );
			logger.LogDebug( "SystemCache: {0}", performanceInformation.SystemCache );
			logger.LogDebug( "KernelTotal: {0}", performanceInformation.KernelTotal );
			logger.LogDebug( "KernelPaged: {0}", performanceInformation.KernelPaged );
			logger.LogDebug( "KernelNonpaged: {0}", performanceInformation.KernelNonpaged );
			logger.LogDebug( "PageSize: {0}", performanceInformation.PageSize );
			logger.LogDebug( "HandleCount: {0}", performanceInformation.HandleCount );
			logger.LogDebug( "ProcessCount: {0}", performanceInformation.ProcessCount );
			logger.LogDebug( "ThreadCount: {0}", performanceInformation.ThreadCount );*/

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

					// Update the properties with the final memory information
					TotalBytes = memoryInformation[ "MemTotal" ];
					FreeBytes = memoryInformation[ "MemFree" ] - memoryInformation[ "Cached" ] - memoryInformation[ "Buffers" ];
					SwapTotalBytes = memoryInformation[ "SwapTotal" ];
					SwapFreeBytes = memoryInformation[ "SwapFree" ];
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

		// C++ Windows API structure for GetPerformanceInfo() - https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private class PERFORMANCE_INFORMATION {
			public uint cb = ( uint ) Marshal.SizeOf( typeof( PERFORMANCE_INFORMATION ) );
			public ulong CommitTotal;
			public ulong CommitLimit;
			public ulong CommitPeak;
			public ulong PhysicalTotal;
			public ulong PhysicalAvailable;
			public ulong SystemCache;
			public ulong KernelTotal;
			public ulong KernelPaged;
			public ulong KernelNonpaged;
			public ulong PageSize;
			public uint HandleCount;
			public uint ProcessCount;
			public uint ThreadCount;
		}

		// C++ Windows API function to get information about system memory - https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex
		// The PerformanceCounter class is not good enough, it does not return enough detail...
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool GlobalMemoryStatusEx( [ In, Out ] MEMORYSTATUSEX lpBuffer );

		// The C++ Windows API function to get information about system performance - https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getperformanceinfo
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool K32GetPerformanceInfo( [ In, Out ] PERFORMANCE_INFORMATION pPerformanceInformation, [ In ] uint cb );

	}
}
