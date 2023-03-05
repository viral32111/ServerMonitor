using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SnmpSharpNet;
using Microsoft.Extensions.Logging;
using Prometheus;

/* Test commands:
https://stackoverflow.com/a/37281164
 snmptrap -v 2c -c 'Server Monitor' 10.0.0.100:1620 0 1.3.6.1.4.1.2.3 1.3.6.1.6.1.4.1.2.3.1.1.1.1.1 s "This is a Test"

https://support.nagios.com/kb/article.php?id=493
 snmptrap -v 2c -c 'Server Monitor' 10.0.0.100:1620 '' 1.3.6.1.4.1.8072.2.3.0.1 1.3.6.1.4.1.8072.2.3.2.1 i 123456
*/

/* Get all SNMP OIDs for an agent:
 snmpwalk -v 2c -c 'Server Monitor' 10.0.0.1:161 > snmpwalk-names.txt
 snmpwalk -v 2c -O n -c 'Server Monitor' 10.0.0.1:161 > snmpwalk-ids.txt
*/

/* https://blog.domotz.com/know-your-networks/snmp-port-number/
UDP port 161 connects the SNMP Managers with SNMP Agents (i.e. polling)
UDP port 162 sees use when SNMP Agents send unsolicited traps to the SNMP Manager
*/

// https://snmpsharpnet.com/index.php/receive-snmp-version-1-and-2c-trap-notifications/
// https://snmpsharpnet.com/

namespace ServerMonitor.Collector {

	// An SNMP manager to fetch agent information & listen for traps
	public class SNMP : Base {

		private readonly ILogger logger = Logging.CreateLogger( "Collector/SNMP" );

		// Various required properties
		private readonly Config configuration;
		private readonly CancellationToken cancellationToken;
		private readonly Socket udpSocket = new( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
		private Task? receiveTask = null;

		// Holds the exported Prometheus metrics
		public readonly Counter TrapsReceived;
		public readonly Counter UptimeSeconds;
		public readonly Gauge ServiceCount;

		// Initializes the exported Prometheus metrics & listening socket
		public SNMP( Config configuration, CancellationToken cancellationToken ) : base( configuration ) {
			this.configuration = configuration;
			this.cancellationToken = cancellationToken;

			TrapsReceived = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_snmp_traps_received", "Number of SNMP traps received.", new CounterConfiguration {
				LabelNames = new[] { "address", "port", "name", "description", "contact", "location" }
			} );
			UptimeSeconds = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_snmp_uptime_seconds", "Number of seconds an SNMP agent has been running.", new CounterConfiguration {
				LabelNames = new[] { "address", "port", "name", "description", "contact", "location" }
			} );
			ServiceCount = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_snmp_service_count", "Number of services an SNMP agent is running.", new GaugeConfiguration {
				LabelNames = new[] { "address", "port", "name", "description", "contact", "location" }
			} );
			TrapsReceived.IncTo( -1 );
			UptimeSeconds.IncTo( -1 );
			ServiceCount.Set( -1 );
			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		public override void Update() {
			logger.LogDebug( "There are {0} SNMP agents", configuration.SNMPAgents.Length );
			
			foreach ( SNMPAgent snmpAgent in configuration.SNMPAgents ) {
				logger.LogDebug( "Updating metrics for SNMP agent '{0}:{1}'", snmpAgent.Address, snmpAgent.Port );
				UpdateAgentInformation( snmpAgent.Address, snmpAgent.Port );
			}

			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Starts the receive packets background task
		public void ListenForTraps() {
			if ( this.receiveTask != null ) throw new InvalidOperationException( "SNMP manager already started" );

			IPAddress listenAddress = configuration.SNMPManagerListenAddress == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse( configuration.SNMPManagerListenAddress );
			int listenPort = configuration.SNMPManagerListenPort;
			udpSocket.Bind( new IPEndPoint( listenAddress, listenPort ) );
			udpSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 0 ); // Disable timeout
			logger.LogInformation( "SNMP manager listening on {0}:{1}", configuration.SNMPManagerListenAddress, configuration.SNMPManagerListenPort );

			logger.LogDebug( "Starting to receive packets..." );
			this.receiveTask = ReceivePackets();
		}

		public void WaitForTrapListener() {
			if ( this.receiveTask == null ) throw new InvalidOperationException( "SNMP manager not started" );

			logger.LogDebug( "Waiting for receive task to finish..." );
			this.receiveTask.Wait();
			logger.LogDebug( "Receive task finished" );
		}

		// https://snmpsharpnet.com/index.php/snmp-version-1-or-2c-get-request/
		public void UpdateAgentInformation( string agentAddress, int agentPort = 161 ) {
			OctetString community = new( "Server Monitor" );
			AgentParameters managerParameters = new( community );
			managerParameters.Version = SnmpVersion.Ver1;

			using ( UdpTarget target = new( IPAddress.Parse( agentAddress ), agentPort, 2000, 1 ) ) {
				Pdu pdu = new( PduType.Get );
				pdu.VbList.Add( "1.3.6.1.2.1.1.1.0" ); // SNMPv2-MIB::sysDescr.0
				pdu.VbList.Add( "1.3.6.1.2.1.1.2.0" ); // SNMPv2-MIB::sysObjectID.0
				pdu.VbList.Add( "1.3.6.1.2.1.1.3.0" ); // DISMAN-EVENT-MIB::sysUpTimeInstance
				pdu.VbList.Add( "1.3.6.1.2.1.1.4.0" ); // SNMPv2-MIB::sysContact.0
				pdu.VbList.Add( "1.3.6.1.2.1.1.5.0" ); // SNMPv2-MIB::sysName.0
				pdu.VbList.Add( "1.3.6.1.2.1.1.6.0" ); // SNMPv2-MIB::sysLocation.0
				pdu.VbList.Add( "1.3.6.1.2.1.1.7.0" ); // SNMPv2-MIB::sysServices.0

				SnmpV1Packet result = ( SnmpV1Packet ) target.Request( pdu, managerParameters );
				if ( result == null ) throw new Exception( $"No response from SNMP agent '{ agentAddress }:{ agentPort }'" );

				if ( result.Pdu.ErrorStatus != 0 ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned error status '{ result.Pdu.ErrorStatus }'" );

				foreach ( Vb varBind in result.Pdu.VbList ) {
					logger.LogDebug( "SNMP agent '{0}:{1}' returned '{2}' ({3}) = '{4}'", agentAddress, agentPort, varBind.Oid.ToString(), SnmpConstants.GetTypeName( varBind.Value.Type ), varBind.Value.ToString() );
				}

				string? description = result.Pdu.VbList[ 0 ].Value.ToString();
				string? objectID = result.Pdu.VbList[ 1 ].Value.ToString();
				string? uptime = result.Pdu.VbList[ 2 ].Value.ToString();
				string? contact = result.Pdu.VbList[ 3 ].Value.ToString();
				string? name = result.Pdu.VbList[ 4 ].Value.ToString();
				string? location = result.Pdu.VbList[ 5 ].Value.ToString();
				string? services = result.Pdu.VbList[ 6 ].Value.ToString();

				if (
					string.IsNullOrWhiteSpace( description ) ||
					string.IsNullOrWhiteSpace( objectID ) ||
					string.IsNullOrWhiteSpace( uptime ) ||
					string.IsNullOrWhiteSpace( contact ) ||
					string.IsNullOrWhiteSpace( name ) ||
					string.IsNullOrWhiteSpace( location ) ||
					string.IsNullOrWhiteSpace( services )
				) throw new Exception( $"One or more values returned by SNMP agent '{ agentAddress }:{ agentPort }' are null, empty or whitespace" );

				// Update Prometheus metrics
				TrapsReceived.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).IncTo( -1 ); // TODO
				UptimeSeconds.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).IncTo( -1 ); // TODO
				ServiceCount.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).IncTo( int.Parse( services ) );
			}
		}

		// Runs in the background to receive packets
		private async Task ReceivePackets() {
			byte[] receiveBuffer = new byte[ 65565 ];

			logger.LogDebug( "Started receiving packets" );
			try {
				while ( udpSocket.IsBound && this.cancellationToken.IsCancellationRequested == false ) {
					int bytesReceived = await udpSocket.ReceiveAsync( receiveBuffer, this.cancellationToken );

					if ( bytesReceived <= 0 ) {
						logger.LogDebug( "Received 0 or less bytes" );
						break;
					}

					ProcessPacket( receiveBuffer, bytesReceived );
				}
			} catch ( OperationCanceledException ) {
				logger.LogDebug( "Receive task cancelled" );
			}

			logger.LogDebug( "Stopped receiving packets" );
		}

		// Processes a received packet
		private void ProcessPacket( byte[] packet, int packetLength ) {
			int protocolVersion = SnmpPacket.GetProtocolVersion( packet, packetLength );

			// Version 1
			if ( protocolVersion == ( int ) SnmpVersion.Ver1 ) {
				SnmpV1TrapPacket snmpPacket = new();
				snmpPacket.decode( packet, packetLength );

				logger.LogDebug( "SNMPv1 TRAP" );
				logger.LogDebug( "\tTrap Generic: '{0}'", snmpPacket.Pdu.Generic );
				logger.LogDebug( "\tTrap Specific: '{0}'", snmpPacket.Pdu.Specific );
				logger.LogDebug( "\tAgent Address: '{0}'", snmpPacket.Pdu.AgentAddress );
				logger.LogDebug( "\tTimestamp: '{0}'", snmpPacket.Pdu.TimeStamp );
				logger.LogDebug( "\tVarBind Count: '{0}' ({1})", snmpPacket.Pdu.VbCount, snmpPacket.Pdu.VbList.Count );
				foreach ( Vb varBind in snmpPacket.Pdu.VbList ) {
					logger.LogDebug( "\t\t{0} ({1}) = '{2}'", varBind.Oid.ToString(), SnmpConstants.GetTypeName( varBind.Value.Type ), varBind.Value.ToString() );
				}

			// Version 2
			} else if ( protocolVersion == ( int ) SnmpVersion.Ver2 ) {
				SnmpV2Packet snmpPacket = new();
				snmpPacket.decode( packet, packetLength );

				if ( snmpPacket.Pdu.Type != PduType.V2Trap ) {
					logger.LogWarning( "Received packet with unknown type: '{0}'", snmpPacket.Pdu.Type );
					return;
				}

				logger.LogDebug( "SNMPv2 TRAP" );
				logger.LogDebug( "\tCommunity: '{0}'", snmpPacket.Community );
				logger.LogDebug( "\tVarBind Count: '{0}' ({1})", snmpPacket.Pdu.VbCount, snmpPacket.Pdu.VbList.Count );
				foreach ( Vb varBind in snmpPacket.Pdu.VbList ) {
					logger.LogDebug( "\t\t{0} ({1}) = '{2}'", varBind.Oid.ToString(), SnmpConstants.GetTypeName( varBind.Value.Type ), varBind.Value.ToString() );
				}

			} else logger.LogWarning( "Received packet with unknown protocol version: '{0}'", protocolVersion );
		}

	}

}
