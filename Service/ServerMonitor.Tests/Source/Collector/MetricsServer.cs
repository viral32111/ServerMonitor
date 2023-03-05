using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class MetricsServer {

		[ Fact ]
		public async Task TestMetricsServer() {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.test.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

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
