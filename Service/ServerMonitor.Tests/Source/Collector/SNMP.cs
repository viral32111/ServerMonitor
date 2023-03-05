using System;
using System.IO;
using System.Threading;
using Xunit;

namespace ServerMonitor.Tests.Collector {

	public class SNMP {

		[ Fact ]
		public void TestSNMPMetrics() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Collector.SNMP snmp = new( ServerMonitor.Configuration.Config, CancellationToken.None );
			snmp.Update();

			foreach ( string[] labelValues in snmp.TrapsReceived.GetAllLabelValues() ) {
				string address = labelValues[ 0 ];
				string port = labelValues[ 1 ];
				string name = labelValues[ 2 ];
				string description = labelValues[ 3 ];
				string contact = labelValues[ 4 ];
				string location = labelValues[ 5 ];

				if ( snmp.TrapsReceived.WithLabels( address, port, name, description, contact, location ).Value != -1 ) {
					Assert.True( snmp.TrapsReceived.WithLabels( address, port, name, description, contact, location ).Value >= 0, "SNMP received trap count is below zero" );
				}

				if ( snmp.UptimeSeconds.WithLabels( address, port, name, description, contact, location ).Value != -1 ) {
					Assert.True( snmp.UptimeSeconds.WithLabels( address, port, name, description, contact, location ).Value >= 0, "SNMP agent uptime is below zero" );
				}

				if ( snmp.ServiceCount.WithLabels( address, port, name, description, contact, location ).Value != -1 ) {
					Assert.True( snmp.ServiceCount.WithLabels( address, port, name, description, contact, location ).Value >= 0, "SNMP agent service count is below zero" );
				}
			}
		}

	}

}
