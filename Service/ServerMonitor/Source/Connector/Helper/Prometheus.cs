using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

		// Encode/decode (mostly) unique identifiers for a server
		public static string EncodeIdentifier( string jobName, string instanceAddress ) => Base64.EncodeURLSafe( string.Concat( jobName, ";", instanceAddress ) );
		public static string[] DecodeIdentifier( string identifier ) => Base64.DecodeURLSafe( identifier ).Split( ';', 2 );

		// Sends a raw request to the Prometheus API
		public static async Task<T> Fetch<T>( Config configuration, string path, Dictionary<string, string?>? parameters = null ) {

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

		// Convinence methods for sending different types of queries to the Prometheus API
		public static Task<JsonObject> FetchQuery( Config configuration, string query ) => Fetch<JsonObject>( configuration, "query", new Dictionary<string, string?>() { { "query", query } } );
		public static Task<JsonArray> FetchSeries( Config configuration, string match ) => Fetch<JsonArray>( configuration, "series", new Dictionary<string, string?>() { { "match[]", match } } );
		public static Task<JsonObject> FetchTargets( Config configuration ) => Fetch<JsonObject>( configuration, "targets" );

		// Fetches all servers
		public static async Task<JsonObject[]> FetchServers( Config configuration ) {

			// Fetch all the actively configured targets (does not mean they are online, just that they are in the scrape pool)
			Dictionary<string, JsonObject> activeTargets = ( await FetchTargets( configuration ) )
				.NestedGet<JsonArray>( "activeTargets" )
				.Where( target => target != null )
				.Select( target => target!.AsObject() )
				.Where( target =>
					target.NestedHas( "labels" ) == true &&
					target.NestedHas( "labels.job" ) == true &&
					target.NestedHas( "labels.instance" ) == true &&
					target.NestedHas( "lastScrape" ) == true
				)
				.ToDictionary(
					target => EncodeIdentifier( target.NestedGet<string>( "labels.job" ), target.NestedGet<string>( "labels.instance" ) ),
					target => new JsonObject() {
						{ "jobName", target.NestedGet<string>( "labels.job" ) },
						{ "instanceAddress", target.NestedGet<string>( "labels.instance" ) },
						{ "lastScrape", DateTimeOffset.Parse( target.NestedGet<string>( "lastScrape" ), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal ) } // https://stackoverflow.com/a/36314187
					}
				);

			// Fetch all the known servers (does not mean they are online, just that they have been scraped at least once)
			Dictionary<string, JsonObject> knownServers = ( await FetchSeries( configuration, "server_monitor_uptime_seconds" ) )
				.Where( server => server != null )
				.Select( server => server!.AsObject() )
				.Where( server =>
					server.NestedHas( "job" ) == true &&
					server.NestedHas( "instance" ) == true &&
					server.NestedHas( "name" ) == true &&
					server.NestedHas( "os" ) == true &&
					server.NestedHas( "architecture" ) == true &&
					server.NestedHas( "version" ) == true
				)
				.ToDictionary(
					server => EncodeIdentifier( server.NestedGet<string>( "job" ), server.NestedGet<string>( "instance" ) ),
					server => new JsonObject() {
						{ "hostName", server.NestedGet<string>( "name" ) },
						{ "operatingSystem", server.NestedGet<string>( "os" ) },
						{ "architecture", server.NestedGet<string>( "architecture" ) },
						{ "version", server.NestedGet<string>( "version" ) }
					}
				);

			// Fetch all the recent servers (means they are online)
			Dictionary<string, JsonObject> recentServers = ( await FetchQuery( configuration, "server_monitor_uptime_seconds" ) )
				.NestedGet<JsonArray>( "result" )
				.Where( server => server != null )
				.Select( server => server!.AsObject() )
				.Where( server =>
					server.NestedHas( "metric" ) == true &&
					server.NestedHas( "metric.job" ) == true &&
					server.NestedHas( "metric.instance" ) == true &&
					server.NestedHas( "metric.name" ) == true &&
					server.NestedHas( "metric.os" ) == true &&
					server.NestedHas( "metric.architecture" ) == true &&
					server.NestedHas( "metric.version" ) == true
				)
				.Where( server =>
					server.NestedHas( "value" ) == true && 
					server.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					server => EncodeIdentifier( server.NestedGet<string>( "metric.job" ), server.NestedGet<string>( "metric.instance" ) ),
					server => new JsonObject() {
						{ "hostName", server.NestedGet<string>( "metric.name" ) },
						{ "operatingSystem", server.NestedGet<string>( "metric.os" ) },
						{ "architecture", server.NestedGet<string>( "metric.architecture" ) },
						{ "version", server.NestedGet<string>( "metric.version" ) },
						{ "uptimeSeconds", double.Parse( server.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) }
					}
				);

			// Merge the data into a single array
			return activeTargets.Aggregate( new List<JsonObject>(), ( servers, pair ) => {
				if ( knownServers.ContainsKey( pair.Key ) == false ) return servers; // Skip any targets that are not exported by the Server Monitor collector (i.e., they aren't one of our servers)

				servers.Add( new JsonObject() {
					{ "identifier", pair.Key },

					{ "jobName", activeTargets[ pair.Key ].NestedGet<string>( "jobName" ) },
					{ "instanceAddress", activeTargets[ pair.Key ].NestedGet<string>( "instanceAddress" ) },
					{ "lastScrape", activeTargets[ pair.Key ].NestedGet<DateTimeOffset>( "lastScrape" ) },
					
					{ "hostName", recentServers.ContainsKey( pair.Key ) == true ? recentServers[ pair.Key ].NestedGet<string>( "hostName" ) : knownServers[ pair.Key ].NestedGet<string>( "hostName" ) },
					{ "operatingSystem", recentServers.ContainsKey( pair.Key ) == true ? recentServers[ pair.Key ].NestedGet<string>( "operatingSystem" ) : knownServers[ pair.Key ].NestedGet<string>( "operatingSystem" ) },
					{ "architecture", recentServers.ContainsKey( pair.Key ) == true ? recentServers[ pair.Key ].NestedGet<string>( "architecture" ) : knownServers[ pair.Key ].NestedGet<string>( "architecture" ) },
					{ "version", recentServers.ContainsKey( pair.Key ) == true ? recentServers[ pair.Key ].NestedGet<string>( "version" ) : knownServers[ pair.Key ].NestedGet<string>( "version" ) },

					{ "uptimeSeconds", recentServers.ContainsKey( pair.Key ) == true ? recentServers[ pair.Key ].NestedGet<double>( "uptimeSeconds" ) : -1 }
				} );

				return servers;
			} ).ToArray();

		}

		// Fetches the processor metrics for a server
		public static async Task<JsonObject> FetchProcessor( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the processor usage
			double processorUsage = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_processor_usage", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => double.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Fetch the processor frequency
			double processorFrequency = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_processor_frequency", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => double.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Fetch the processor temperature
			double processorTemperature = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_processor_temperature", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => double.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Return as a JSON object
			return new() {
				{ "usage", processorUsage },
				{ "frequency", processorFrequency },
				{ "temperature", processorTemperature }
			};

		}

		// Fetches the swap/page-file metrics for a server
		public static async Task<JsonObject> FetchSwap( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the total bytes
			long totalBytes = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_memory_swap_total_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Fetch the free bytes
			long freeBytes = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_memory_swap_free_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Return as a JSON object
			return new() {
				{ "totalBytes", totalBytes },
				{ "freeBytes", freeBytes },
			};

		}

		// Fetches the memory metrics for a server
		public static async Task<JsonObject> FetchMemory( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the total bytes
			long totalBytes = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_memory_total_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Fetch the free bytes
			long freeBytes = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_memory_free_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.First();

			// Return as a JSON object
			return new() {
				{ "totalBytes", totalBytes },
				{ "freeBytes", freeBytes },
				{ "swap", await FetchSwap( configuration, jobName, instanceAddress ) }
			};

		}

		// Fetches all the partitions for a drive on a server
		public static async Task<JsonObject[]> FetchDrivePartitions( Config configuration, string instanceAddress, string jobName, string driveName ) {

			// Fetches the mountpoints for all known parttions ( Partition Name -> Mountpoint )
			Dictionary<string, string> partitionMountpoints = ( await FetchSeries( configuration, CreatePromQL( "server_monitor_resource_drive_total_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.Where( partition => partition != null )
				.Select( partition => partition!.AsObject() )
				.Where( partition =>
					partition.NestedHas( "mountpoint" ) == true &&
					partition.NestedHas( "partition" ) == true
				)
				.Where( partition => partition.NestedGet<string>( "partition" ).StartsWith( driveName ) )
				.ToDictionary(
					partition => partition.NestedGet<string>( "partition" ),
					partition => partition.NestedGet<string>( "mountpoint" )
				);

			// Fetches the total bytes for recently scraped partitions ( Partition Name -> Total Bytes )
			Dictionary<string, long> partitionTotalBytes = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_drive_total_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( partition => partition != null )
				.Select( partition => partition!.AsObject() )
				.Where( partition =>
					partition.NestedHas( "metric" ) == true &&
					partition.NestedHas( "metric.mountpoint" ) == true &&
					partition.NestedHas( "metric.partition" ) == true
				)
				.Where( partition =>
					partition.NestedHas( "value" ) == true &&
					partition.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.Where( partition => partition.NestedGet<string>( "metric.partition" ).StartsWith( driveName ) )
				.ToDictionary(
					partition => partition.NestedGet<string>( "metric.partition" ),
					partition => long.Parse( partition.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetches the total bytes for recently scraped partitions ( Partition Name -> Free Bytes )
			Dictionary<string, long> partitionFreeBytes = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_drive_free_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( partition => partition != null )
				.Select( partition => partition!.AsObject() )
				.Where( partition =>
					partition.NestedHas( "metric" ) == true &&
					partition.NestedHas( "metric.mountpoint" ) == true &&
					partition.NestedHas( "metric.partition" ) == true
				)
				.Where( partition =>
					partition.NestedHas( "value" ) == true &&
					partition.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.Where( partition => partition.NestedGet<string>( "metric.partition" ).StartsWith( driveName ) )
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

		// Fetches all the drives on a server
		public static async Task<JsonObject[]> FetchDrives( Config configuration, string instanceAddress, string jobName ) {

			// Fetches the names of all known drives
			string[] driveNames = ( await FetchSeries( configuration, CreatePromQL( "server_monitor_resource_drive_health", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive => drive.NestedHas( "drive" ) == true )
				.Select( drive => drive.NestedGet<string>( "drive" ) )
				.ToArray();

			// Fetches the bytes read for recently scraped drives ( Drive Name -> Bytes Read )
			Dictionary<string, long> driveBytesRead = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_drive_read_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive =>
					drive.NestedHas( "metric" ) == true &&
					drive.NestedHas( "metric.drive" ) == true
				)
				.Where( drive =>
					drive.NestedHas( "value" ) == true &&
					drive.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					drive => drive.NestedGet<string>( "metric.drive" ),
					drive => long.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetches the bytes written for recently scraped drives ( Drive Name -> Bytes Written )
			Dictionary<string, long> driveBytesWritten = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_drive_write_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive =>
					drive.NestedHas( "metric" ) == true &&
					drive.NestedHas( "metric.drive" ) == true
				)
				.Where( drive =>
					drive.NestedHas( "value" ) == true &&
					drive.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					drive => drive.NestedGet<string>( "metric.drive" ),
					drive => long.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetches the health for recently scraped drives ( Drive Name -> Health )
			Dictionary<string, int> driveHealth = ( await FetchQuery( configuration, CreatePromQL( "server_monitor_resource_drive_health", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive =>
					drive.NestedHas( "metric" ) == true &&
					drive.NestedHas( "metric.drive" ) == true
				)
				.Where( drive =>
					drive.NestedHas( "value" ) == true &&
					drive.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					drive => drive.NestedGet<string>( "metric.drive" ),
					drive => int.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetches the partitions for each drive
			Dictionary<string, JsonArray> drivePartitions = new();
			foreach ( string driveName in driveNames ) drivePartitions.Add( driveName, JSON.CreateJsonArray( await FetchDrivePartitions( configuration, instanceAddress, jobName, driveName ) ) );

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
