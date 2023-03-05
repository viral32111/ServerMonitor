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

/* https://blog.domotz.com/know-your-networks/snmp-port-number/
UDP port 161 connects the SNMP Managers with SNMP Agents (i.e. polling)
UDP port 162 sees use when SNMP Agents send unsolicited traps to the SNMP Manager
*/

// https://snmpsharpnet.com/index.php/receive-snmp-version-1-and-2c-trap-notifications/
// https://snmpsharpnet.com/

namespace ServerMonitor.Collector {

	// An SNMP agent to collect traps/events
	public class SNMP {

		private readonly ILogger logger = Logging.CreateLogger( "Collector/SNMP" );

		private readonly Config configuration;
		private readonly CancellationToken cancellationToken;
		private readonly Socket udpSocket = new( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
		private Task? receiveTask = null;

		// TODO: Define & initialize the exported Prometheus metrics

		// Initializes the socket
		public SNMP( Config configuration, CancellationToken cancellationToken ) {
			this.configuration = configuration;
			this.cancellationToken = cancellationToken;

			IPAddress listenAddress = configuration.SNMPAgentListenAddress == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse( configuration.SNMPAgentListenAddress );
			int listenPort = configuration.SNMPAgentListenPort;
			udpSocket.Bind( new IPEndPoint( listenAddress, listenPort ) );
			logger.LogDebug( "Listening for UDP packets on {0}:{1}", configuration.SNMPAgentListenAddress, configuration.SNMPAgentListenPort );

			udpSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 0 ); // Never timeout
			logger.LogDebug( "Disabled timeout on socket" );
		}

		// Starts the receive packets background task
		public void StartAgent() {
			if ( this.receiveTask != null ) throw new InvalidOperationException( "SNMP agent already started" );

			logger.LogDebug( "Starting to receive packets..." );
			this.receiveTask = ReceivePackets();
		}

		public void WaitForAgent() {
			if ( this.receiveTask == null ) throw new InvalidOperationException( "SNMP agent not started" );

			logger.LogDebug( "Waiting for receive task to finish..." );
			this.receiveTask.Wait();
			logger.LogDebug( "Receive task finished" );
		}

		// Runs in the background to receive packets
		private async Task ReceivePackets() {
			byte[] receiveBuffer = new byte[ 65565 ];

			logger.LogDebug( "Started receiving packets" );
			while ( udpSocket.IsBound && this.cancellationToken.IsCancellationRequested == false ) {
				int bytesReceived = await udpSocket.ReceiveAsync( receiveBuffer, this.cancellationToken );

				if ( bytesReceived <= 0 ) {
					logger.LogDebug( "Received 0 or less bytes" );
					break;
				}

				ProcessPacket( receiveBuffer, bytesReceived );
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
