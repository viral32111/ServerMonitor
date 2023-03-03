using System;
using System.IO;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class Resources {

		[ Fact ]
		public void TestMemoryMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Resource.Memory memory = new( ServerMonitor.Configuration.Config );
			memory.Update();

			Assert.True( memory.TotalBytes.Value > 0, "Total memory is below 0 bytes" );
			Assert.True( memory.FreeBytes.Value >= 0, "Free memory is below 0 bytes" );
			Assert.True( memory.FreeBytes.Value <= memory.TotalBytes.Value, "Free memory is greater than total memory" );

			Assert.True( memory.SwapTotalBytes.Value > 0, "Total swap/page-file is below 0 bytes" );
			Assert.True( memory.SwapFreeBytes.Value > 0, "Free swap/page-file is below 0 bytes" );
		}

		[ Fact ]
		public void TestProcessorMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Resource.Processor processor = new( ServerMonitor.Configuration.Config );
			processor.Update();

			Assert.True( processor.Usage.Value >= 0, "Processor usage is below 0%" );
			Assert.True( processor.Usage.Value <= 100, "Processor usage is above 100%" );
			
			// Processor temperature is not supported on Windows yet!
			if ( processor.Temperature.Value != -1 ) {
				Assert.True( processor.Temperature.Value >= 0, "Processor temperature is below 0C" );
				Assert.True( processor.Temperature.Value <= 150, "Processor temperature is above 150C" );
			}

			Assert.True( processor.Frequency.Value >= 0, "Processor frequency is below 0Hz" );
			// No upper limit for future proofing, but as of today it should be about 6GHz
		}

		[ Fact ]
		public void TestUptimeMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Resource.Uptime uptime = new( ServerMonitor.Configuration.Config );
			uptime.Update();

			Assert.True( uptime.UptimeSeconds.Value > 0, "Uptime is below 0 seconds" );
		}

		[ Fact ]
		public void TestDiskMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Resource.Disk disk = new( ServerMonitor.Configuration.Config );
			disk.Update();

			foreach ( string[] labelValues in disk.ReadBytes.GetAllLabelValues() ) {
				string driveName = labelValues[ 0 ];

				Assert.True( disk.ReadBytes.WithLabels( driveName ).Value >= 0, $"Total bytes read is below 0 bytes ({ driveName })" );
				Assert.True( disk.WriteBytes.WithLabels( driveName ).Value >= 0, $"Total bytes written is below 0 bytes ({ driveName })" );

				// S.M.A.R.T health is not supported yet!
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
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Resource.Network network = new( ServerMonitor.Configuration.Config );
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

	}

}
