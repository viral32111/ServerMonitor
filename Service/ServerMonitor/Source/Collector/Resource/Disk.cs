using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Mono.Unix.Native;
using Prometheus;

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system disk metrics
	public class Disk : Base {

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

			TotalBytes.Set( -1 );
			FreeBytes.Set( -1 );
			ReadBytes.IncTo( -1 );
			WriteBytes.IncTo( -1 );
			Health.Set( -1 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		/*********************************************************************************************************************/

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

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

				long[] stats = GetDriveStatisticsForWindows( driveMountPath );
				ReadBytes.WithLabels( driveName ).IncTo( stats[ 0 ] );
				WriteBytes.WithLabels( driveName ).IncTo( stats[ 1 ] );

				// TODO: S.M.A.R.T health
				Health.WithLabels( driveName ).Set( -1 );

				logger.LogDebug( "Updated Prometheus metrics" );
			}

		}

		// Get read & write statistics for a drive (for Windows) - https://stackoverflow.com/a/30451751
		[ SupportedOSPlatform( "windows" ) ]
		private long[] GetDriveStatisticsForWindows( string driveLetter ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// File & I/O control code constants - https://learn.microsoft.com/en-us/windows/win32/fileio/file-access-rights-constants, http://www.ioctls.net
			const UInt32 FILE_READ_ATTRIBUTES = 0x80;
			const UInt32 FILE_SHARE_READ = 0x1;
			const UInt32 FILE_SHARE_WRITE = 0x2;
			const UInt32 OPEN_EXISTING = 3;
			const UInt32 IOCTL_DISK_PERFORMANCE = 0x70020;

			// Create the volume path & remove any trailing backslashes from the drive letter (e.g., \\.\C:)
			string volumePath = string.Concat( @"\\.\", driveLetter.TrimEnd( '\\' ) );

			// Reference to an invalid Windows handle, required as we're not passing a template
			SafeFileHandle INVALID_HANDLE_VALUE = new( new IntPtr( -1 ), true );

			// Try to open the volume
			SafeFileHandle deviceHandle = CreateFileW( volumePath, FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, INVALID_HANDLE_VALUE );
			if ( deviceHandle.IsInvalid ) throw new Win32Exception( Marshal.GetLastWin32Error() ); // https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes

			// Expected size of the output buffer, cannot be any lower otherwise DeviceIoControl() fails with code 87 (parameter incorrect)
			const int outBufferSize = 88;

			// Get disk performance information for this volume
			DISK_PERFORMANCE diskPerformance = new();
			bool ioSuccess = DeviceIoControl( deviceHandle, IOCTL_DISK_PERFORMANCE, Array.Empty<byte>(), 0, diskPerformance, outBufferSize, out UInt32 bytesReturned, IntPtr.Zero );
			if ( !ioSuccess ) throw new Win32Exception( Marshal.GetLastWin32Error() );
			if ( bytesReturned != outBufferSize ) throw new Exception( $"Windows API function DeviceIoControl() returned { bytesReturned } bytes, expected { outBufferSize } bytes" );

			// Return the total bytes read & written
			return new[] { diskPerformance.BytesRead, diskPerformance.BytesWritten };

		}

		/*[ SupportedOSPlatform( "windows" ) ]
		private void SMART( DriveInfo driveInformation ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

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

		/*********************************************************************************************************************/

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			// Loop through each drive...
			foreach ( string driveName in GetDrives() ) {

				// Set the values for the exported Prometheus metrics
				long[] driveStatistics = GetDriveStatisticsForLinux( driveName );
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
					int statusCode = Syscall.statvfs( mountPath, out filesystemStatistics );
					if ( statusCode != 0 ) throw new Exception( $"Linux syscall statvfs() failed with code { statusCode }" );

					// Set the values for the exported Prometheus metrics
					TotalBytes.WithLabels( partitionName, mountPath ).Set( filesystemStatistics.f_blocks * filesystemStatistics.f_bsize );
					FreeBytes.WithLabels( partitionName, mountPath ).Set( filesystemStatistics.f_bavail * filesystemStatistics.f_bsize );
					logger.LogDebug( "Updated Prometheus metrics" );

				}

				// TODO: S.M.A.R.T health
				Health.WithLabels( driveName ).Set( -1 );

			}

		}

		// Gets a list of drives (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private string[] GetDrives() => Directory.GetDirectories( "/sys/block/" )
			.Where( drivePath => Regex.IsMatch( Path.GetFileName( drivePath ), @"^sd[a-z]+$|^nvme\d+n\d+$" ) ) // Name must be a regular or NVMe drive
			.Where( drivePath => File.Exists( Path.Combine( drivePath, "stat" ) ) ) // Must have I/O statistics
			.Where( drivePath => File.ReadAllLines( Path.Combine( drivePath, "removable" ) )[ 0 ] == "0" ) // Must not be removable
			.Select( drivePath => Path.GetFileName( drivePath ) ) // Only return the drive name
			.ToArray();

		// Get read & write statistics for a drive (for Linux) - https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats, https://unix.stackexchange.com/a/111993
		[ SupportedOSPlatform( "linux" ) ]
		private long[] GetDriveStatisticsForLinux( string driveName ) => File.ReadAllLines( Path.Combine( "/sys/block", driveName, "stat" ) )
			.Select( line => line.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split each line into parts
			.Select( parts => parts.Select( part => long.Parse( part ) ).ToArray() ) // Convert each part to a number
			.Select( parts => new long[] { parts[ 2 ] * 512, parts[ 6 ] * 512 } ) // Only return total read & written, as bytes by multiplying sector count by 512
			.First();

		// Gets a list of partitions for a drive (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private string[] GetPartitions( string driveName ) => Directory.GetDirectories( Path.Combine( "/sys/block", driveName ) ) // List pseudo-directory containing block devices
			.Where( partitionPath => Regex.IsMatch( Path.GetFileName( partitionPath ), @"^sd[a-z]\d+$|^nvme\d+n\d+p\d+$" ) ) // Name must be a regular or NVMe partition
			.Where( partitionPath => File.Exists( Path.Combine( partitionPath, "partition" ) ) ) // Must be a partition
			.Where( partitionPath => int.Parse( File.ReadAllLines( Path.Combine( partitionPath, "partition" ) )[ 0 ] ) > 0 ) // Must have a partition number
			.Select( drivePath => Path.GetFileName( drivePath ) ) // Only return the partition name
			.ToArray();

		// Gets the mapped device name for a partition, if LUKS encrypted (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private string? GetMappedName( string partitionName ) => Directory.GetDirectories( Path.Combine( "/sys/class/block", partitionName, "holders" ) ) // List pseudo-directory containing holder symlinks
			.Where( holderPath => Directory.Exists( Path.Combine( holderPath, "slaves", partitionName ) ) ) // Must be a slave to this partition
			.Where( holderPath => Regex.IsMatch( Path.GetFileName( holderPath ), @"^dm-\d+$" ) ) // Name must be a device mapper
			.Where( holderPath => Directory.Exists( Path.Combine( holderPath, "dm" ) ) ) // Must be a device mapper
			.Where( holderPath => File.Exists( Path.Combine( holderPath, "dm", "name" ) ) ) // Must have a name
			.Where( hodlerPath => File.Exists( Path.Combine( "/dev/mapper", File.ReadAllLines( Path.Combine( hodlerPath, "dm", "name" ) )[ 0 ] ) ) ) // Must be mapped
			.Select( holderPath => File.ReadAllLines( Path.Combine( holderPath, "dm", "name" ) )[ 0 ] ) // Only return the mapped name
			.FirstOrDefault(); // Return the first item, or null if none

		// Gets the mount path for a partition, if mounted (for Linux) - https://linux.die.net/man/5/proc
		[ SupportedOSPlatform( "linux" ) ]
		private string? GetMountPath( string partitionPath ) => File.ReadAllLines( "/proc/mounts" ) // Read pseudo-file containing mount information
			.Select( line => line.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split each line into parts
			.Where( parts => parts[ 0 ] == partitionPath ) // Only keep lines related to this partition
			.Select( parts => parts[ 1 ] ) // Only return the mount path
			.FirstOrDefault(); // Return the first item, or null if none

		/*********************************************************************************************************************/

		// C++ Windows API structure for DeviceIoControl() - https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-disk_performance
		// 64-bit signed integer is 8 bytes, 32-bit unsigned integer is 4 bytes, WCHAR (16-bit unsigned integer) is 2 bytes
		[ SupportedOSPlatform( "windows" ) ]
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Unicode ) ]
		private class DISK_PERFORMANCE {
			[ MarshalAs( UnmanagedType.I8 ) ] public Int64 BytesRead; // LARGE_INTEGER
			[ MarshalAs( UnmanagedType.I8 ) ] public Int64 BytesWritten; // LARGE_INTEGER
			[ MarshalAs( UnmanagedType.I8 ) ] public Int64 ReadTime; // LARGE_INTEGER
			[ MarshalAs( UnmanagedType.I8 ) ] public Int64 WriteTime; // LARGE_INTEGER
			[ MarshalAs( UnmanagedType.I8 ) ] public Int64 IdleTime; // LARGE_INTEGER
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 ReadCount; // DWORD
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 WriteCount; // DWORD
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 QueueDepth; // DWORD
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 SplitCount; // DWORD
			[ MarshalAs( UnmanagedType.I8 ) ] public Int64 QueryTime; // LARGE_INTEGER
			[ MarshalAs( UnmanagedType.U4 ) ] public UInt32 StorageDeviceNumber; // DWORD
			public IntPtr StorageManagerName; // WCHAR[8], Attempting to Marshal this as a string causes an access violation exception...
		}

		// C++ Windows API function for creating/opening a device file - https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew, https://www.pinvoke.net/default.aspx/kernel32.CreateFile
		[ DllImport( "kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true ) ]
		[ SupportedOSPlatform( "windows" ) ]
		private static extern SafeFileHandle CreateFileW(
			[ In, MarshalAs( UnmanagedType.LPWStr ) ] string lpFileName, // LPCWSTR
			[ In ] UInt32 dwDesiredAccess, // DWORD
			[ In ] UInt32 dwShareMode, // DWORD
			[ In, Optional ] IntPtr lpSecurityAttributes, // LPSECURITY_ATTRIBUTES
			[ In ] UInt32 dwCreationDisposition, // DWORD
			[ In ] UInt32 dwFlagsAndAttributes, // DWORD
			[ In, Optional ] SafeFileHandle hTemplateFile // HANDLE
		);

		// C++ Windows API function for exchanging I/O control commands to devices - https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol, https://www.pinvoke.net/default.aspx/kernel32.deviceiocontrol, https://stackoverflow.com/a/17354960
		[ return: MarshalAs( UnmanagedType.Bool ) ]
		[ SupportedOSPlatform( "windows" ) ]
		[ DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern bool DeviceIoControl(
			[ In ] SafeFileHandle hDevice, // HANDLE
			[ In ] UInt32 IoControlCode, // DWORD
			[ In, Optional ] byte[] InBuffer, // LPVOID
			[ In ] UInt32 nInBufferSize, // DWORD
			[ Out, Optional ] DISK_PERFORMANCE OutBuffer, // LPVOID
			[ In ] UInt32 nOutBufferSize, // DWORD
			[ Out, Optional ] out UInt32 pBytesReturned, // LPDVOID
			[ In, Out, Optional ] IntPtr Overlapped // LPOVERLAPPED
		);

	}

}
