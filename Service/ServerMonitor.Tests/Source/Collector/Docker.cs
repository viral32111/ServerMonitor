using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class Docker {

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
		public void TestDockerMetrics() {
			ServerMonitor.Collector.Docker docker = new( mockConfiguration );
			docker.Update();

			foreach ( string[] labelValues in docker.Status.GetAllLabelValues() ) {
				string id = labelValues[ 0 ];
				string name = labelValues[ 1 ];
				string image = labelValues[ 2 ];

				Assert.True( docker.Status.WithLabels( id, name, image ).Value >= 0, $"Docker container status is below 0 ({ id })" );
				Assert.True( docker.Status.WithLabels( id, name, image ).Value <= 6, $"Docker container status is above 6 ({ id })" );

				Assert.True( docker.ExitCode.WithLabels( id, name, image ).Value >= 0, $"Docker container exit code is below 0 ({ id })" );

				Assert.True( docker.CreatedTimestamp.WithLabels( id, name, image ).Value >= 0, $"Docker container uptime is below 0 seconds ({ id })" );
			}
		}

	}

}
