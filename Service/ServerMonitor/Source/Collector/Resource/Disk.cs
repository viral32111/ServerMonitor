using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Prometheus;
using Mono.Unix.Native;
using Microsoft.Win32.SafeHandles;

/*
using System.Text;
using System.Management;
using System.Collections.Generic;
*/

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

		// Updates the exported Prometheus metrics (for Windows)
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// Get information about drives - https://learn.microsoft.com/en-us/dotnet/api/system.io.driveinfo.availablefreespace?view=net-7.0#examples
			DriveInfo[] drives = DriveInfo.GetDrives()
				.Where( driveInfo => driveInfo.DriveType == DriveType.Fixed ) // Skip network shares, etc.
				.Where( driveInfo => driveInfo.IsReady == true ) // Skip unmounted drives
				.Where( driveInfo => driveInfo.TotalSize != 0 ) // Skip pseudo filesystems
				.Where( driveInfo => // Skip WSL & Docker filesystems
					driveInfo.DriveFormat != "9P" &&
					driveInfo.DriveFormat != "v9fs" &&
					driveInfo.DriveFormat != "drivefs" &&
					driveInfo.DriveFormat != "overlay"
				).ToArray();

			// Loop through each drive...
			foreach ( DriveInfo driveInformation in drives ) {
				string driveName = driveInformation.VolumeLabel;
				string driveMountPath = driveInformation.RootDirectory.FullName;

				// Set the values for the exported Prometheus metrics
				TotalBytes.WithLabels( driveName, driveMountPath ).Set( driveInformation.TotalSize );
				FreeBytes.WithLabels( driveName, driveMountPath ).Set( driveInformation.TotalFreeSpace );

				// TODO: Total bytes read & written since system startup - https://stackoverflow.com/questions/36977903/how-can-we-get-disk-performance-info-in-c-sharp, https://stackoverflow.com/a/30451751
				ReadBytes.WithLabels( driveName ).IncTo( 0 );
				WriteBytes.WithLabels( driveName ).IncTo( 0 );

				// TODO: S.M.A.R.T health
				Health.WithLabels( driveName ).Set( -1 );

				logger.LogDebug( "Updated Prometheus metrics" );
			}

		}





		// https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-disk_performance
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private struct DISK_PERFORMANCE {
			long BytesRead;
			long BytesWritten;
			long ReadTime;
			long WriteTime;
			long IdleTime;
			int ReadCount;
			int WriteCount;
			int QueueDepth;
			int SplitCount;
			long QueryTime;
			int StorageDeviceNumber;
			char[] StorageManagerName;
		}

		// https://stackoverflow.com/a/17354960, https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool DeviceIoControl(
			[ In ] SafeFileHandle hDevice,
			[ In ] int dwIoControlCode,
			[ In, Optional ] byte[] lpInBuffer,
			[ In ] int nInBufferSize,
			[ Out, Optional ] DISK_PERFORMANCE lpOutBuffer,
			[ In ] int nOutBufferSize,
			[ Out, Optional ] out int lpBytesReturned,
			[ In, Out, Optional ] IntPtr lpOverlapped
		);




		/*private void SMART( DriveInfo driveInformation ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( "SELECT * FROM Win32_DiskDrive" ).Get() ) {
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
			}

			foreach ( ManagementObject managementObject in new ManagementObjectSearcher( @"root\WMI", "SELECT * FROM MSStorageDriver_ATAPISmartData" ).Get() ) {
				logger.LogDebug( "Active: '{0}'", managementObject[ "Active" ] );
				logger.LogDebug( "SelfTestStatus: '{0}'", managementObject[ "SelfTestStatus" ] );
				logger.LogDebug( "Checksum: '{0}'", managementObject[ "Checksum" ] );
				logger.LogDebug( "Length: '{0}'", managementObject[ "Length" ] );
				logger.LogDebug( "InstanceName: '{0}'", managementObject[ "InstanceName" ] );
				logger.LogDebug( "TotalTime: '{0}'", managementObject[ "TotalTime" ] );
				logger.LogDebug( "VendorSpecific: '{0}'", managementObject[ "VendorSpecific" ] );
			}
		}*/

		// Updates the exported Prometheus metrics (for Linux)
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// Loop through each drive...
			foreach ( string driveName in GetDrives() ) {

				// Set the values for the exported Prometheus metrics
				long[] driveStatistics = GetDriveStatistics( driveName );
				ReadBytes.WithLabels( driveName ).IncTo( driveStatistics[ 0 ] );
				WriteBytes.WithLabels( driveName ).IncTo( driveStatistics[ 1 ] );

				// Loop through each partition on the drive...
				foreach ( string partitionName in GetPartitions( driveName ) ) {

					// Get the device path for the partition
					string? mappedName = GetMappedName( partitionName );
					string partitionPath = mappedName != null ? Path.Combine( "/dev/mapper", mappedName ) : Path.Combine( "/dev", partitionName );

					// Get the mount path for the partition, skip if not mounted
					string? mountPath = GetMountPath( partitionPath );
					if ( mountPath == null ) continue;

					// Get filesystem statistics for the partition using the Linux C statvfs() function
					Statvfs filesystemStatistics = new();
					int status = Syscall.statvfs( mountPath, out filesystemStatistics );
					if ( status != 0 ) throw new Exception( "statvfs() call failed" );

					// Set the values for the exported Prometheus metrics
					TotalBytes.WithLabels( partitionName, mountPath ).Set( filesystemStatistics.f_blocks * filesystemStatistics.f_bsize );
					FreeBytes.WithLabels( partitionName, mountPath ).Set( filesystemStatistics.f_bavail * filesystemStatistics.f_bsize );
					logger.LogDebug( "Updated Prometheus metrics" );

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

	}

}
