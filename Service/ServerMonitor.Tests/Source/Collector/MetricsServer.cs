using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class MetricsServer {

		// Configuration object with all collectors disabled as we're only testing the request/response functionality
		private static readonly Config mockConfig = new() {
			PrometheusListenAddress = "127.0.0.1",
			PrometheusListenPort = 5000,
			PrometheusListenPath = "metrics/",
			PrometheusMetricsPrefix = default!,

			CollectProcessorMetrics = false,
			CollectMemoryMetrics = false,
			CollectDiskMetrics = false,
			CollectUptimeMetrics = false,
			CollectNetworkMetrics = false,
			CollectFanMetrics = false,
			CollectPowerMetrics = false,
			CollectServiceMetrics = false,
			CollectDockerMetrics = false,

			DockerEngineAPIAddress = default!,
			DockerEngineAPIVersion = default!
		};

		[ Fact ]
		public async Task TestMetricsServer() {
			ServerMonitor.Collector.Collector.HandleCommand( mockConfig, true );

			HttpClient httpClient = new();
			HttpResponseMessage response = await httpClient.GetAsync( string.Format(
				"http://{0}:{1}/{2}",
				mockConfig.PrometheusListenAddress,
				mockConfig.PrometheusListenPort,
				mockConfig.PrometheusListenPath
			) );

			Assert.True( response.IsSuccessStatusCode );
		}

	}

}
