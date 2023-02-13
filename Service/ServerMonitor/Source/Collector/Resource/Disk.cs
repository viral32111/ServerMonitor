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

		// POSIX C structure for statvfs()
		[ StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto ) ]
		private struct statvfs_struct {
			public ulong f_bsize;
			public ulong f_frsize;
			public ulong f_blocks;
			public ulong f_bfree;
			public ulong f_bavail;
			public ulong f_files;
			public ulong f_ffree;
			public ulong f_favail;
			public ulong f_fsid;
			public ulong f_flag;
			public ulong f_namemax;
		}

		// POSIX C function to get information about a filesystem on Linux - https://www.man7.org/linux/man-pages/man3/statvfs.3.html
		[ return: MarshalAs( UnmanagedType.I4 ) ]
		[ DllImport( "libc", CharSet = CharSet.Auto, SetLastError = true ) ]
		private static extern int statvfs( string path, out statvfs_struct buf );

		/*
		1. Read /proc/partitions to get the list of partitions, filter anything that doesn't match sdXX or nvmeXnXpX
		2. Loop through each partition...
		 2.1. Read /sys/class/block/<partition>/stat to get read & write statistics
		 2.2. Read /sys/class/block/<partition>/size to get the size of the partition?
		 2.3. Check for symlinks in /sys/class/block/<partition>/holders/, if so...
		  2.3.1. Check if /sys/class/block/<partition>/holders/<symlink>/dm/name exists, if it does then its an encrypted volume, use /dev/mapper/<name> as the device name
		 2.4. If not, use /dev/<partition> as the device name
		 2.5. Read /proc/mounts to get the mount path for the partition
		 2.6. Call statvfs() on that mount path to get total & free space
		*/

		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			string[] driveNames = GetDrives();
			logger.LogDebug( "Found {0} drives", driveNames.Length );
			foreach ( string driveName in driveNames ) {
				DriveStatistics driveStatistics = GetDriveStatistics( driveName );
				logger.LogDebug( "Drive: '{0}', Read: {1} bytes, Write: {2} bytes", driveName, driveStatistics.ReadBytes, driveStatistics.WriteBytes );

				string[] partitionNames = GetPartitions( driveName );
				logger.LogDebug( "Found {0} partitions", partitionNames.Length );
				foreach ( string partitionName in partitionNames ) {
					string partitionPath = Path.Combine( "/dev", partitionName );

					string? mappedName = GetMappedName( partitionName );
					if ( mappedName != null ) partitionPath = Path.Combine( "/dev/mapper", mappedName );
					
					string? mountPath = GetMountPath( partitionPath );
					if ( mountPath == null ) continue; // Skip partitions that aren't mounted

					logger.LogDebug( "Partition: '{0}', Mapped: '{1}', Path: '{2}', Mount: '{3}'", partitionName, mappedName, partitionPath, mountPath );

					statvfs_struct statvfs_struc = new();
					if ( statvfs( mountPath, out statvfs_struc ) != 0 ) throw new Exception( "statvfs() failed" );
					logger.LogDebug( "  Total space: {0} GiB", Math.Round( ( double ) ( statvfs_struc.f_blocks * statvfs_struc.f_bsize ) / 1024 / 1024 / 1024, 2 ) );
					logger.LogDebug( "  Free space: {0} GiB", Math.Round( ( double ) ( statvfs_struc.f_bavail * statvfs_struc.f_bsize ) / 1024 / 1024 / 1024, 2 ) );
				}
			}

			/*logger.LogDebug( string.Join( ", ", Directory.GetDirectories( "/sys/block/" ) ) );
			logger.LogDebug( string.Join( ", ", Directory.GetDirectories( "/sys/block/" ).Where( drivePath => Regex.IsMatch( Path.GetFileName( drivePath ), @"^sd[a-z]+$|^nvme\d+n\d+$" ) ).Where( drivePath => {
				logger.LogDebug( Path.Combine( drivePath, "stat" ) );
				logger.LogDebug( File.Exists( Path.Combine( drivePath, "stat" ) ).ToString() );
				return File.Exists( Path.Combine( drivePath, "stat" ) );
			} ).Where( drivePath => {
				logger.LogDebug( File.ReadAllText( Path.Combine( drivePath, "removable" ) ) );
				logger.LogDebug( ( File.ReadAllText( Path.Combine( drivePath, "removable" ) ) == "0" ).ToString() );
				logger.LogDebug( File.ReadAllLines( Path.Combine( drivePath, "removable" ) )[ 0 ] );
				logger.LogDebug( ( File.ReadAllLines( Path.Combine( drivePath, "removable" ) )[ 0 ] == "0" ).ToString() );
				return File.ReadAllText( Path.Combine( drivePath, "removable" ) ) == "0";
			} ).ToArray() ) );*/
		}

		// Gets a list of drives (for Linux)
		private string[] GetDrives() => Directory.GetDirectories( "/sys/block/" )
			.Where( drivePath => Regex.IsMatch( Path.GetFileName( drivePath ), @"^sd[a-z]+$|^nvme\d+n\d+$" ) ) // Name must be a regular or NVMe drive
			.Where( drivePath => File.Exists( Path.Combine( drivePath, "stat" ) ) ) // Must have I/O statistics
			.Where( drivePath => File.ReadAllLines( Path.Combine( drivePath, "removable" ) )[ 0 ] == "0" ) // Must not be removable
			.Select( drivePath => Path.GetFileName( drivePath ) ) // Only return the drive name
			.ToArray();

		private struct DriveStatistics {
			public long ReadBytes;
			public long WriteBytes;
		}

		// https://www.kernel.org/doc/Documentation/ABI/testing/procfs-diskstats
		// https://unix.stackexchange.com/a/111993
		private DriveStatistics GetDriveStatistics( string driveName ) {
			string statisticsLine = File.ReadAllLines( Path.Combine( "/sys/block", driveName, "stat" ) )[ 0 ];
			string[] statistics = statisticsLine.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			return new() {
				ReadBytes = long.Parse( statistics[ 2 ] ) * 512,
				WriteBytes = long.Parse( statistics[ 6 ] ) * 512
			};
		}

		// Gets a list of partitions for a drive (for Linux)
		private string[] GetPartitions( string driveName ) => Directory.GetDirectories( Path.Combine( "/sys/block", driveName ) ) // List pseudo-directory containing block devices
			.Where( partitionPath => Regex.IsMatch( Path.GetFileName( partitionPath ), @"^sd[a-z]\d+$|^nvme\d+n\d+p\d+$" ) ) // Name must be a regular or NVMe partition
			.Where( partitionPath => File.Exists( Path.Combine( partitionPath, "partition" ) ) ) // Must be a partition
			.Where( partitionPath => int.Parse( File.ReadAllLines( Path.Combine( partitionPath, "partition" ) )[ 0 ] ) > 0 ) // Must have a partition number
			.Select( drivePath => Path.GetFileName( drivePath ) ) // Only return the partition name
			.ToArray();

		// Gets the mapped device name for a partition, if LUKS encrypted (for Linux)
		private string? GetMappedName( string partitionName ) => Directory.GetDirectories( Path.Combine( "/sys/class/block", partitionName, "holders" ) ) // List pseudo-directory containing holder symlinks
			.Where( holderPath => Directory.Exists( Path.Combine( holderPath, "slaves", partitionName ) ) ) // Must be a slave to this partition
			.Where( holderPath => Regex.IsMatch( Path.GetFileName( holderPath ), @"^dm-\d+$" ) ) // Name must be a device mapper
			.Where( holderPath => Directory.Exists( Path.Combine( holderPath, "dm" ) ) ) // Must be a device mapper
			.Where( holderPath => File.Exists( Path.Combine( holderPath, "dm", "name" ) ) ) // Must have a name
			.Where( hodlerPath => File.Exists( Path.Combine( "/dev/mapper", File.ReadAllLines( Path.Combine( hodlerPath, "dm", "name" ) )[ 0 ] ) ) ) // Must be mapped
			.Select( holderPath => File.ReadAllLines( Path.Combine( holderPath, "dm", "name" ) )[ 0 ] ) // Only return the mapped name
			.FirstOrDefault(); // Return the first item, or null if none

		// Gets the mount path for a partition, if mounted (for Linux)
		private string? GetMountPath( string partitionPath ) => File.ReadAllLines( "/proc/mounts" ) // Read pseudo-file containing mount information
			.Select( line => line.Split( " ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) // Split each line into parts
			.Where( parts => parts[ 0 ] == partitionPath ) // Only keep lines related to this partition
			.Select( parts => parts[ 1 ] ) // Only return the mount path
			.FirstOrDefault(); // Return the first item, or null if none

		/*public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			statvfs_struct statvfs_struc = new();
			int result = statvfs( "/", out statvfs_struc );
			if ( result != 0 ) {
				logger.LogError( "statvfs() failed with error code: {0}", result );
				return;
			}
			logger.LogDebug( "Block size: {0}", statvfs_struc.f_bsize );
			logger.LogDebug( "Total blocks: {0}", statvfs_struc.f_blocks );
			logger.LogDebug( "Free blocks for unprivileged users: {0}", statvfs_struc.f_bfree );
			logger.LogDebug( "Free blocks for privileged users: {0}", statvfs_struc.f_bavail );
			logger.LogDebug( "Total inodes: {0}", statvfs_struc.f_files );
			logger.LogDebug( "Free inodes for unprivileged users: {0}", statvfs_struc.f_ffree );
			logger.LogDebug( "Free inodes for privileged users: {0}", statvfs_struc.f_favail );
			logger.LogDebug( "File system ID: {0}", statvfs_struc.f_fsid );
			logger.LogDebug( "Mount flags: {0}", statvfs_struc.f_flag );
			logger.LogDebug( "Maximum filename length: {0}", statvfs_struc.f_namemax );

			logger.LogInformation( "Total disk space: {0} GiB", Math.Round( ( double ) ( statvfs_struc.f_blocks * statvfs_struc.f_bsize ) / 1024 / 1024 / 1024, 2 ) );
			logger.LogInformation( "Free disk space: {0} GiB", Math.Round( ( double ) ( statvfs_struc.f_bavail * statvfs_struc.f_bsize ) / 1024 / 1024 / 1024, 2 ) );*/

			/*DriveInfo[] drives = DriveInfo.GetDrives()
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
			}*/

			/*Partition[] partitions = GetPartitions();
			logger.LogDebug( $"Found { partitions.Length } partitions" );
			foreach ( Partition partition in partitions ) {
				logger.LogDebug( $"Disk { partitionStatistics.Name }, partition { partitionStatistics.Name }, mountpoint { partitionStatistics.Mountpoint }" );
			}
		}*/

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
		/*private PartitionInfo[] GetPartitions( string diskName ) {
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
		}

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
		}*/

	}
}
