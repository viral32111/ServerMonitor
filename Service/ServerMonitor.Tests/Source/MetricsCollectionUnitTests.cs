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
			Assert.True( memory.FreeBytes.Value > 0, "Free memory is below 0 bytes" );

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

			Assert.True( disk.TotalBytes.Value > 0, "Total disk space is below 0 bytes" );
			Assert.True( disk.FreeBytes.Value >= 0, "Free disk space is below 0 bytes" );

			Assert.True( disk.WriteBytesPerSecond.Value >= 0, "Disk read speed is below 0 bytes per second" );
			Assert.True( disk.ReadBytesPerSecond.Value >= 0, "Disk read speed is below 0 bytes per second" );

			Assert.True( disk.Health.Value > 0, "Disk S.M.A.R.T health is below 0%" );
			Assert.True( disk.Health.Value < 100, "Disk S.M.A.R.T health is above 100%" );
		}

	}

}
