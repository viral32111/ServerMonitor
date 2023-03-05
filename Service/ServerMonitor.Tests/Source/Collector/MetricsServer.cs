using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class MetricsServer {

		[ Fact ]
		public async Task TestMetricsServer() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			// Disable all metrics as we're only testing listening functionality
			ServerMonitor.Configuration.Config.CollectProcessorMetrics = false;
			ServerMonitor.Configuration.Config.CollectMemoryMetrics = false;
			ServerMonitor.Configuration.Config.CollectDiskMetrics = false;
			ServerMonitor.Configuration.Config.CollectNetworkMetrics = false;
			ServerMonitor.Configuration.Config.CollectUptimeMetrics = false;
			ServerMonitor.Configuration.Config.CollectPowerMetrics = false;
			ServerMonitor.Configuration.Config.CollectFanMetrics = false;
			ServerMonitor.Configuration.Config.CollectServiceMetrics = false;
			ServerMonitor.Configuration.Config.CollectDockerMetrics = false;
			ServerMonitor.Configuration.Config.CollectSNMPMetrics = false;

			ServerMonitor.Collector.Collector.HandleCommand( ServerMonitor.Configuration.Config, true );

			HttpClient httpClient = new();
			HttpResponseMessage response = await httpClient.GetAsync( string.Format(
				"http://{0}:{1}/{2}",
				ServerMonitor.Configuration.Config.PrometheusListenAddress,
				ServerMonitor.Configuration.Config.PrometheusListenPort,
				ServerMonitor.Configuration.Config.PrometheusListenPath
			) );

			Assert.True( response.IsSuccessStatusCode );
		}

	}

}
