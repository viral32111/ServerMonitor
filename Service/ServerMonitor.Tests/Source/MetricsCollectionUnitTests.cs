using Xunit;

namespace ServerMonitor.Tests {

	public class MetricsCollectionUnitTests {

		// Create a mock configuration
		private static readonly Config mockConfiguration = new() {
			PrometheusListenAddress = "127.0.0.1",
			PrometheusListenPort = 5000,
			PrometheusListenPath = "metrics",
			PrometheusMetricsPrefix = "server_monitor",
			CollectProcessorMetrics = true,
			CollectMemoryMetrics = true,
			CollectDiskMetrics = true,
			CollectNetworkMetrics = true,
			CollectUptimeMetrics = true,
			CollectPowerMetrics = false,
			CollectFanMetrics = false,
			CollectServiceMetrics = true
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

		[ Fact ]
		public void TestDiskMetrics() {
			ServerMonitor.Collector.Resource.Disk disk = new( mockConfiguration );
			disk.Update();

			foreach ( string[] labelValues in disk.ReadBytes.GetAllLabelValues() ) {
				string driveName = labelValues[ 0 ];

				Assert.True( disk.ReadBytes.WithLabels( driveName ).Value >= 0, $"Total bytes read is below 0 bytes ({ driveName })" );
				Assert.True( disk.WriteBytes.WithLabels( driveName ).Value >= 0, $"Total bytes written is below 0 bytes ({ driveName })" );

				if ( disk.Health.WithLabels( driveName ).Value != -1 ) {
					Assert.True( disk.Health.WithLabels( driveName ).Value >= 0, $"Disk S.M.A.R.T health is below 0% ({ driveName })" );
					Assert.True( disk.Health.WithLabels( driveName ).Value <= 100, $"Disk S.M.A.R.T health is above 100% ({ driveName })" );
				}
			}

			foreach ( string[] labelValues in disk.TotalBytes.GetAllLabelValues() ) {
				string partitionName = labelValues[ 0 ];
				string partitionMountPath = labelValues[ 1 ];

				Assert.True( disk.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value > 0, $"Total partition space is below 0 bytes ({ partitionName }, { partitionMountPath })" );
				Assert.True( disk.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value >= 0, $"Free partition space is below 0 bytes ({ partitionName }, { partitionMountPath })" );

				Assert.True( disk.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value <= disk.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value, $"Free partition space is greater than total partition space ({ partitionName }, { partitionMountPath })" );
			}

		}

		[ Fact ]
		public void TestNetworkMetrics() {
			ServerMonitor.Collector.Resource.Network network = new( mockConfiguration );
			network.Update();

			foreach ( string[] labelValues in network.SentBytes.GetAllLabelValues() ) {
				string interfaceName = labelValues[ 0 ];
				Assert.True( network.SentBytes.WithLabels( interfaceName ).Value >= 0, $"Network bytes sent is below 0 bytes ({ interfaceName })" );
			}

			foreach ( string[] labelValues in network.ReceivedBytes.GetAllLabelValues() ) {
				string interfaceName = labelValues[ 0 ];
				Assert.True( network.ReceivedBytes.WithLabels( interfaceName ).Value >= 0, $"Network bytes received is below 0 bytes ({ interfaceName })" );
			}
		}

		[ Fact ]
		public void TestServicesMetrics() {
			ServerMonitor.Collector.Services services = new( mockConfiguration );
			services.Update();

			foreach ( string[] labelValues in services.StatusCode.GetAllLabelValues() ) {
				string service = labelValues[ 0 ];
				string name = labelValues[ 1 ];
				string description = labelValues[ 2 ];

				Assert.True( services.StatusCode.WithLabels( service, name, description ).Value >= 0, $"Service status code is below 0 ({ service })" );
				Assert.True( services.StatusCode.WithLabels( service, name, description ).Value <= 4, $"Service status code is above 4 ({ service })" );

				Assert.True( services.ExitCode.WithLabels( service, name, description ).Value >= 0, $"Service exit code is below 0 ({ service })" );

				Assert.True( services.UptimeSeconds.WithLabels( service, name, description ).Value >= 0, $"Service uptime is below 0 seconds ({ service })" );
			}
		}

	}

}
