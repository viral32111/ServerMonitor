using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Management;
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
			DriveInfo[] drives = DriveInfo.GetDrives()
				.Where( driveInfo => driveInfo.DriveType == DriveType.Fixed ) // Only internal drives (no network shares, swap, etc.)
				.Where( driveInfo => driveInfo.IsReady == true ) // Skip unmounted drives
				.Where( driveInfo => // Skip WSL & Docker filesystems
					driveInfo.DriveFormat != "9P" &&
					driveInfo.DriveFormat != "v9fs" &&
					driveInfo.DriveFormat != "drivefs" &&
					driveInfo.DriveFormat != "overlay"
				)
				.Where( driveInfo => // Skip pseudo filesystems
					!driveInfo.RootDirectory.FullName.StartsWith( "/sys" ) &&
					!driveInfo.RootDirectory.FullName.StartsWith( "/proc" ) &&
					!driveInfo.RootDirectory.FullName.StartsWith( "/dev" ) &&
					driveInfo.TotalSize != 0
				)
				.ToArray();

			// Update the metrics for each drive
			foreach ( DriveInfo driveInformation in drives ) {
				SMART( driveInformation );




				/*string driveLabel = driveInfo.VolumeLabel;
				string driveFileSystem = driveInfo.DriveFormat;
				string driveMountpoint = driveInfo.RootDirectory.FullName;

				TotalBytes.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( driveInfo.TotalSize );
				FreeBytes.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( driveInfo.TotalFreeSpace );

				// TODO - https://stackoverflow.com/questions/36977903/how-can-we-get-disk-performance-info-in-c-sharp
				Health.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( -1 );
				WriteBytesPerSecond.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( 0 );
				ReadBytesPerSecond.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( 0 );*/
			}

		}

		private void SMART( DriveInfo driveInformation ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			/*foreach ( ManagementObject managementObject in new ManagementObjectSearcher( "SELECT * FROM Win32_DiskDrive" ).Get() ) {
				logger.LogDebug( "Name: '{0}'", managementObject[ "Name" ] );
				logger.LogDebug( "System Name: '{0}'", managementObject[ "SystemName" ] );
				logger.LogDebug( "Model: '{0}'", managementObject[ "Model" ] );
				logger.LogDebug( "Manufacturer: '{0}'", managementObject[ "Manufacturer" ] );
				logger.LogDebug( "Needs Cleaning: '{0}'", managementObject[ "NeedsCleaning" ] );
				logger.LogDebug( "Install Date: '{0}'", managementObject[ "InstallDate" ] );
				logger.LogDebug( "DeviceID: '{0}'", managementObject[ "DeviceID" ] );
				logger.LogDebug( "Description: '{0}'", managementObject[ "Description" ] );
				logger.LogDebug( "Status: '{0}'", managementObject[ "Status" ] );
				logger.LogDebug( "StatusInfo: '{0}'", managementObject[ "StatusInfo" ] );
				logger.LogDebug( "MediaType: '{0}'", managementObject[ "MediaType" ] );
				logger.LogDebug( "SerialNumber: '{0}'", managementObject[ "SerialNumber" ] );
				logger.LogDebug( "Size: '{0}'", managementObject[ "Size" ] );
				logger.LogDebug( "TotalSectors: {0}*512 = {1}", managementObject[ "TotalSectors" ], long.Parse( managementObject[ "TotalSectors" ].ToString() ?? "0" ) * 512 );
			}

			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( "SELECT * FROM Win32_PhysicalMemory" ).Get() ) {
				logger.LogDebug( "Name: '{0}'", managementObject[ "Name" ] );
				logger.LogDebug( "Model: '{0}'", managementObject[ "Model" ] );
				logger.LogDebug( "Manufacturer: '{0}'", managementObject[ "Manufacturer" ] );
				logger.LogDebug( "SerialNumber: '{0}'", managementObject[ "SerialNumber" ] );
				logger.LogDebug( "Capacity: '{0}'", managementObject[ "Capacity" ] );
				logger.LogDebug( "Speed: '{0}'", managementObject[ "Speed" ] );
				logger.LogDebug( "Status: '{0}'", managementObject[ "Status" ] );
			}*/

			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( @"root\WMI", "SELECT * FROM MSStorageDriver_ATAPISmartData" ).Get() ) {
				logger.LogDebug( "Active: '{0}'", managementObject[ "Active" ] );
				logger.LogDebug( "SelfTestStatus: '{0}'", managementObject[ "SelfTestStatus" ] );
				logger.LogDebug( "Checksum: '{0}'", managementObject[ "Checksum" ] );
				logger.LogDebug( "Length: '{0}'", managementObject[ "Length" ] );
				logger.LogDebug( "InstanceName: '{0}'", managementObject[ "InstanceName" ] );
				logger.LogDebug( "TotalTime: '{0}'", managementObject[ "TotalTime" ] );
				logger.LogDebug( "VendorSpecific: '{0}'", managementObject[ "VendorSpecific" ] );
			}
		}

		// TODO: PInvoke for statvfs() on Linux? - https://developers.redhat.com/blog/2019/03/25/using-net-pinvoke-for-linux-system-functions#pinvoking_linux
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			DriveInfo[] drives = DriveInfo.GetDrives()
				.Where( driveInfo => driveInfo.DriveType == DriveType.Fixed ) // Only internal drives (no network shares, swap, etc.)
				.Where( driveInfo => driveInfo.IsReady == true ) // Skip unmounted drives
				.Where( driveInfo => // Skip WSL & Docker filesystems
					driveInfo.DriveFormat != "9P" &&
					driveInfo.DriveFormat != "v9fs" &&
					driveInfo.DriveFormat != "drivefs" &&
					driveInfo.DriveFormat != "overlay"
				)
				.Where( driveInfo => // Skip pseudo filesystems
					!driveInfo.RootDirectory.FullName.StartsWith( "/sys" ) &&
					!driveInfo.RootDirectory.FullName.StartsWith( "/proc" ) &&
					!driveInfo.RootDirectory.FullName.StartsWith( "/dev" ) &&
					driveInfo.TotalSize != 0
				)
				.ToArray();

			foreach ( DriveInfo driveInformation in drives ) {
				MountInfo mountInformation = GetMountInformation( driveInformation.RootDirectory.FullName );
				string deviceName = Path.GetFileNameWithoutExtension( mountInformation.DevicePath );
				string deviceMountPath = driveInformation.RootDirectory.FullName;
				string deviceFileSystem = driveInformation.DriveFormat;
				long totalBytes = driveInformation.TotalSize;
				long freeBytes = driveInformation.TotalFreeSpace;
				logger.LogDebug( "{0} ({1}, {2}): {3}, {4}", deviceName, deviceMountPath, deviceFileSystem, totalBytes, freeBytes );

				PartitionStats partitionStatistics = GetPartitionStatistics( deviceName );
				logger.LogDebug( "\t{0}, {1}, {2}, {3}", partitionStatistics.ReadsCompleted, partitionStatistics.WritesCompleted, partitionStatistics.SectorsRead, partitionStatistics.SectorsWritten );
			}

			/*Partition[] partitions = GetPartitions();
			logger.LogDebug( $"Found { partitions.Length } partitions" );
			foreach ( Partition partition in partitions ) {
				logger.LogDebug( $"Disk { partitionStatistics.Name }, partition { partitionStatistics.Name }, mountpoint { partitionStatistics.Mountpoint }" );
			}*/
		}

		struct MountInfo {
			public string DevicePath;
			public string MountPath;
			public string FileSystem;
			public string Options;
			public int Dump;
			public int Pass;
		}

		private MountInfo GetMountInformation( string mountPath ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing information on mounted filesystems - https://linux.die.net/man/5/proc
			using ( FileStream fileStream = new( "/proc/mounts", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Read each line until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 6 ) throw new Exception( "Mount information file contains an invalid line (too few parts)" );

						// Create a structure with initial mount information
						MountInfo mountInformation = new() {
							DevicePath = lineParts[ 0 ],
							MountPath = lineParts[ 1 ],
							FileSystem = lineParts[ 2 ],
							Options = lineParts[ 3 ]
						};

						// Parse the dump & pass values as integers
						if ( !int.TryParse( lineParts[ 4 ], out mountInformation.Dump ) ) throw new Exception( "Failed to parse mount information dump value as an integer" );
						if ( !int.TryParse( lineParts[ 5 ], out mountInformation.Pass ) ) throw new Exception( "Failed to parse mount information pass value as an integer" );

						// Return if this is the mount we're looking for
						if ( mountInformation.MountPath == mountPath ) return mountInformation;

					} while ( !streamReader.EndOfStream );

				}
			}

			throw new Exception( $"Unable to find information about mount" );
		}

		// Get the mountpoint for a given disk's partition on Linux
		/*private string GetPartitionMountpoint( string partitionName ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing information on mounted partitions - https://linux.die.net/man/5/proc
			using ( FileStream fileStream = new( "/proc/mounts", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Read each line until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 2 ) throw new Exception( "Mount information file contains an invalid line (too few parts)" );
						string path = lineParts[ 0 ];
						string mountpoint = lineParts[ 1 ];

						// Return the mountpoint if this is the device we're looking for
						if ( path == $"/dev/{ partitionName }" ) return mountpoint;

					} while ( !streamReader.EndOfStream );

				}
			}

			throw new Exception( $"Unable to find mountpoint for partition { partitionName }" );
		}*/

		// Get a list of partitions for a given disk on Linux
		/*private PartitionInfo[] GetDiskPartitions( string diskName ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing information on disk partitions - https://linux.die.net/man/5/proc
			using ( FileStream fileStream = new( "/proc/partitions", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Will hold the information for each disk partition
					List<PartitionInfo> partitions = new();

					// Read each line until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 4 ) throw new Exception( "Partition information file contains an invalid line (too few parts)" );

						// Create a structure with the name
						PartitionInfo partitionInfo;
						partitionInfo.Name = lineParts[ 3 ];

						// Skip partitions that aren't on this disk
						if ( partitionInfo.Name.StartsWith( diskName ) == false ) continue;

						// Parse & convert number of blocks into bytes
						if ( int.TryParse( lineParts[ 2 ], out int blockCount ) != true ) throw new Exception( "Failed to parse partition block count as integer" );
						partitionInfo.TotalBytes = blockCount * 512;

						
						// Add it to the list
						partitions.Add( partitionInfo );

					} while ( !streamReader.EndOfStream );

					// Convert to a fixed array before returning
					return partitions.ToArray();

				}
			}

			throw new Exception( "Unable to find partitions for disk" );
		}*/

		// Gets the statistics of a disk's partition on Linux
		private PartitionStats GetPartitionStatistics( string partitionName ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Read the pseudo-file containing disk I/O statistics - https://linux.die.net/man/5/proc
			using ( FileStream fileStream = new( "/proc/diskstats", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {

					// Read each line until we reach the end...
					do {
						string? fileLine = streamReader.ReadLine();
						if ( string.IsNullOrWhiteSpace( fileLine ) ) break;

						// Split into individual parts
						string[] lineParts = fileLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
						if ( lineParts.Length < 20 ) throw new Exception( "Disk statistics file contains an invalid line (too few parts)" );

						// Parse all the individual statistics into a structure - https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats
						PartitionStats partitionStatistics = new();
						if ( int.TryParse( lineParts[ 3 ], out partitionStatistics.ReadsCompleted ) != true ) throw new Exception( "Failed to parse reads completed as an integer" );
						if ( int.TryParse( lineParts[ 4 ], out partitionStatistics.ReadsMerged ) != true ) throw new Exception( "Failed to parse reads merged as an integer" );
						if ( int.TryParse( lineParts[ 5 ], out partitionStatistics.SectorsRead ) != true ) throw new Exception( "Failed to parse sectors read as an integer" );
						if ( int.TryParse( lineParts[ 6 ], out partitionStatistics.MillisecondsSpentReading ) != true ) throw new Exception( "Failed to parse time spent reading as an integer" );
						if ( int.TryParse( lineParts[ 7 ], out partitionStatistics.WritesCompleted ) != true ) throw new Exception( "Failed to parse writes completed as an integer" );
						if ( int.TryParse( lineParts[ 8 ], out partitionStatistics.WritesMerged ) != true ) throw new Exception( "Failed to parse writes merged as an integer" );
						if ( int.TryParse( lineParts[ 9 ], out partitionStatistics.SectorsWritten ) != true ) throw new Exception( "Failed to parse sectors written as an integer" );
						if ( int.TryParse( lineParts[ 10 ], out partitionStatistics.MillisecondsSpentWriting ) != true ) throw new Exception( "Failed to parse time spent writing as an integer" );
						if ( int.TryParse( lineParts[ 11 ], out partitionStatistics.IOsInProgress ) != true ) throw new Exception( "Failed to parse I/Os in progress as an integer" );
						if ( int.TryParse( lineParts[ 12 ], out partitionStatistics.MillisecondsSpentDoingIO ) != true ) throw new Exception( "Failed to parse time spent doing I/O as an integer" );
						if ( int.TryParse( lineParts[ 13 ], out partitionStatistics.WeightedMillisecondsSpentDoingIO ) != true ) throw new Exception( "Failed to parse weighted time spent doing I/O as an integer" );
						if ( int.TryParse( lineParts[ 14 ], out partitionStatistics.DisardsCompleted ) != true ) throw new Exception( "Failed to parse discards completed as an integer" );
						if ( int.TryParse( lineParts[ 15 ], out partitionStatistics.DisardsMerged ) != true ) throw new Exception( "Failed to parse discards merged as an integer" );
						if ( int.TryParse( lineParts[ 16 ], out partitionStatistics.SectorsDisarded ) != true ) throw new Exception( "Failed to parse sectors discarded as an integer" );
						if ( int.TryParse( lineParts[ 17 ], out partitionStatistics.MillisecondsSpentDisarding ) != true ) throw new Exception( "Failed to disk time spent discarding as an integer" );
						if ( int.TryParse( lineParts[ 18 ], out partitionStatistics.FlushRequestsCompleted ) != true ) throw new Exception( "Failed to parse flush requests completed as an integer" );
						if ( int.TryParse( lineParts[ 19 ], out partitionStatistics.MillisecondsSpentFlushing ) != true ) throw new Exception( "Failed to parse time spent flushing as an integer" );

						// Return if this is the partition we're looking for
						if ( lineParts[ 2 ] == partitionName ) return partitionStatistics;

					} while ( !streamReader.EndOfStream );

				}
			}

			// If we get here, the partition wasn't found
			throw new Exception( "Failed to find any statistics for partition" );
		}

		// The structure of the /proc/diskstats file - https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats
		struct PartitionStats {
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

	}
}
