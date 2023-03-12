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

		// Creates a Prometheus query with labels
		private static string CreatePromQL( string metric, Dictionary<string, string> labels ) => string.Concat( metric, "{", string.Join( ",", labels.Select( pair => $"{ pair.Key }=\"{ pair.Value }\"" ) ), "}" );

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

		// Gets the date & time a target was last scraped
		public static async Task<DateTimeOffset> GetTargetLastScrape( Config configuration, string targetAddress ) {

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

		// Gets information for all servers or a specific server
		public static async Task<Dictionary<string, JsonObject>> GetServerInformation( Config configuration, string metric, string? desiredIdentifier = null ) {

			// Create the dictionary to store the server information
			Dictionary<string, JsonObject> information = new();

			// Loop through all the servers that have ever had the given metric scraped...
			JsonArray allServers = await Helper.Prometheus.Series( configuration, metric );
			foreach ( JsonObject? server in allServers ) {
				if ( server == null ) throw new Exception( "Null object found in list of all servers from Prometheus API" );

				// Get this server's configured job name
				string jobName = server.NestedGet<string>( "job" );

				// Get this server's configured IP address & port
				string instanceAddress = server.NestedGet<string>( "instance" );

				// Sometimes Prometheus gives back empty results, so skip those...
				if ( server.NestedHas( "name" ) == false ) {
					logger.LogWarning( "Server '{0}' is missing name label! Skipping...", instanceAddress );
					continue;
				}

				// Get this server's hostname
				string name = server.NestedGet<string>( "name" );

				// Generate the ID for this server based on the address & name
				string identifier = Hash.SHA1( $"{ instanceAddress }-{ name }" );

				// Skip this server if it's not the one we're looking for
				if ( desiredIdentifier != null && identifier != desiredIdentifier ) continue;

				// Get when this server was last scraped
				DateTimeOffset lastScrape = await Helper.Prometheus.GetTargetLastScrape( configuration, instanceAddress );

				// Add this server's information
				information.Add( identifier, new() {
					{ "id", identifier },
					{ "name", name },
					{ "instance", instanceAddress },
					{ "job", jobName },
					{ "lastUpdate", lastScrape.ToUnixTimeSeconds() }
				} );

			}

			// Return the information
			return information;

		}

		// Gets all the partitions for a drive on a server
		public static async Task<JsonObject[]> GetPartitions( Config configuration, string instanceAddress, string jobName, string driveName ) {

			// Gets the mountpoints for all known parttions ( Partition Name -> Mountpoint )
			Dictionary<string, string> partitionMountpoints = ( await Series( configuration, CreatePromQL( "server_monitor_resource_drive_total_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.Where( partition => partition != null )
				.Select( partition => partition!.AsObject() )
				.Where( partition => partition.NestedHas( "mountpoint" ) == true )
				.Where( partition => partition.NestedHas( "partition" ) == true )
				.Where( partition => partition.NestedGet<string>( "partition" ).StartsWith( driveName ) )
				.ToDictionary(
					partition => partition.NestedGet<string>( "partition" ),
					partition => partition.NestedGet<string>( "mountpoint" )
				);

			// Gets the total bytes for recently scraped partitions ( Partition Name -> Total Bytes )
			Dictionary<string, long> partitionTotalBytes = ( await Query( configuration, CreatePromQL( "server_monitor_resource_drive_total_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( partition => partition != null )
				.Select( partition => partition!.AsObject() )
				.Where( partition => partition.NestedHas( "metric" ) == true )
				.Where( partition => partition.NestedHas( "metric.mountpoint" ) == true )
				.Where( partition => partition.NestedHas( "metric.partition" ) == true )
				.Where( partition => partition.NestedGet<string>( "metric.partition" ).StartsWith( driveName ) )
				.Where( partition => partition.NestedHas( "value" ) == true )
				.ToDictionary(
					partition => partition.NestedGet<string>( "metric.partition" ),
					partition => long.Parse( partition.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Gets the total bytes for recently scraped partitions ( Partition Name -> Free Bytes )
			Dictionary<string, long> partitionFreeBytes = ( await Query( configuration, CreatePromQL( "server_monitor_resource_drive_free_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( partition => partition != null )
				.Select( partition => partition!.AsObject() )
				.Where( partition => partition.NestedHas( "metric" ) == true )
				.Where( partition => partition.NestedHas( "metric.mountpoint" ) == true )
				.Where( partition => partition.NestedHas( "metric.partition" ) == true )
				.Where( partition => partition.NestedGet<string>( "metric.partition" ).StartsWith( driveName ) )
				.Where( partition => partition.NestedHas( "value" ) == true )
				.ToDictionary(
					partition => partition.NestedGet<string>( "metric.partition" ),
					partition => long.Parse( partition.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Combine the data into an array of JSON objects - Partitions that have not been recently scraped will have -1 for totalBytes & freeBytes
			return partitionMountpoints.Aggregate( new List<JsonObject>(), ( partitions, pair ) => {
				partitions.Add( new() {
					{ "name", pair.Key },
					{ "mountpoint", pair.Value },
					{ "totalBytes", partitionTotalBytes.ContainsKey( pair.Key ) == true ? partitionTotalBytes[ pair.Key ] : - 1 },
					{ "freeBytes", partitionFreeBytes.ContainsKey( pair.Key ) == true ? partitionFreeBytes[ pair.Key ] : - 1 }
				} );

				return partitions;
			} ).ToArray();

		}

		// Gets all the drives on a server
		public static async Task<JsonObject[]> GetDrives( Config configuration, string instanceAddress, string jobName ) {

			// Gets the names of all known drives
			string[] driveNames = ( await Series( configuration, CreatePromQL( "server_monitor_resource_drive_health", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive => drive.NestedHas( "drive" ) == true )
				.Select( drive => drive.NestedGet<string>( "drive" ) )
				.ToArray();

			// Gets the bytes read for recently scraped drives ( Drive Name -> Bytes Read )
			Dictionary<string, long> driveBytesRead = ( await Query( configuration, CreatePromQL( "server_monitor_resource_drive_read_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive => drive.NestedHas( "metric" ) == true )
				.Where( drive => drive.NestedHas( "metric.drive" ) == true )
				.Where( drive => drive.NestedHas( "value" ) == true )
				.ToDictionary(
					drive => drive.NestedGet<string>( "metric.drive" ),
					drive => long.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Gets the bytes written for recently scraped drives ( Drive Name -> Bytes Written )
			Dictionary<string, long> driveBytesWritten = ( await Query( configuration, CreatePromQL( "server_monitor_resource_drive_write_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive => drive.NestedHas( "metric" ) == true )
				.Where( drive => drive.NestedHas( "metric.drive" ) == true )
				.Where( drive => drive.NestedHas( "value" ) == true )
				.ToDictionary(
					drive => drive.NestedGet<string>( "metric.drive" ),
					drive => long.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Gets the health for recently scraped drives ( Drive Name -> Health )
			Dictionary<string, int> driveHealth = ( await Query( configuration, CreatePromQL( "server_monitor_resource_drive_health", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive => drive.NestedHas( "metric" ) == true )
				.Where( drive => drive.NestedHas( "metric.drive" ) == true )
				.Where( drive => drive.NestedHas( "value" ) == true )
				.ToDictionary(
					drive => drive.NestedGet<string>( "metric.drive" ),
					drive => int.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Gets the partitions for each drive
			Dictionary<string, JsonArray> drivePartitions = new();
			foreach ( string driveName in driveNames ) drivePartitions.Add( driveName, JSON.CreateJsonArray( await GetPartitions( configuration, instanceAddress, jobName, driveName ) ) );

			// Combine the data into an array of JSON objects - Drives that have not been recently scraped will have -1 for bytesRead, bytesWritten & health
			return driveNames.Aggregate( new List<JsonObject>(), ( drives, driveName ) => {
				drives.Add( new() {
					{ "name", driveName },
					{ "health", driveHealth.ContainsKey( driveName ) == true ? driveHealth[ driveName ] : - 1 },
					{ "bytesRead", driveBytesRead.ContainsKey( driveName ) == true ? driveBytesRead[ driveName ] : - 1 },
					{ "bytesWritten", driveBytesWritten.ContainsKey( driveName ) == true ? driveBytesWritten[ driveName ] : - 1 },
					{ "partitions", drivePartitions.ContainsKey( driveName ) == true ? drivePartitions[ driveName ] : new JsonArray() }
				} );

				return drives;
			} ).ToArray();

		}

	}

}
