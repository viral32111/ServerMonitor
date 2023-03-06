using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

namespace ServerMonitor.Collector {

	// An SNMP manager to fetch agent information & listen for traps
	public class SNMP : Base {

		private readonly ILogger logger = Logging.CreateLogger( "Collector/SNMP" );

		// Properties used across methods
		private readonly Config configuration;
		private readonly CancellationToken cancellationToken;
		private readonly Socket udpSocket = new( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
		private Task? receiveTask = null;

		// The exported Prometheus metrics
		public readonly Counter TrapsReceived;
		public readonly Counter UptimeSeconds;
		public readonly Gauge ServiceCount;

		// Initializes the properties & exported Prometheus metrics
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

		// Updates the exported Prometheus metrics for each configured SNMP agent
		public override void Update() {
			foreach ( SNMPAgent snmpAgent in configuration.SNMPAgents ) UpdateAgentMetrics( snmpAgent.Address, snmpAgent.Port );
			logger.LogDebug( "Updated Prometheus metrics" );
		}

		// Starts listening for incoming SNMP traps
		public void ListenForTraps() {
			if ( this.receiveTask != null ) throw new InvalidOperationException( "Already listening for SNMP traps" );

			// Bind the socket to the configured address & port
			IPAddress listenAddress = configuration.SNMPManagerListenAddress == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse( configuration.SNMPManagerListenAddress );
			int listenPort = configuration.SNMPManagerListenPort;
			udpSocket.Bind( new IPEndPoint( listenAddress, listenPort ) );
			logger.LogInformation( "SNMP manager listening on {0}:{1}", configuration.SNMPManagerListenAddress, configuration.SNMPManagerListenPort );

			// Start the background task for receiving trap packets
			this.receiveTask = ReceivePackets();
		}

		// Waits for the receive background task to finish
		public void WaitForTrapListener() {
			if ( this.receiveTask == null ) throw new InvalidOperationException( "Not yet listening for SNMP traps" );
			this.receiveTask.Wait();
		}

		// Updates the exported Prometheus metrics for an SNMP agent
		private void UpdateAgentMetrics( string agentAddress, int agentPort ) {
			
			// Fetch the required information from the agent
			Dictionary<string, string?> agentInformation = FetchAgentInformation( agentAddress, agentPort, new() {
				"1.3.6.1.2.1.1.1.0", // SNMPv2-MIB::sysDescr.0 (Description)
				"1.3.6.1.2.1.1.2.0", // SNMPv2-MIB::sysObjectID.0 (Object ID)
				"1.3.6.1.2.1.1.3.0", // DISMAN-EVENT-MIB::sysUpTimeInstance (Uptime)
				"1.3.6.1.2.1.1.4.0", // SNMPv2-MIB::sysContact.0 (Contact)
				"1.3.6.1.2.1.1.5.0", // SNMPv2-MIB::sysName.0 (Name)
				"1.3.6.1.2.1.1.6.0", // SNMPv2-MIB::sysLocation.0 (Location)
				"1.3.6.1.2.1.1.7.0" // SNMPv2-MIB::sysServices.0 (Service Count)
			} );

			// Ensure all of the information returned is valid
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.1.0", out string? description ) == false || string.IsNullOrWhiteSpace( description ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned description that is null, empty or whitespace" );
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.2.0", out string? objectID ) == false || string.IsNullOrWhiteSpace( objectID ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned object ID that is null, empty or whitespace" );
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.3.0", out string? uptimeText ) == false || string.IsNullOrWhiteSpace( uptimeText ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned uptime that is null, empty or whitespace" );
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.4.0", out string? contact ) == false || string.IsNullOrWhiteSpace( contact ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned contact that is null, empty or whitespace" );
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.5.0", out string? name ) == false || string.IsNullOrWhiteSpace( name ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned name that is null, empty or whitespace" );
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.6.0", out string? location ) == false || string.IsNullOrWhiteSpace( location ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned location that is null, empty or whitespace" );
			if ( agentInformation.TryGetValue( "1.3.6.1.2.1.1.7.0", out string? serviceCount ) == false || string.IsNullOrWhiteSpace( serviceCount ) ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned service count that is null, empty or whitespace" );

			// Parse SNMP uptime string into seconds
			Match uptimeMatch = Regex.Match( uptimeText, @"^(\d+)d (\d+)h (\d+)m (\d+)s (\d+)ms$" );
			if ( uptimeMatch.Success == false ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned uptime '{ uptimeText }' in an unexpected format" );
			if ( int.TryParse( uptimeMatch.Groups[ 1 ].Value, out int uptimeDays ) == false ) throw new Exception( $"Failed to parse uptime days '{ uptimeMatch.Groups[ 1 ].Value }' as an integer" );
			if ( int.TryParse( uptimeMatch.Groups[ 2 ].Value, out int uptimeHours ) == false ) throw new Exception( $"Failed to parse uptime hours '{ uptimeMatch.Groups[ 2 ].Value }' as an integer" );
			if ( int.TryParse( uptimeMatch.Groups[ 3 ].Value, out int uptimeMinutes ) == false ) throw new Exception( $"Failed to parse uptime minutes '{ uptimeMatch.Groups[ 3 ].Value }' as an integer" );
			if ( int.TryParse( uptimeMatch.Groups[ 4 ].Value, out int uptimeSeconds ) == false ) throw new Exception( $"Failed to parse uptime seconds '{ uptimeMatch.Groups[ 4 ].Value }' as an integer" );
			if ( int.TryParse( uptimeMatch.Groups[ 5 ].Value, out int uptimeMilliseconds ) == false ) throw new Exception( $"Failed to parse uptime milliseconds '{ uptimeMatch.Groups[ 5 ].Value }' as an integer" );
			TimeSpan uptime = new( uptimeDays, uptimeHours, uptimeMinutes, uptimeSeconds, uptimeMilliseconds );

			// Update the exported Prometheus metrics
			UptimeSeconds.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).IncTo( uptime.TotalSeconds );
			ServiceCount.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).IncTo( int.Parse( serviceCount ) );
			logger.LogInformation( "Updated Prometheus metrics for SNMP agent '{0}:{1}'", agentAddress, agentPort );

		}

		// Fetches information from an SNMP agent - https://snmpsharpnet.com/index.php/snmp-version-1-or-2c-get-request/
		private Dictionary<string, string?> FetchAgentInformation( string agentAddress, int agentPort, List<string> oids ) {

			// Info about the request to the agent
			AgentParameters managerParameters = new( SnmpVersion.Ver1, new OctetString( configuration.SNMPCommunity ) );

			// Create the PDU containing the OIDs to fetch values for
			Pdu pdu = new( PduType.Get );
			foreach ( string oid in oids ) pdu.VbList.Add( oid );

			// Send the request to the agent (timeout after 2 seconds)
			using ( UdpTarget targetAgent = new( IPAddress.Parse( agentAddress ), agentPort, 2000, 1 ) ) {
				SnmpV1Packet agentResponse = ( SnmpV1Packet ) targetAgent.Request( pdu, managerParameters );
				if ( agentResponse == null ) throw new Exception( $"No response from SNMP agent '{ agentAddress }:{ agentPort }'" );
				if ( agentResponse.Pdu.ErrorStatus != 0 ) throw new Exception( $"SNMP agent '{ agentAddress }:{ agentPort }' returned error status '{ agentResponse.Pdu.ErrorStatus }'" );

				// Print the response for debugging
				foreach ( Vb varBinding in agentResponse.Pdu.VbList ) logger.LogDebug( "SNMP agent '{0}:{1}' returned '{0}' ({1}) = '{2}'", agentAddress, agentPort, varBinding.Oid.ToString(), SnmpConstants.GetTypeName( varBinding.Value.Type ), varBinding.Value.ToString() );

				// Convert the response to a dictionary of OIDs and their values
				return agentResponse.Pdu.VbList.ToDictionary(
					vb => vb.Oid.ToString(),
					vb => vb.Value.ToString()
				);
			}

		}

		// Runs in the background to receive trap packets
		private async Task ReceivePackets() {

			// Disable timing out when waiting to receive packets
			udpSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 0 );

			// Create a buffer to hold the received packet
			byte[] receiveBuffer = new byte[ 65565 ];

			// Loop forever until cancelled
			try {
				while ( udpSocket.IsBound && this.cancellationToken.IsCancellationRequested == false ) {

					// Block until a packet is received
					int bytesReceived = await udpSocket.ReceiveAsync( receiveBuffer, this.cancellationToken );
					if ( bytesReceived <= 0 ) break;

					// Handle the received packet
					ProcessTrapPacket( receiveBuffer, bytesReceived );

					// Clear the buffer
					Array.Clear( receiveBuffer, 0, receiveBuffer.Length );

				}
			} catch ( OperationCanceledException ) {
				logger.LogInformation( "Cancelled listening for traps" );
			}

		}

		// Handles a received trap packet - https://snmpsharpnet.com/index.php/receive-snmp-version-1-and-2c-trap-notifications/
		private void ProcessTrapPacket( byte[] packet, int packetLength ) {
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

				// TODO: TrapsReceived.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).Inc();

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

				// TODO: TrapsReceived.WithLabels( agentAddress, agentPort.ToString(), name, description, contact, location ).Inc();

			} else logger.LogWarning( "Received packet with unknown protocol version: '{0}'", protocolVersion );

		}

	}

}
