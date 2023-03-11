using System;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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

			// Create an array to hold information about each server
			JsonArray servers = new();

			// Query Prometheus for all server uptimes
			JsonObject serverUptimes = await Helper.Prometheus.Query( configuration, "server_monitor_uptime_seconds" );

			// Loop through the results (i.e., servers with uptimes) from the query response
			foreach ( JsonObject? server in serverUptimes.NestedGet<JsonArray>( "result" ) ) {
				if ( server == null ) throw new Exception( "Null object in Prometheus API query for server uptime" );

				// Get the target's address
				string address = server.NestedGet<string>( "metric.instance" );

				// Sometimes Prometheus gives back empty results, so skip those...
				if ( server.NestedHas( "metric.name" ) == false ) {
					logger.LogWarning( $"Result (server) '{ address }' is missing name label! Skipping..." );
					continue;
				}

				// Get the target's hostname
				string name = server.NestedGet<string>( "metric.name" );

				// Get the uptime & when it was last scraped
				JsonArray value = server.NestedGet<JsonArray>( "value" );
				if ( value.Count != 2 ) throw new Exception( $"Invalid number of values '{ value.Count }' in Prometheus API query for server uptime" );
				if ( double.TryParse( value[ 1 ]!.AsValue().GetValue<string>(), out double uptimeSeconds ) == false ) throw new Exception( $"Failed to parse uptime '{ value[ 1 ]!.AsValue().GetValue<string>() }' from Prometheus API query for server uptime" );
				DateTimeOffset lastUpdate = DateTimeOffset.FromUnixTimeSeconds( ( long ) Math.Round( value[ 0 ]!.AsValue().GetValue<double>(), 0 ) );

				// Generate the ID for this server based on the address & name
				string serverIdentifier = Hash.SHA1( $"{ address }-{ name }" );

				// Add this server's information to the array
				servers.Add( new JsonObject() {
					{ "id", serverIdentifier },
					{ "name", name },
					{ "address", address },
					{ "uptimeSeconds", uptimeSeconds },
					{ "lastUpdate", lastUpdate.ToUnixTimeSeconds() }
				} );

			}

			// Return the list of servers
			return Response.SendJson( response, data: servers );

		}

	}

}
