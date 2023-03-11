using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using viral32111.JsonExtensions;

// https://prometheus.io/docs/prometheus/latest/querying/api/

namespace ServerMonitor.Connector.Helper {

	// Helper class to encapsulate Prometheus API calls
	public static class Prometheus {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Helper/Prometheus" );

		// Creates query string from a dictionary of parameters
		private static string CreateQueryString( Dictionary<string, string?> parameters ) => string.Join( "&", parameters.Select( pair => $"{ pair.Key }={ Uri.EscapeDataString( pair.Value ?? "" ) }" ) );

		// Sends a request to the Prometheus API
		public static async Task<T> Request<T>( Config configuration, string path, Dictionary<string, string?>? parameters = null ) {

			// Construct the base URL & query string parameter
			string baseUrl = $"{ ( configuration.PrometheusAPIPort == 443 ? "https" : "http" ) }://{ configuration.PrometheusAPIAddress }:{ configuration.PrometheusAPIPort }/api/v{ configuration.PrometheusAPIVersion }";
			string queryString = CreateQueryString( parameters ?? new() );

			// Create the HTTP request
			HttpRequestMessage httpRequest = new() {
				Method = HttpMethod.Get,
				RequestUri = new Uri( $"{ baseUrl }/{ path }?{ queryString }" )
			};

			// Send the HTTP request & wait for the response
			logger.LogDebug( "Sending API request '{0}' '{1}' to Prometheus", httpRequest.Method, httpRequest.RequestUri );
			HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest );
			httpResponse.EnsureSuccessStatusCode();

			// Decode the response as JSON
			Stream httpResponseContentStream = await httpResponse.Content.ReadAsStreamAsync();
			JsonObject? httpResponsePayload = await JsonSerializer.DeserializeAsync<JsonObject>( httpResponseContentStream );
			if ( httpResponsePayload == null ) throw new Exception( "Failed to deserialize Prometheus API JSON response" );

			// Ensure the required properties exist
			if ( httpResponsePayload.TryGetPropertyValue( "status", out JsonNode? statusTextNode ) == false || statusTextNode == null ) throw new Exception( "Prometheus API JSON response does not contain status property" );
			if ( httpResponsePayload.TryGetPropertyValue( "data", out JsonNode? dataNode ) == false || dataNode == null ) throw new Exception( "Prometheus API JSON response does not contain data property" );

			// Ensure the response was successful
			string statusText = statusTextNode.AsValue().GetValue<string>();
			if ( statusText != "success" ) throw new Exception( $"Prometheus API response was unsuccessful: '{ statusText }'" );

			// Return the data as the requested type
			if ( typeof( T ) == typeof( JsonArray ) ) return ( T ) ( object ) dataNode.AsArray();
			else if ( typeof( T ) == typeof( JsonObject ) ) return ( T ) ( object ) dataNode.AsObject();
			else return dataNode.AsValue().GetValue<T>();
		}

		// Sends a generic query to the Prometheus API
		public static Task<JsonObject> Query( Config configuration, string query ) => Request<JsonObject>( configuration, "query", new Dictionary<string, string?>() {
			{ "query", query },
		} );

		// Sends a series query to the Prometheus API
		public static Task<JsonArray> Series( Config configuration, string match ) => Request<JsonArray>( configuration, "series", new Dictionary<string, string?>() {
			{ "match[]", match },
		} );

		// Sends a targets query to the Prometheus API
		public static Task<JsonObject> Targets( Config configuration ) => Request<JsonObject>( configuration, "targets" );

		// Gets the last date & time a target was scraped
		public static async Task<DateTimeOffset> GetTargetLastUpdate( Config configuration, string targetAddress ) {

			// Fetch all the configured targets
			JsonObject targets = await Helper.Prometheus.Targets( configuration );

			// Loop through all the active targets...
			foreach ( JsonObject? activeTarget in targets.NestedGet<JsonArray>( "activeTargets" ) ) {
				if ( activeTarget == null ) throw new Exception( "Null object found in list of active targets from Prometheus API" );

				// Get this target's address
				string address = activeTarget.NestedGet<string>( "labels.instance" );

				// Skip this target if it's not the one we're looking for
				if ( address != targetAddress ) continue;

				// Parse this target's last scrape timestamp - https://stackoverflow.com/a/36314187
				if ( DateTimeOffset.TryParse( activeTarget.NestedGet<string>( "lastScrape" ), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset lastScrape ) == false ) throw new Exception( $"Failed to parse active target last scrape timestamp: '{ activeTarget.NestedGet<string>( "lastScrape" ) }'" );

				// Return the last scrape timestamp
				return lastScrape;

			}

			// If we got here then we failed to find the target
			throw new Exception( $"No active target with address '{ targetAddress }'" );

		}
	
	}

}
