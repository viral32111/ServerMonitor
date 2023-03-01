using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system memory metrics
	public class Memory : Base {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Memory" );

		// Holds the exported Prometheus metrics
		public readonly Gauge TotalBytes;
		public readonly Gauge FreeBytes;
		public readonly Gauge SwapTotalBytes;
		public readonly Gauge SwapFreeBytes;

		// Initialise the exported Prometheus metrics
		public Memory( Config configuration ) {
			TotalBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_total_bytes", "Total system memory, in bytes." );
			FreeBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_free_bytes", "Free system memory, in bytes." );
			SwapTotalBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_swap_total_bytes", "Total swap/page-file, in bytes." );
			SwapFreeBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_swap_free_bytes", "Free swap/page-file, in bytes." );
			TotalBytes.Set( -1 );
			FreeBytes.Set( -1 );
			SwapTotalBytes.Set( -1 );
			SwapFreeBytes.Set( -1 );
			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// Call the Windows API functions to populate the structures with raw data - https://stackoverflow.com/a/105109
			MEMORYSTATUSEX memoryStatus = new();
			memoryStatus.dwLength = ( UInt32 ) Marshal.SizeOf( memoryStatus );
			PERFORMANCE_INFORMATION performanceInformation = new();
			performanceInformation.cb = ( UInt32 ) Marshal.SizeOf( performanceInformation );
			if ( !GlobalMemoryStatusEx( memoryStatus ) ) throw new Exception( "Windows API function GlobalMemoryStatusEx() failed" );
			if ( !K32GetPerformanceInfo( performanceInformation, performanceInformation.cb ) ) throw new Exception( "Windows API function K32GetPerformanceInfo() failed" );

			// Set the values for the exported Prometheus memory metrics
			TotalBytes.Set( memoryStatus.ullTotalPhys );
			FreeBytes.Set( memoryStatus.ullAvailPhys );

			// Get page file usage percentage from Performance Monitor counter - https://serverfault.com/a/399880
			using ( PerformanceCounter performanceCounter = new( "Paging File", "% Usage", "_Total" ) ) {
				float pageFileUsagePercentage = performanceCounter.NextValue();

				// Set the values for the exported Prometheus page-file metrics
				SwapTotalBytes.Set( ( double ) memoryStatus.ullTotalPageFile - memoryStatus.ullTotalPhys );
				SwapFreeBytes.Set( SwapTotalBytes.Value * ( 1 - ( pageFileUsagePercentage / 100 ) ) );
			}

			logger.LogDebug( "Updated Prometheus metrics" );

		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

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
						if ( lineParts.Length != 2 ) throw new Exception( $"Memory information line part count is { lineParts }, expected 2" );
						string name = lineParts[ 0 ], data = lineParts[ 1 ];

						// Split the data into value & suffix
						string[] dataParts = data.Split( " ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( double.TryParse( dataParts[ 0 ], out double value ) != true ) throw new Exception( $"Failed to parse data part value '{ dataParts[ 0 ] }' as double" );

						// If there is a suffix, convert the value down to bytes
						if ( dataParts.Length == 2 ) memoryInformation.Add( name, dataParts[ 1 ] switch {
							"kB" => value * 1024, // https://superuser.com/q/1737654
							_ => throw new Exception( $"Unrecognised data suffix '{ dataParts[ 1 ] }'" )
						} );

						// Otherwise there is no suffix, so just add the value as-is
						else memoryInformation.Add( name, value );
					} while ( !streamReader.EndOfStream );

					// Set the values for the exported Prometheus metrics
					TotalBytes.Set( memoryInformation[ "MemTotal" ] );
					FreeBytes.Set( memoryInformation[ "MemFree" ] + memoryInformation[ "Cached" ] + memoryInformation[ "Buffers" ] ); // Ignore cached & buffered memory as its instantly reclaimable - https://stackoverflow.com/a/41251290
					SwapTotalBytes.Set( memoryInformation[ "SwapTotal" ] );
					SwapFreeBytes.Set( memoryInformation[ "SwapFree" ] );
					logger.LogDebug( "Updated Prometheus metrics" );

				}
			}
		}

		// C++ Windows API structure for GlobalMemoryStatusEx() - https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex
		[ SupportedOSPlatform( "windows" ) ]
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private class MEMORYSTATUSEX {
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 dwLength; // DWORD
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 dwMemoryLoad; // DWORD
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullTotalPhys; // DWORDLONG
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullAvailPhys; // DWORDLONG
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullTotalPageFile; // DWORDLONG
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullAvailPageFile; // DWORDLONG
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullTotalVirtual; // DWORDLONG
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullAvailVirtual; // DWORDLONG
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 ullAvailExtendedVirtual; // DWORDLONG
		}

		// C++ Windows API structure for GetPerformanceInfo() - https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information
		[ SupportedOSPlatform( "windows" ) ]
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private class PERFORMANCE_INFORMATION {
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 cb; // DWORD
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 CommitTotal; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 CommitLimit; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 CommitPeak; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 PhysicalTotal; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 PhysicalAvailable; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 SystemCache; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 KernelTotal; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 KernelPaged; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 KernelNonpaged; // SIZE_T
			[ MarshalAs( UnmanagedType.U8 ) ] public UInt64 PageSize; // SIZE_T
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 HandleCount; // DWORD
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 ProcessCount; // DWORD
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 ThreadCount; // DWORD
		}

		// C++ Windows API function to get information about system memory - https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex
		// The PerformanceCounter class is not good enough, it does not return enough detail...
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ SupportedOSPlatform( "windows" ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool GlobalMemoryStatusEx(
			[ In, Out ] MEMORYSTATUSEX lpBuffer // LPMEMORYSTATUSEX
		);

		// The C++ Windows API function to get information about system performance - https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getperformanceinfo
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ SupportedOSPlatform( "windows" ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool K32GetPerformanceInfo(
			[ Out ] PERFORMANCE_INFORMATION pPerformanceInformation,
			[ In ] UInt32 cb // DWORD
		);

	}

}
