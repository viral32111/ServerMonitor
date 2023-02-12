using System;
using Xunit;

namespace ServerMonitor.Tests {

	public class MetricsCollectionUnitTests {

		// Create a mock configuration
		private static readonly Config mockConfiguration = new() {
			PrometheusListenAddress = "127.0.0.1",
			PrometheusListenPort = 5000,
			PrometheusListenPath = "metrics",
			PrometheusMetricsPrefix = "server_monitor"
		};

		[ Fact ]
		public void TestMemoryMetrics() {
			ServerMonitor.Collector.Resource.Memory memory = new( mockConfiguration );
			memory.Update();

			Assert.True( memory.TotalBytes.Value > 0, "Total memory is below 0 bytes" );
			Assert.True( memory.FreeBytes.Value >= 0, "Free memory is below 0 bytes" );
			Assert.True( memory.FreeBytes.Value <= memory.TotalBytes.Value, "Free memory is greater than total memory" );

			Assert.True( memory.SwapTotalBytes.Value > 0, "Total swap/page-file is below 0 bytes" );
			Assert.True( memory.SwapFreeBytes.Value > 0, "Free swap/page-file is below 0 bytes" );
		}

		[ Fact ]
		public void TestProcessorMetrics() {
			ServerMonitor.Collector.Resource.Processor processor = new( mockConfiguration );
			processor.Update();

			Assert.True( processor.Usage.Value >= 0, "Processor usage is below 0%" );
			Assert.True( processor.Usage.Value <= 100, "Processor usage is above 100%" );
			
			Assert.True( processor.Temperature.Value >= 0, "Processor temperature is below 0C" );
			Assert.True( processor.Temperature.Value <= 150, "Processor temperature is above 150C" );

			Assert.True( processor.Frequency.Value >= 0, "Processor frequency is below 0Hz" );
			// No upper limit for future proofing, but as of today it should be about 6GHz
		}

		[ Fact ]
		public void TestUptimeMetrics() {
			ServerMonitor.Collector.Resource.Uptime uptime = new( mockConfiguration );
			uptime.Update();

			Assert.True( uptime.UptimeSeconds.Value > 0, "Uptime is below 0 seconds" );
		}

		/*[ Fact ]
		public void TestDiskMetrics() {
			ServerMonitor.Collector.Resource.Disk disk = new( mockConfiguration );
			disk.Update();

			foreach ( string[] labelValues in disk.TotalBytes.GetAllLabelValues() ) {
				string driveLabel = labelValues[ 0 ];
				string driveFilesystem = labelValues[ 1 ];
				string driveMountpoint = labelValues[ 2 ];

				Assert.True( disk.TotalBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value > 0, $"Total disk space is below 0 bytes ({ driveMountpoint })" );
				Assert.True( disk.FreeBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value >= 0, $"Free disk space is below 0 bytes ({ driveMountpoint })" );
				Assert.True( disk.FreeBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value <= disk.TotalBytes.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value, $"Free disk space is greater than total disk space ({ driveMountpoint })" );

				Assert.True( disk.WriteBytesPerSecond.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value >= 0, $"Disk write speed is below 0 bytes per second ({ driveMountpoint })" );
				Assert.True( disk.ReadBytesPerSecond.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value >= 0, $"Disk read speed is below 0 bytes per second ({ driveMountpoint })" );

				if ( disk.Health.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value != -1 ) {
					Assert.True( disk.Health.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value > 0, $"Disk S.M.A.R.T health is below 0% ({ driveMountpoint })" );
					Assert.True( disk.Health.WithLabels( driveLabel, driveFilesystem, driveMountpoint ).Value < 100, $"Disk S.M.A.R.T health is above 100% ({ driveMountpoint })" );
				}

			}

		}*/

	}

}
