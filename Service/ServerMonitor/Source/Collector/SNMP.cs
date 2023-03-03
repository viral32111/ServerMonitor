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
	public static class SNMP {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/SNMP" );

		// TODO: Define & initialize the exported Prometheus metrics

		// TODO: This will start the SNMP agent
		public static async Task StartAgent( Config configuration, CancellationToken cancellationToken ) {
			throw new NotImplementedException();
		}

		// TODO: This will run in the background to receive SNMP packets
		private static async Task ReceivePackets() {
			throw new NotImplementedException();
		}

	}

}
