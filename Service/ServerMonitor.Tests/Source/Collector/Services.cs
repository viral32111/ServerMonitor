using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class Services {

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

			CollectServiceMetrics = true,

			CollectDockerMetrics = true,
			DockerEngineAPIAddress = "tcp://127.0.0.1:2375",
			DockerEngineAPIVersion = 1.41f
		};

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
