using System;
using System.IO;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class Information {
		
		[ Fact ]
		public void TestUptimeMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.Resource.Information information = new( ServerMonitor.Configuration.Config );
			information.Update();

			foreach ( string[] labelValues in information.UptimeSeconds.GetAllLabelValues() ) {
				string name = labelValues[ 0 ];
				string operatingSystem = labelValues[ 1 ];
				string architecture = labelValues[ 2 ];
				string version = labelValues[ 3 ];

				Assert.True( information.UptimeSeconds.WithLabels( name, operatingSystem, architecture, version ).Value > 0, "Uptime is below 0 seconds" );
			}
		}

	}

}
