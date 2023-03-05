using System;
using System.IO;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class Services {

		[ Fact ]
		public void TestServicesMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.test.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Services services = new( ServerMonitor.Configuration.Config );
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
