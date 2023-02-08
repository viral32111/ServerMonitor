using Xunit;

namespace ServerMonitor.Tests {

	public class MetricsCollectionUnitTests {

		[ Fact ]
		public void TestMemoryMetrics() {
			ServerMonitor.Collector.Resource.Memory memory = new();
			memory.Update();

			Assert.True( memory.TotalBytes > 0 );
			Assert.True( memory.FreeBytes > 0 );
			Assert.True( memory.GetUsedBytes() > 0 );

			Assert.True( memory.GetUsedPercentage() >= 0 );
			Assert.True( memory.GetUsedPercentage() <= 100 );
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
