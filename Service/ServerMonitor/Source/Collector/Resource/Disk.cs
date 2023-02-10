using System;
using System.IO;
using System.Linq;
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
		public override void Update() {

			// Get the relevant drive information
			DriveInfo[] driveInformation = DriveInfo.GetDrives()
				.Where( driveInfo => driveInfo.DriveType == DriveType.Fixed ) // Skip network shares, ramfs, etc.
				.Where( driveInfo => driveInfo.IsReady == true ) // Skip unmounted drives
				.Where( driveInfo => driveInfo.DriveFormat != "9P" && driveInfo.DriveFormat != "v9fs" ) // Skip WSL-related filesystems
				.Where( driveInfo => driveInfo.DriveFormat != "overlay" ) // Skip Docker-related filesystems
				.ToArray();

			// Update the metrics for each drive
			foreach ( DriveInfo driveInfo in driveInformation ) {
				string driveLabel = driveInfo.VolumeLabel;
				string driveFileSystem = driveInfo.DriveFormat;
				string driveMountpoint = driveInfo.RootDirectory.FullName;

				TotalBytes.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( driveInfo.TotalSize );
				FreeBytes.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( driveInfo.TotalFreeSpace );

				// TODO
				Health.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( -1 );
				WriteBytesPerSecond.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( 0 );
				ReadBytesPerSecond.WithLabels( driveLabel, driveFileSystem, driveMountpoint ).Set( 0 );
			}

		}

	}
}
