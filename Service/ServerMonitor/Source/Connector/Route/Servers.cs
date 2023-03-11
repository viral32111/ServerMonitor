using System;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;
using viral32111.JsonExtensions;

namespace ServerMonitor.Connector.Route {

	// Route request handlers for /servers
	public static class Servers {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Servers" );

		// Returns a list of all servers
		[ Route( "GET", "/servers" ) ]
		public static async Task<HttpListenerResponse> OnGetRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {

			// Fetch information about all servers that have ever had an uptime scraped
			Dictionary<string, JsonObject> servers = await Helper.Prometheus.GetServerInformation( configuration, "server_monitor_uptime_seconds" );
			foreach ( KeyValuePair<string, JsonObject> server in servers ) server.Value.Add( "uptimeSeconds", -1 ); // Default to offline

			// Loop through the online servers that have had an uptime scraped recently
			JsonObject recentServers = await Helper.Prometheus.Query( configuration, "server_monitor_uptime_seconds" );
			foreach ( JsonObject? server in recentServers.NestedGet<JsonArray>( "result" ) ) {
				if ( server == null ) throw new Exception( "Null object found in list of recent servers from Prometheus API" );

				// Get this server's IP address
				string address = server.NestedGet<string>( "metric.instance" );

				// Sometimes Prometheus gives back empty results, so skip those...
				if ( server.NestedHas( "metric.name" ) == false ) {
					logger.LogWarning( "Server '{0}' is missing name label! Skipping...", address );
					continue;
				}

				// Get this server's hostname
				string name = server.NestedGet<string>( "metric.name" );

				// Generate the ID for this server based on the address & name
				string identifier = Hash.SHA1( $"{ address }-{ name }" );

				// Parse the uptime
				JsonArray value = server.NestedGet<JsonArray>( "value" );
				if ( value.Count != 2 ) throw new Exception( $"Invalid number of values '{ value.Count }' (expected 2) in list of recent servers from Prometheus API" );
				if ( double.TryParse( value[ 1 ]!.AsValue().GetValue<string>(), out double uptimeSeconds ) == false ) throw new Exception( $"Failed to parse uptime '{ value[ 1 ]!.AsValue().GetValue<string>() }' from Prometheus API query for server uptime" );

				// Get when this server was last scraped
				DateTimeOffset lastScrape = await Helper.Prometheus.GetTargetLastScrape( configuration, address );

				// Get this server's information
				servers.TryGetValue( identifier, out JsonObject? serverInformation );
				if ( serverInformation == null ) {
					logger.LogWarning( "Server '{0}' is missing from the list of all servers! Skipping...", address );
					continue;
				}

				// Update this server's information
				serverInformation[ "uptimeSeconds" ] = uptimeSeconds;
				serverInformation[ "lastUpdate" ] = lastScrape.ToUnixTimeSeconds();

			}

			// Convert the dictionary to a JSON array
			JsonArray serversArray = new();
			foreach ( JsonObject server in servers.Values ) serversArray.Add( server );

			// Return that JSON array
			return Response.SendJson( response, data: serversArray );

		}

	}

}
