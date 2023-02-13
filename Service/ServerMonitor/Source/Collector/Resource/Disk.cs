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
		public readonly Counter ReadBytes;
		public readonly Counter WriteBytes;
		public readonly Gauge Health;

		// Initialise the exported Prometheus metrics
		public Disk( Config configuration ) {
			TotalBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_total_bytes", "Total disk space, in bytes.", new GaugeConfiguration {
				LabelNames = new[] { "partition", "mountpoint" }
			} );
			FreeBytes = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_free_bytes", "Free disk space, in bytes.", new GaugeConfiguration {
				LabelNames = new[] { "partition", "mountpoint" }
			} );
			ReadBytes = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_read_bytes", "Total read, in bytes.", new CounterConfiguration {
				LabelNames = new[] { "drive" }
			} );
			WriteBytes = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_write_bytes", "Total written, in bytes.", new CounterConfiguration {
				LabelNames = new[] { "drive" }
			} );
			Health = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_resource_disk_health", "S.M.A.R.T disk health", new GaugeConfiguration {
				LabelNames = new[] { "drive" }
			} );

			TotalBytes.Set( 0 );
			FreeBytes.Set( 0 );
			ReadBytes.IncTo( 0 );
			WriteBytes.IncTo( 0 );
			Health.Set( -1 ); // -1 if not supported

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

		// Updates the exported Prometheus metrics (for Linux)
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Loop through each drive...
			foreach ( string driveName in GetDrives() ) {

				// Update the read & write metrics for the drive
				long[] driveStatistics = GetDriveStatistics( driveName );
				ReadBytes.WithLabels( driveName ).IncTo( driveStatistics[ 0 ] );
				WriteBytes.WithLabels( driveName ).IncTo( driveStatistics[ 1 ] );

				// Loop through each partition on the drive...
				foreach ( string partitionName in GetPartitions( driveName ) ) {

					// Get the device path for the partition
					string? mappedName = GetMappedName( partitionName );
					logger.LogDebug( "Partition {0}: {1}", partitionName, mappedName );
					string partitionPath = mappedName != null ? Path.Combine( "/dev/mapper", mappedName ) : Path.Combine( "/dev", partitionName );
					logger.LogDebug( "Partition {0}: {1}", partitionName, partitionPath );

					// Get the mount path for the partition, skip if not mounted
					string? mountPath = GetMountPath( partitionPath );
					logger.LogDebug( "Partition {0}: {1}", partitionName, mountPath );
					if ( mountPath == null ) continue;

					logger.LogDebug( "Partition: {0}, {1}, {2}, {3}", partitionName, mappedName, partitionPath, mountPath );

					Gauge.Child totalBytes = TotalBytes.WithLabels( partitionName, mountPath );
					Gauge.Child freeBytes = FreeBytes.WithLabels( partitionName, mountPath );
					logger.LogDebug( "Total: {0}, Free: {0}", totalBytes.Value, freeBytes.Value );

					// Get filesystem statistics for the partition
					ulong[] filesystemStatistics = GetFilesystemStatistics( partitionPath );
					logger.LogDebug( "Partition: {0}, {1}, {2}, {3}, {4}, {5}", partitionName, mappedName, partitionPath, mountPath, filesystemStatistics[ 0 ], filesystemStatistics[ 1 ] );

					// Update the metrics for the partition
					totalBytes.Set( filesystemStatistics[ 0 ] );
					freeBytes.Set( filesystemStatistics[ 1 ] );

				}

				// TODO: S.M.A.R.T health
				Health.Set( -1 );

			}

		}

		// Gets a list of drives (for Linux)
		private string[] GetDrives() => Directory.GetDirectories( "/sys/block/" )
			.Where( drivePath => Regex.IsMatch( Path.GetFileName( drivePath ), @"^sd[a-z]+$|^nvme\d+n\d+$" ) ) // Name must be a regular or NVMe drive
			.Where( drivePath => File.Exists( Path.Combine( drivePath, "stat" ) ) ) // Must have I/O statistics
			.Where( drivePath => File.ReadAllLines( Path.Combine( drivePath, "removable" ) )[ 0 ] == "0" ) // Must not be removable
			.Select( drivePath => Path.GetFileName( drivePath ) ) // Only return the drive name
			.ToArray();

		// Get read & write statistics for a drive (on Linux) - https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats, https://unix.stackexchange.com/a/111993
		private long[] GetDriveStatistics( string driveName ) => File.ReadAllLines( Path.Combine( "/sys/block", driveName, "stat" ) )
			.Select( line => line.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split each line into parts
			.Select( parts => parts.Select( part => long.Parse( part ) ).ToArray() ) // Convert each part to a number
			.Select( parts => new long[] { parts[ 2 ] * 512, parts[ 6 ] * 512 } ) // Only return total read & written, as bytes by multiplying sector count by 512
			.First();

		// Gets a list of partitions for a drive (on Linux)
		private string[] GetPartitions( string driveName ) => Directory.GetDirectories( Path.Combine( "/sys/block", driveName ) ) // List pseudo-directory containing block devices
			.Where( partitionPath => Regex.IsMatch( Path.GetFileName( partitionPath ), @"^sd[a-z]\d+$|^nvme\d+n\d+p\d+$" ) ) // Name must be a regular or NVMe partition
			.Where( partitionPath => File.Exists( Path.Combine( partitionPath, "partition" ) ) ) // Must be a partition
			.Where( partitionPath => int.Parse( File.ReadAllLines( Path.Combine( partitionPath, "partition" ) )[ 0 ] ) > 0 ) // Must have a partition number
			.Select( drivePath => Path.GetFileName( drivePath ) ) // Only return the partition name
			.ToArray();

		// Gets the mapped device name for a partition, if LUKS encrypted (on Linux)
		private string? GetMappedName( string partitionName ) => Directory.GetDirectories( Path.Combine( "/sys/class/block", partitionName, "holders" ) ) // List pseudo-directory containing holder symlinks
			.Where( holderPath => Directory.Exists( Path.Combine( holderPath, "slaves", partitionName ) ) ) // Must be a slave to this partition
			.Where( holderPath => Regex.IsMatch( Path.GetFileName( holderPath ), @"^dm-\d+$" ) ) // Name must be a device mapper
			.Where( holderPath => Directory.Exists( Path.Combine( holderPath, "dm" ) ) ) // Must be a device mapper
			.Where( holderPath => File.Exists( Path.Combine( holderPath, "dm", "name" ) ) ) // Must have a name
			.Where( hodlerPath => File.Exists( Path.Combine( "/dev/mapper", File.ReadAllLines( Path.Combine( hodlerPath, "dm", "name" ) )[ 0 ] ) ) ) // Must be mapped
			.Select( holderPath => File.ReadAllLines( Path.Combine( holderPath, "dm", "name" ) )[ 0 ] ) // Only return the mapped name
			.FirstOrDefault(); // Return the first item, or null if none

		// Gets the mount path for a partition, if mounted (on Linux) - https://linux.die.net/man/5/proc
		private string? GetMountPath( string partitionPath ) => File.ReadAllLines( "/proc/mounts" ) // Read pseudo-file containing mount information
			.Select( line => line.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split each line into parts
			.Where( parts => parts[ 0 ] == partitionPath ) // Only keep lines related to this partition
			.Select( parts => parts[ 1 ] ) // Only return the mount path
			.FirstOrDefault(); // Return the first item, or null if none

		// https://stackoverflow.com/questions/28950380/a-call-to-pinvoke-function-has-unbalanced-the-stack-in-debug-mode
		// https://stackoverflow.com/questions/69581944/c-sharp-p-invoke-corrupt-memory

		// Linux POSIX C structure for statvfs() - https://www.man7.org/linux/man-pages/man3/statvfs.3.html
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private struct statvfs_struct {
			public ulong f_bsize;
			public ulong f_frsize;
			public uint f_blocks;
			public uint f_bfree;
			public uint f_bavail;
			public uint f_files;
			public uint f_ffree;
			public uint f_favail;
			public ulong f_fsid;
			public ulong f_flag;
			public ulong f_namemax;
		}

		// Linux POSIX C function to get information about a filesystem (on Linux) - https://www.man7.org/linux/man-pages/man3/statvfs.3.html
		[ return: MarshalAs( UnmanagedType.I4 ) ]
		[ DllImport( "libc", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.Cdecl ) ]
		private static extern int statvfs( string path, out statvfs_struct buf );

		// Gets the total & free bytes for a filesystem (on Linux)
		// NOTE: This has to be done in its own function because calling statvfs() seems to corrupt stack memory? I'm probably using it wrong...
		private ulong[] GetFilesystemStatistics( string mountPath ) {
			logger.LogTrace( "a" );
			statvfs_struct statvfs_struct = new();
			logger.LogTrace( "b" );
			if ( statvfs( mountPath, out statvfs_struct ) != 0 ) throw new Exception( "Failed to call Linux POSIX C function statvfs()" );
			logger.LogTrace( "c" );
			return new ulong[] {
				statvfs_struct.f_blocks * statvfs_struct.f_bsize, // Multiply by block size to get bytes
				statvfs_struct.f_bavail * statvfs_struct.f_bsize
			};
		}

	}

}
