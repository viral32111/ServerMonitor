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

			Assert.True( memory.TotalBytes.Value > 0 );
			Assert.True( memory.FreeBytes.Value > 0 );

			Assert.True( memory.SwapTotalBytes.Value > 0 );
			Assert.True( memory.SwapFreeBytes.Value > 0 );
		}

		[ Fact ]
		public void TestProcessorMetrics() {
			ServerMonitor.Collector.Resource.Processor processor = new( mockConfiguration );
			processor.Update();

			Assert.True( processor.Usage.Value >= 0 );
			Assert.True( processor.Usage.Value <= 100 );
			
			Assert.True( processor.Temperature.Value >= 0 );
			Assert.True( processor.Temperature.Value >= 150 );

			Assert.True( processor.Frequency.Value >= 0 );
			// No upper limit for future proofing, but it should be in the range of 1GHz to 10GHz
		}

	}

}
