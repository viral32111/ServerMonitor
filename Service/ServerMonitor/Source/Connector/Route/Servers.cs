using System;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;

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

			// Get the first result from the result array from the query response
			if ( serverUptimes.TryGetPropertyValue( "result", out JsonNode? resultNode ) == false || resultNode == null ) throw new Exception( "No result property in Prometheus API query for server uptime" );
			JsonArray results = resultNode.AsArray();

			// Loop through each result in that array...
			foreach ( JsonObject? result in results ) {
				if ( result == null ) throw new Exception( "Null object in Prometheus API query for server uptime" );

				// Get the labels object on the metric
				if ( result.TryGetPropertyValue( "metric", out JsonNode? metricNode ) == false || metricNode == null ) throw new Exception( "No metric property in Prometheus API query for server uptime" );
				JsonObject labels = metricNode.AsObject();

					// Get the target's address (server IP address & port)
					if ( labels.TryGetPropertyValue( "instance", out JsonNode? instanceNode ) == false || instanceNode == null ) throw new Exception( "No instance property in Prometheus API query for server uptime" );
					string targetAddress = instanceNode.AsValue().GetValue<string>();

					// Get the target's hostname
					if ( labels.TryGetPropertyValue( "name", out JsonNode? nameNode ) == false || nameNode == null ) {
						logger.LogWarning( "No name property in Prometheus API query for server uptime! Skipping..." );
						continue;
					}
					string targetName = nameNode.AsValue().GetValue<string>();

				// Get the value array on the result
				if ( result.TryGetPropertyValue( "value", out JsonNode? valueNode ) == false || valueNode == null ) throw new Exception( "No value property in Prometheus API query for server uptime" );
				JsonArray value = valueNode.AsArray();
				if ( value.Count != 2 ) throw new Exception( $"Invalid number of values '{ value.Count }' in Prometheus API query for server uptime" );

					// Get the uptime & when it was last scraped
					if ( double.TryParse( value[ 1 ]!.AsValue().GetValue<string>(), out double uptimeSeconds ) == false ) throw new Exception( $"Failed to parse uptime '{ value[ 1 ]!.AsValue().GetValue<string>() }' from Prometheus API query for server uptime" );
					DateTimeOffset lastUpdate = DateTimeOffset.FromUnixTimeSeconds( ( long ) Math.Round( value[ 0 ]!.AsValue().GetValue<double>(), 0 ) );

				// Generate the ID for this server based on the address & name
				string serverIdentifier = Hash.SHA1( $"{ targetAddress }-{ targetName }" );

				// Add this server's information to the array
				servers.Add( new JsonObject() {
					{ "id", serverIdentifier },
					{ "name", targetName },
					{ "address", targetAddress },
					{ "uptimeSeconds", uptimeSeconds },
					{ "lastUpdate", lastUpdate.ToUnixTimeSeconds() }
				} );

			}

			// Return the list of servers
			return Response.SendJson( response, data: servers );

		}

	}

}
