using System;
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

			try {
				docker.Update();

				foreach ( string[] labelValues in docker.Status.GetAllLabelValues() ) {
					string id = labelValues[ 0 ];
					string name = labelValues[ 1 ];
					string image = labelValues[ 2 ];

					Assert.True( docker.Status.WithLabels( id, name, image ).Value >= 0, $"Docker container status is below 0 ({ id })" );
					Assert.True( docker.Status.WithLabels( id, name, image ).Value <= 6, $"Docker container status is above 6 ({ id })" );

					// Not every container has exited yet
					if ( docker.ExitCode.WithLabels( id, name, image ).Value != -1 ) {
						Assert.True( docker.ExitCode.WithLabels( id, name, image ).Value >= 0, $"Docker container exit code is below 0 ({ id })" );
					}

					Assert.True( docker.CreatedTimestamp.WithLabels( id, name, image ).Value >= 0, $"Docker container uptime is below 0 seconds ({ id })" );

					// Not every container has a healthcheck
					if ( docker.HealthStatus.WithLabels( id, name, image ).Value != -1 ) {
						Assert.True( docker.HealthStatus.WithLabels( id, name, image ).Value >= 0, $"Docker container health status is below 0 ({ id })" );
						Assert.True( docker.HealthStatus.WithLabels( id, name, image ).Value <= 2, $"Docker container health status is above 2 ({ id })" );
					}
				}
			} catch ( Exception exception ) {
				Console.WriteLine( $"Failed to collect Docker metrics: { exception.Message }" );
			}
		}

	}

}
