using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
		private static string CreatePromQL( string metric, Dictionary<string, string> labels, string? function = null, int? duration = null ) {
			string query = string.Concat( metric, "{", string.Join( ",", labels.Select( pair => $"{ pair.Key }=\"{ pair.Value }\"" ) ), "}", ( duration != null ? $"[{duration}s]" : "" ) );
			return ( string.IsNullOrWhiteSpace( function ) == false ) ? $"{ function }({ query })" : query;
		}

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
						{ "lastScrape", DateTimeOffset.Parse( target.NestedGet<string>( "lastScrape" ), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal ).ToUnixTimeSeconds() } // https://stackoverflow.com/a/36314187
					}
				);

			// Fetch all the known servers (does not mean they are online, just that they have been scraped at least once)
			Dictionary<string, JsonObject> knownServers = ( await FetchSeries( configuration, $"{ configuration.PrometheusMetricsPrefix }_uptime_seconds" ) )
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
			Dictionary<string, JsonObject> recentServers = ( await FetchQuery( configuration, $"{ configuration.PrometheusMetricsPrefix }_uptime_seconds" ) )
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

			// Merge the data into an array of JSON objects (offline servers will have -1 for uptimeSeconds)
			return activeTargets.Aggregate( new List<JsonObject>(), ( servers, pair ) => {
				if ( knownServers.ContainsKey( pair.Key ) == false ) return servers; // Skip any targets that are not exported by the Server Monitor collector (i.e., they aren't one of our servers)

				servers.Add( new JsonObject() {
					{ "identifier", pair.Key },

					{ "jobName", activeTargets[ pair.Key ].NestedGet<string>( "jobName" ) },
					{ "instanceAddress", activeTargets[ pair.Key ].NestedGet<string>( "instanceAddress" ) },
					{ "lastScrape", activeTargets[ pair.Key ].NestedGet<long>( "lastScrape" ) },
					
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
			double? processorUsage = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_usage", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => double.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

			// Fetch the processor frequency
			double? processorFrequency = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_frequency", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => double.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

			// Fetch the processor temperature
			double? processorTemperature = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_processor_temperature", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => double.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

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
			long? totalBytes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_swap_total_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

			// Fetch the free bytes
			long? freeBytes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_swap_free_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

			// Return as a JSON object
			return new() {
				{ "totalBytes", totalBytes },
				{ "freeBytes", freeBytes },
			};

		}

		// Fetches the memory metrics for a server
		public static async Task<JsonObject> FetchMemory( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the total bytes
			long? totalBytes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_total_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

			// Fetch the free bytes
			long? freeBytes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_memory_free_bytes", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "value" ) && result.NestedGet<JsonArray>( "value" ).Count == 2 )
				.Select( result => long.Parse( result.NestedGet<JsonArray>( "value" )[ 1 ]!.GetValue<string>() ) )
				.FirstOrDefault();

			// Return as a JSON object
			return new() {
				{ "totalBytes", totalBytes },
				{ "freeBytes", freeBytes },
				{ "swap", await FetchSwap( configuration, jobName, instanceAddress ) }
			};

		}

		// Fetches all the partitions for a drive on a server
		public static async Task<JsonObject[]> FetchDrivePartitions( Config configuration, string jobName, string instanceAddress, string driveName ) {

			// Fetch the mountpoints for all known parttions ( Partition Name -> Mountpoint )
			Dictionary<string, string> partitionMountpoints = ( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_total_bytes", new() {
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

			// Fetch the total bytes for recently scraped partitions ( Partition Name -> Total Bytes )
			Dictionary<string, long> partitionTotalBytes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_total_bytes", new() {
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

			// Fetch the total bytes for recently scraped partitions ( Partition Name -> Free Bytes )
			Dictionary<string, long> partitionFreeBytes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_free_bytes", new() {
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

			// Merge the data into an array of JSON objects (partitions that have not been recently scraped will have -1 for totalBytes & freeBytes)
			return partitionMountpoints.Aggregate( new List<JsonObject>(), ( partitions, pair ) => {
				partitions.Add( new() {
					{ "name", pair.Key },
					{ "mountpoint", pair.Value },
					{ "totalBytes", partitionTotalBytes.ContainsKey( pair.Key ) == true ? partitionTotalBytes[ pair.Key ] : -1 },
					{ "freeBytes", partitionFreeBytes.ContainsKey( pair.Key ) == true ? partitionFreeBytes[ pair.Key ] : -1 }
				} );

				return partitions;
			} ).ToArray();

		}

		// Fetches all the drives on a server
		public static async Task<JsonObject[]> FetchDrives( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the names of all known drives
			string[] driveNames = ( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_health", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.Where( drive => drive != null )
				.Select( drive => drive!.AsObject() )
				.Where( drive => drive.NestedHas( "drive" ) == true )
				.Select( drive => drive.NestedGet<string>( "drive" ) )
				.ToArray();

			// Fetch the total bytes read for recently scraped drives ( Drive Name -> Bytes Read )
			Dictionary<string, long> driveTotalBytesRead = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_read_bytes", new() {
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

			// Fetch the rate of bytes read for recently scraped drives ( Drive Name -> Bytes Read )
			Dictionary<string, double> driveRateBytesRead = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_read_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			}, "rate", configuration.PrometheusScrapeIntervalSeconds * 2 ) ) )
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
					drive => double.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetch the total bytes written for recently scraped drives ( Drive Name -> Bytes Written )
			Dictionary<string, long> driveTotalBytesWritten = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_write_bytes", new() {
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

			// Fetch the rate of bytes written for recently scraped drives ( Drive Name -> Bytes Written )
			Dictionary<string, double> driveRateBytesWritten = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_write_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName },
			}, "rate", configuration.PrometheusScrapeIntervalSeconds * 2 ) ) )
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
					drive => double.Parse( drive.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetch the health for recently scraped drives ( Drive Name -> Health )
			Dictionary<string, int> driveHealth = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_drive_health", new() {
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

			// Fetch the partitions for each drive
			Dictionary<string, JsonArray> drivePartitions = new();
			foreach ( string driveName in driveNames ) drivePartitions.Add( driveName, JSON.CreateJsonArray( await FetchDrivePartitions( configuration, jobName, instanceAddress, driveName ) ) );

			// Merge the data into an array of JSON objects (drives that have not been recently scraped will have -1 for bytesRead, bytesWritten & health)
			return driveNames.Aggregate( new List<JsonObject>(), ( drives, driveName ) => {
				drives.Add( new() {
					{ "name", driveName },
					{ "health", driveHealth.ContainsKey( driveName ) == true ? driveHealth[ driveName ] : -1 },
					{ "bytesRead", new JsonObject() {
						{ "total", driveTotalBytesRead.ContainsKey( driveName ) == true ? driveTotalBytesRead[ driveName ] : -1 },
						{ "rate", driveRateBytesRead.ContainsKey( driveName ) == true ? driveRateBytesRead[ driveName ] : -1  }
					} },
					{ "bytesWritten", new JsonObject() {
						{ "total", driveTotalBytesWritten.ContainsKey( driveName ) == true ? driveTotalBytesWritten[ driveName ] : -1 },
						{ "rate", driveRateBytesWritten.ContainsKey( driveName ) == true ? driveRateBytesWritten[ driveName ] : -1 }
					} },
					{ "partitions", drivePartitions.ContainsKey( driveName ) == true ? drivePartitions[ driveName ] : new JsonArray() }
				} );

				return drives;
			} ).ToArray();

		}

		// Fetches network interfaces for a server
		public static async Task<JsonObject[]> FetchNetworkInterfaces( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the names of all known network interfaces
			string[] interfaceNames = ( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_network_sent_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.Where( netInterface => netInterface != null )
				.Select( netInterface => netInterface!.AsObject() )
				.Where( netInterface => netInterface.NestedHas( "interface" ) == true )
				.Select( netInterface => netInterface.NestedGet<string>( "interface" ) )
				.ToArray();

			// Fetch the total bytes sent for recently scraped network interfaces ( Interface Name -> Bytes Sent )
			Dictionary<string, long> interfaceTotalBytesSent = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_network_sent_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( netInterface => netInterface != null )
				.Select( netInterface => netInterface!.AsObject() )
				.Where( netInterface =>
					netInterface.NestedHas( "metric" ) == true &&
					netInterface.NestedHas( "metric.interface" ) == true
				)
				.Where( netInterface =>
					netInterface.NestedHas( "value" ) == true &&
					netInterface.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					netInterface => netInterface.NestedGet<string>( "metric.interface" ),
					netInterface => long.Parse( netInterface.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);
			
			// Fetch the rate of bytes sent for recently scraped network interfaces ( Interface Name -> Bytes Sent )
			Dictionary<string, double> interfaceRateBytesSent = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_network_sent_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			}, "rate", configuration.PrometheusScrapeIntervalSeconds * 2 ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( netInterface => netInterface != null )
				.Select( netInterface => netInterface!.AsObject() )
				.Where( netInterface =>
					netInterface.NestedHas( "metric" ) == true &&
					netInterface.NestedHas( "metric.interface" ) == true
				)
				.Where( netInterface =>
					netInterface.NestedHas( "value" ) == true &&
					netInterface.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					netInterface => netInterface.NestedGet<string>( "metric.interface" ),
					netInterface => double.Parse( netInterface.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetch the total bytes received for recently scraped network interfaces ( Interface Name -> Bytes Received )
			Dictionary<string, long> interfaceTotalBytesReceived = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_network_received_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( netInterface => netInterface != null )
				.Select( netInterface => netInterface!.AsObject() )
				.Where( netInterface =>
					netInterface.NestedHas( "metric" ) == true &&
					netInterface.NestedHas( "metric.interface" ) == true
				)
				.Where( netInterface =>
					netInterface.NestedHas( "value" ) == true &&
					netInterface.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					netInterface => netInterface.NestedGet<string>( "metric.interface" ),
					netInterface => long.Parse( netInterface.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetch the rate of bytes received for recently scraped network interfaces ( Interface Name -> Bytes Received )
			Dictionary<string, double> interfaceRateBytesReceived = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_resource_network_received_bytes", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			}, "rate", configuration.PrometheusScrapeIntervalSeconds * 2 ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( netInterface => netInterface != null )
				.Select( netInterface => netInterface!.AsObject() )
				.Where( netInterface =>
					netInterface.NestedHas( "metric" ) == true &&
					netInterface.NestedHas( "metric.interface" ) == true
				)
				.Where( netInterface =>
					netInterface.NestedHas( "value" ) == true &&
					netInterface.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					netInterface => netInterface.NestedGet<string>( "metric.interface" ),
					netInterface => double.Parse( netInterface.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Merge the data into an array of JSON objects (interfaces that have not been recently scraped will have -1 for bytesSent & bytesReceived)
			return interfaceNames.Aggregate( new List<JsonObject>(), ( interfaces, interfaceName ) => {
				interfaces.Add( new() {
					{ "name", interfaceName },
					{ "bytesSent", new JsonObject() {
						{ "total", interfaceTotalBytesSent.ContainsKey( interfaceName ) == true ? interfaceTotalBytesSent[ interfaceName ] : -1 },
						{ "rate", interfaceRateBytesSent.ContainsKey( interfaceName ) == true ? interfaceRateBytesSent[ interfaceName ] : -1 }
					} },
					{ "bytesReceived", new JsonObject() {
						{ "total", interfaceTotalBytesReceived.ContainsKey( interfaceName ) == true ? interfaceTotalBytesReceived[ interfaceName ] : -1 },
						{ "rate", interfaceRateBytesReceived.ContainsKey( interfaceName ) == true ? interfaceRateBytesReceived[ interfaceName ] : -1 }
					} }
				} );

				return interfaces;
			} ).ToArray();

		}

		// Fetches services on a server
		public static async Task<JsonObject[]> FetchServices( Config configuration, string jobName, string instanceAddress ) {

			// Fetch the name & description of all known services
			Dictionary<string, JsonObject> information = ( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_service_status_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.Where( service => service != null )
				.Select( service => service!.AsObject() )
				.Where( service =>
					service.NestedHas( "service" ) == true &&
					service.NestedHas( "name" ) == true &&
					service.NestedHas( "description" ) == true &&
					service.NestedHas( "level" ) == true
				)
				.ToDictionary(
					service => string.Concat( service.NestedGet<string>( "level" ), "/", service.NestedGet<string>( "service" ) ),
					service => new JsonObject() {
						{ "name", service.NestedGet<string>( "name" ) },
						{ "description", service.NestedGet<string>( "description" ) }
					}
				);

			// Get the status codes for recently scraped services ( Service Name -> Status Code )
			Dictionary<string, int> statusCodes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_service_status_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( service => service != null )
				.Select( service => service!.AsObject() )
				.Where( service =>
					service.NestedHas( "metric" ) == true &&
					service.NestedHas( "metric.service" ) == true &&
					service.NestedHas( "metric.level" ) == true
				)
				.Where( service =>
					service.NestedHas( "value" ) == true &&
					service.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					service => string.Concat( service.NestedGet<string>( "metric.level" ), "/", service.NestedGet<string>( "metric.service" ) ),
					service => int.Parse( service.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Get the exit codes for recently scraped services ( Service Name -> Exit Code )
			Dictionary<string, int> exitCodes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_service_exit_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( service => service != null )
				.Select( service => service!.AsObject() )
				.Where( service =>
					service.NestedHas( "metric" ) == true &&
					service.NestedHas( "metric.service" ) == true &&
					service.NestedHas( "metric.level" ) == true
				)
				.Where( service =>
					service.NestedHas( "value" ) == true &&
					service.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					service => string.Concat( service.NestedGet<string>( "metric.level" ), "/", service.NestedGet<string>( "metric.service" ) ),
					service => int.Parse( service.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Get the uptime for recently scraped services ( Service Name -> Uptime )
			Dictionary<string, double> uptimes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_service_uptime_seconds", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( service => service != null )
				.Select( service => service!.AsObject() )
				.Where( service =>
					service.NestedHas( "metric" ) == true &&
					service.NestedHas( "metric.service" ) == true &&
					service.NestedHas( "metric.level" ) == true
				)
				.Where( service =>
					service.NestedHas( "value" ) == true &&
					service.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					service => string.Concat( service.NestedGet<string>( "metric.level" ), "/", service.NestedGet<string>( "metric.service" ) ),
					service => double.Parse( service.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Merge the data into an array of JSON objects (services that have not been recently scraped will have -1 for statusCode, exitCode & uptimeSeconds)
			return information.Aggregate( new List<JsonObject>(), ( services, pair ) => {
				string[] parts = pair.Key.Split( '/', 2 );
				int statusCode = statusCodes.ContainsKey( pair.Key ) == true ? statusCodes[ pair.Key ] : -1;

				services.Add( new() {
					{ "level", parts[ 0 ] },
					{ "service", parts[ 1 ] },

					{ "name", information[ pair.Key ].NestedGet<string>( "name" ) },
					{ "description", information[ pair.Key ].NestedGet<string>( "description" ) },

					{ "statusCode", statusCode },
					{ "exitCode", exitCodes.ContainsKey( pair.Key ) == true ? exitCodes[ pair.Key ] : -1 },
					{ "uptimeSeconds", uptimes.ContainsKey( pair.Key ) == true ? uptimes[ pair.Key ] : -1 },

					// NOTE: This is not fetched from the action server because it would cause hundreds of requests...
					{ "supportedActions", new JsonObject() {
						{ "start", statusCode != 1 },
						{ "stop", statusCode == 1 },
						{ "restart", statusCode != 0 }
					} },

					{ "logs", new JsonArray() } // TODO
				} );

				return services;
			} ).ToArray();

		}

		// Fetches Docker container metrics on a server
		public static async Task<JsonObject[]> FetchDockerContainers( Config configuration, string jobName, string instanceAddress, long lastScrape ) {

			// Fetch information for all known Docker containers
			Dictionary<string, JsonObject> dockerContainers = ( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_docker_status_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.Where( container => container != null )
				.Select( container => container!.AsObject() )
				.Where( container =>
					container.NestedHas( "id" ) == true &&
					container.NestedHas( "name" ) == true &&
					container.NestedHas( "image" ) == true
				)
				.ToDictionary(
					container => container.NestedGet<string>( "id" ),
					container => new JsonObject() {
						{ "name", container.NestedGet<string>( "name" ) },
						{ "image", container.NestedGet<string>( "image" ) }
					}
				);

			// Get the status codes for recently scraped containers ( Container ID -> Status Code )
			Dictionary<string, int> statusCodes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_docker_status_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( container => container != null )
				.Select( container => container!.AsObject() )
				.Where( container =>
					container.NestedHas( "metric" ) == true &&
					container.NestedHas( "metric.id" ) == true
				)
				.Where( container =>
					container.NestedHas( "value" ) == true &&
					container.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					container => container.NestedGet<string>( "metric.id" ),
					container => int.Parse( container.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Get the exit codes for recently scraped containers ( Container ID -> Exit Code )
			Dictionary<string, int> exitCodes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_docker_exit_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( container => container != null )
				.Select( container => container!.AsObject() )
				.Where( container =>
					container.NestedHas( "metric" ) == true &&
					container.NestedHas( "metric.id" ) == true
				)
				.Where( container =>
					container.NestedHas( "value" ) == true &&
					container.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					container => container.NestedGet<string>( "metric.id" ),
					container => int.Parse( container.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Get the health status for recently scraped containers ( Container ID -> Uptime )
			Dictionary<string, int> healthStatusCodes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_docker_health_status_code", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( container => container != null )
				.Select( container => container!.AsObject() )
				.Where( container =>
					container.NestedHas( "metric" ) == true &&
					container.NestedHas( "metric.id" ) == true
				)
				.Where( container =>
					container.NestedHas( "value" ) == true &&
					container.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					container => container.NestedGet<string>( "metric.id" ),
					container => int.Parse( container.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Get the uptime for recently scraped containers ( Container ID -> Uptime )
			// NOTE: This is the time since the container was created, not the time since it was started. It does not stop incrementing when the container is stopped!
			Dictionary<string, long> uptimes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_docker_created_timestamp", new() {
				{ "instance", instanceAddress },
				{ "job", jobName }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( container => container != null )
				.Select( container => container!.AsObject() )
				.Where( container =>
					container.NestedHas( "metric" ) == true &&
					container.NestedHas( "metric.id" ) == true
				)
				.Where( container =>
					container.NestedHas( "value" ) == true &&
					container.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					container => container.NestedGet<string>( "metric.id" ),
					container => lastScrape - long.Parse( container.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Merge the data into an array of JSON objects (drives that have not been recently scraped will have -1 for statusCode, exitCode, healthStatusCode & uptimeSeconds)
			return dockerContainers.Aggregate( new List<JsonObject>(), ( containers, pair ) => {
				int statusCode = statusCodes.ContainsKey( pair.Key ) == true ? statusCodes[ pair.Key ] : -1;

				containers.Add( new() {
					{ "id", pair.Key },

					{ "name", pair.Value.NestedGet<string>( "name" ) },
					{ "image", pair.Value.NestedGet<string>( "image" ) },

					{ "statusCode", statusCode },
					{ "exitCode", exitCodes.ContainsKey( pair.Key ) == true ? exitCodes[ pair.Key ] : -1 },
					{ "healthStatusCode", healthStatusCodes.ContainsKey( pair.Key ) == true ? healthStatusCodes[ pair.Key ] : -1 },
					{ "uptimeSeconds", uptimes.ContainsKey( pair.Key ) == true && statusCode == 1 ? uptimes[ pair.Key ] : -1 },

					{ "supportedActions", new JsonObject() { // TODO
						{ "start", false },
						{ "stop", false },
						{ "restart", false },
						{ "remove", false },
					} },

					{ "logs", new JsonArray() } // TODO
				} );

				return containers;
			} ).ToArray();

		}

		// Fetches SNMP agent metrics on a server
		public static async Task<JsonObject[]> FetchSNMPAgents( Config configuration, string jobName, string instanceAddress ) {

			// Fetch all known SNMP agents
			Dictionary<string, JsonObject> snmpAgents = ( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_snmp_uptime_seconds", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.Where( agent => agent != null )
				.Select( agent => agent!.AsObject() )
				.Where( agent =>
					agent.NestedHas( "address" ) == true &&
					agent.NestedHas( "port" ) == true &&
					agent.NestedHas( "name" ) == true &&
					agent.NestedHas( "description" ) == true &&
					agent.NestedHas( "location" ) == true &&
					agent.NestedHas( "contact" ) == true
				)
				.ToDictionary(
					agent => string.Concat( agent.NestedGet<string>( "address" ), ":", agent.NestedGet<string>( "port" ) ),
					agent => new JsonObject() {
						{ "name", agent.NestedGet<string>( "name" ) },
						{ "description", agent.NestedGet<string>( "description" ) },
						{ "location", agent.NestedGet<string>( "location" ) },
						{ "contact", agent.NestedGet<string>( "contact" ) }
					}
				);

			// Fetch the uptime for recently scraped SNMP agents ( Address:Port -> Uptime )
			Dictionary<string, double> uptimes = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_snmp_uptime_seconds", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( agent => agent != null )
				.Select( agent => agent!.AsObject() )
				.Where( agent =>
					agent.NestedHas( "metric" ) == true &&
					agent.NestedHas( "metric.address" ) == true &&
					agent.NestedHas( "metric.port" ) == true
				)
				.Where( agent =>
					agent.NestedHas( "value" ) == true &&
					agent.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					agent => string.Concat( agent.NestedGet<string>( "metric.address" ), ":", agent.NestedGet<string>( "metric.port" ) ),
					agent => double.Parse( agent.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetch the service count for recently scraped SNMP agents ( Address:Port -> Service Count )
			Dictionary<string, long> serviceCounts = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_snmp_service_count", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( agent => agent != null )
				.Select( agent => agent!.AsObject() )
				.Where( agent =>
					agent.NestedHas( "metric" ) == true &&
					agent.NestedHas( "metric.address" ) == true &&
					agent.NestedHas( "metric.port" ) == true
				)
				.Where( agent =>
					agent.NestedHas( "value" ) == true &&
					agent.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					agent => string.Concat( agent.NestedGet<string>( "metric.address" ), ":", agent.NestedGet<string>( "metric.port" ) ),
					agent => long.Parse( agent.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Fetch the traps received count for recently scraped SNMP agents ( Address:Port -> Received Traps )
			Dictionary<string, long> receivedTraps = ( await FetchQuery( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_snmp_traps_received", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.NestedGet<JsonArray>( "result" )
				.Where( agent => agent != null )
				.Select( agent => agent!.AsObject() )
				.Where( agent =>
					agent.NestedHas( "metric" ) == true &&
					agent.NestedHas( "metric.address" ) == true &&
					agent.NestedHas( "metric.port" ) == true
				)
				.Where( agent =>
					agent.NestedHas( "value" ) == true &&
					agent.NestedGet<JsonArray>( "value" ).Count == 2
				)
				.ToDictionary(
					agent => string.Concat( agent.NestedGet<string>( "metric.address" ), ":", agent.NestedGet<string>( "metric.port" ) ),
					agent => long.Parse( agent.NestedGet<JsonArray>( "value" )[ 1 ]!.AsValue().GetValue<string>() )
				);

			// Merge the data into an array of JSON objects (drives that have not been recently scraped will have -1 for statusCode, exitCode, healthStatusCode & uptimeSeconds)
			return snmpAgents.Aggregate( new List<JsonObject>(), ( containers, pair ) => {
				string[] parts = pair.Key.Split( ':', 2 );

				containers.Add( new() {
					{ "address", parts[ 0 ] },
					{ "port", int.Parse( parts[ 1 ] ) },

					{ "name", pair.Value.NestedGet<string>( "name" ) },
					{ "description", pair.Value.NestedGet<string>( "description" ) },
					{ "location", pair.Value.NestedGet<string>( "location" ) },
					{ "contact", pair.Value.NestedGet<string>( "contact" ) },

					{ "uptimeSeconds", uptimes.ContainsKey( pair.Key ) == true ? uptimes[ pair.Key ] : -1 },
					{ "serviceCount", serviceCounts.ContainsKey( pair.Key ) == true ? serviceCounts[ pair.Key ] : -1 },
					{ "receivedTraps", new JsonObject() {
						{ "count", receivedTraps.ContainsKey( pair.Key ) == true ? receivedTraps[ pair.Key ] : -1 },
						{ "logs", new JsonArray() } // TODO
					} }
				} );

				return containers;
			} ).ToArray();

		}

		// Fetches the action server IP address & port on a server
		public static async Task<JsonObject?> FetchActionServer( Config configuration, string jobName, string instanceAddress ) =>
			( await FetchSeries( configuration, CreatePromQL( $"{ configuration.PrometheusMetricsPrefix }_action_listening", new() {
				{ "job", jobName },
				{ "instance", instanceAddress }
			} ) ) )
				.Where( result => result != null )
				.Select( result => result!.AsObject() )
				.Where( result => result.NestedHas( "address" ) == true && result.NestedHas( "port" ) == true )
				.Select( result => new JsonObject() {
					{ "address", result.NestedGet<string>( "address" ) },
					{ "port", int.Parse( result.NestedGet<string>( "port" ) ) }
				} )
				.FirstOrDefault();

	}

}
