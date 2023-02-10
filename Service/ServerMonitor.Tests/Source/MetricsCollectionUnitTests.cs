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
			ServerMonitor.Collector.Resource.Processor processor = new();
			processor.Update();

			Assert.True( processor.Usage >= 0 );
			Assert.True( processor.Usage <= 100 );
		}

	}

}
