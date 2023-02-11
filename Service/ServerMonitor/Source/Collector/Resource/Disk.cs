using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system disk metrics
	public class Disk : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Disk" );

		// Holds the exported Prometheus metrics
		public readonly Gauge TotalBytes;
		public readonly Gauge FreeBytes;
		public readonly Gauge Health;
		public readonly Gauge WriteBytesPerSecond;
		public readonly Gauge ReadBytesPerSecond;

		// Initialise the exported Prometheus metrics
		public Disk( Config configuration ) {
			string[] labelNames = new[] { "label", "filesystem", "mountpoint" };

			TotalBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_total_bytes", "Total disk space, in bytes.", new GaugeConfiguration { LabelNames = labelNames } );
			FreeBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_free_bytes", "Free disk space, in bytes.", new GaugeConfiguration { LabelNames = labelNames } );
			Health = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_health", "S.M.A.R.T disk health", new GaugeConfiguration { LabelNames = labelNames } );
			WriteBytesPerSecond = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_current_write_speed_bytes", "Current write speed, in bytes per second.", new GaugeConfiguration { LabelNames = labelNames } );
			ReadBytesPerSecond = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_current_read_speed_bytes", "Current read speed, in bytes per second.", new GaugeConfiguration { LabelNames = labelNames } );

			TotalBytes.Set( 0 );
			FreeBytes.Set( 0 );
			Health.Set( -1 ); // -1 if not supported
			WriteBytesPerSecond.Set( 0 );
			ReadBytesPerSecond.Set( 0 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the metrics for Windows & Linux...
		// NOTE: This functionality is natively cross-platform as we're only using .NET Core APIs
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Get the relevant drive information - https://learn.microsoft.com/en-us/dotnet/api/system.io.driveinfo.availablefreespace?view=net-7.0#examples
			DriveInfo[] driveInformation = DriveInfo.GetDrives()
				.Where( driveInfo => driveInfo.DriveType == DriveType.Fixed ) // Skip network shares, ramfs, etc.
				.Where( driveInfo => driveInfo.IsReady == true ) // Skip unmounted drives
				.Where( driveInfo => // Skip WSL & Docker filesystems
					driveInfo.DriveFormat != "9P" && driveInfo.DriveFormat != "v9fs" &&
					driveInfo.DriveFormat != "overlay"
				)
				.Where( driveInfo => // Skip psuedo file systems
					!driveInfo.RootDirectory.FullName.StartsWith( "/sys" ) &&
					!driveInfo.RootDirectory.FullName.StartsWith( "/proc" ) &&
					!driveInfo.RootDirectory.FullName.StartsWith( "/dev" ) &&
					driveInfo.TotalSize != 0
				).ToArray();

			// Update the metrics for each drive
			foreach ( DriveInfo driveInfo in driveInformation ) {
				string driveLabel = driveInfo.VolumeLabel;
				string driveFileSystem = driveInfo.DriveFormat;
				string driveMountpoint = driveInfo.RootDirectory.FullName;

				TotalBytes.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( driveInfo.TotalSize );
				FreeBytes.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( driveInfo.TotalFreeSpace );

				// TODO - https://stackoverflow.com/questions/36977903/how-can-we-get-disk-performance-info-in-c-sharp
				Health.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( -1 );
				WriteBytesPerSecond.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( 0 );
				ReadBytesPerSecond.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( 0 );
			}

		}

		// Structure of the /proc/diskstats file - https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats
		struct DiskStats {
			public string DeviceName;
			public int ReadsCompleted;
			public int ReadsMerged;
			public int SectorsRead;
			public int MillisecondsSpentReading;
			public int WritesCompleted;
			public int WritesMerged;
			public int SectorsWritten;
			public int MillisecondsSpentWriting;
			public int IOsInProgress;
			public int MillisecondsSpentDoingIO;
			public int WeightedMillisecondsSpentDoingIO;
			public int DisardsCompleted;
			public int DisardsMerged;
			public int SectorsDisarded;
			public int MillisecondsSpentDisarding;
			public int FlushRequestsCompleted;
			public int MillisecondsSpentFlushing;
		}

		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );
			
			DiskStats[] diskStatistics = GetDiskStatistics();
			foreach ( DiskStats diskStats in diskStatistics ) {
				string mountpoint = GetDiskMountpoint( diskStats.DeviceName );
				logger.LogDebug( $"Disk { diskStats.DeviceName } mounted at { mountpoint }" );
			}
		}

		// Get the mountpoint for a given disk on Linux
		private string GetDiskMountpoint( string deviceName ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing information on mounted disks - https://linux.die.net/man/5/proc
			using ( FileStream fileStream = new( "/proc/mounts", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Read each line until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 2 ) throw new Exception( "Mount information file contains an invalid line (too few parts)" );
						string devicePath = lineParts[ 0 ];
						string mountpoint = lineParts[ 1 ];

						// Return the mountpoint if this is the device we're looking for
						if ( devicePath == $"/dev/{ deviceName }" ) return mountpoint;

					} while ( !streamReader.EndOfStream );

				}
			}

			throw new Exception( $"Unable to find mountpoint for device { deviceName }" );
		}

		// Gets the current disk statistics on Linux
		private DiskStats[] GetDiskStatistics() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing disk I/O statistics - https://linux.die.net/man/5/proc
			using ( FileStream fileStream = new( "/proc/diskstats", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// List to hold the statistics for each disk
					List<DiskStats> diskStatistics = new();

					// Read each line until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 20 ) throw new Exception( "Disk statistics file contains an invalid line (too few parts)" );

						// Create the disk statistics structure with the device name
						DiskStats diskStats;
						diskStats.DeviceName = lineParts[ 2 ];

						// Skip non-disk devices
						if ( !diskStats.DeviceName.StartsWith( "sda" ) && !diskStats.DeviceName.StartsWith( "nvme" ) ) continue;

						// Parse all the individual statistics - https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats
						if ( int.TryParse( lineParts[ 3 ], out diskStats.ReadsCompleted ) != true ) throw new Exception( "Failed to parse disk reads completed as an integer" );
						if ( int.TryParse( lineParts[ 4 ], out diskStats.ReadsMerged ) != true ) throw new Exception( "Failed to parse disk reads merged as an integer" );
						if ( int.TryParse( lineParts[ 5 ], out diskStats.SectorsRead ) != true ) throw new Exception( "Failed to parse disk sectors read as an integer" );
						if ( int.TryParse( lineParts[ 6 ], out diskStats.MillisecondsSpentReading ) != true ) throw new Exception( "Failed to parse disk time spent reading as an integer" );
						if ( int.TryParse( lineParts[ 7 ], out diskStats.WritesCompleted ) != true ) throw new Exception( "Failed to parse disk writes completed as an integer" );
						if ( int.TryParse( lineParts[ 8 ], out diskStats.WritesMerged ) != true ) throw new Exception( "Failed to parse disk writes merged as an integer" );
						if ( int.TryParse( lineParts[ 9 ], out diskStats.SectorsWritten ) != true ) throw new Exception( "Failed to parse disk sectors written as an integer" );
						if ( int.TryParse( lineParts[ 10 ], out diskStats.MillisecondsSpentWriting ) != true ) throw new Exception( "Failed to parse disk time spent writing as an integer" );
						if ( int.TryParse( lineParts[ 11 ], out diskStats.IOsInProgress ) != true ) throw new Exception( "Failed to parse disk I/Os in progress as an integer" );
						if ( int.TryParse( lineParts[ 12 ], out diskStats.MillisecondsSpentDoingIO ) != true ) throw new Exception( "Failed to parse disk time spent doing I/O as an integer" );
						if ( int.TryParse( lineParts[ 13 ], out diskStats.WeightedMillisecondsSpentDoingIO ) != true ) throw new Exception( "Failed to parse disk weighted time spent doing I/O as an integer" );
						if ( int.TryParse( lineParts[ 14 ], out diskStats.DisardsCompleted ) != true ) throw new Exception( "Failed to parse disk discards completed as an integer" );
						if ( int.TryParse( lineParts[ 15 ], out diskStats.DisardsMerged ) != true ) throw new Exception( "Failed to parse disk discards merged as an integer" );
						if ( int.TryParse( lineParts[ 16 ], out diskStats.SectorsDisarded ) != true ) throw new Exception( "Failed to parse disk sectors discarded as an integer" );
						if ( int.TryParse( lineParts[ 17 ], out diskStats.MillisecondsSpentDisarding ) != true ) throw new Exception( "Failed to parse disk time spent discarding as an integer" );
						if ( int.TryParse( lineParts[ 18 ], out diskStats.FlushRequestsCompleted ) != true ) throw new Exception( "Failed to parse disk flush requests completed as an integer" );
						if ( int.TryParse( lineParts[ 19 ], out diskStats.MillisecondsSpentFlushing ) != true ) throw new Exception( "Failed to parse disk time spent flushing as an integer" );

						// Add this disk's statistics to the list
						diskStatistics.Add( diskStats );

					} while ( !streamReader.EndOfStream );

					// We're done, return the statistics of each disk as an array
					return diskStatistics.ToArray();

				}
			}

			// If we get here, there were no disk statistics to read
			throw new Exception( "Failed to find any disks" );
		}

	}
}
