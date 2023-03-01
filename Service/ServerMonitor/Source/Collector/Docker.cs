using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Prometheus;

// https://docs.docker.com/engine/api/

/* https://docs.docker.com/desktop/faqs/general/#/how-do-i-connect-to-the-remote-docker-engine-api
Linux (default): unix:///var/run/docker.sock
Windows (default): npipe://./pipe/docker_engine
Universal (if configured): tcp://localhost:2375
*/

namespace ServerMonitor.Collector {

	// Encapsulates collecting Docker container metrics
	public class Docker : Base {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Docker" );

		// Setup a basic HTTP client for talking to the Docker Engine API
		private static readonly HttpClient httpClient = new() {
			DefaultRequestHeaders = {
				{ "Accept", "application/json" },
				{ "User-Agent", "Server Monitor" }
			}
		};

		// Holds the exported Prometheus metrics
		public readonly Gauge Status;
		public readonly Gauge ExitCode;
		public readonly Counter CreatedTimestamp;

		// Initialise the exported Prometheus metrics
		public Docker( Config configuration ) : base( configuration ) {
			Status = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_docker_status", "Docker container status", new GaugeConfiguration {
				LabelNames = new[] { "name", "id", "image" }
			} );
			ExitCode = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_docker_exit_code", "Docker container exit code", new GaugeConfiguration {
				LabelNames = new[] { "name", "id", "image" }
			} );
			CreatedTimestamp = Metrics.CreateCounter( $"{ configuration.PrometheusMetricsPrefix }_docker_created_timestamp", "Docker container creation unix timestamp", new CounterConfiguration {
				LabelNames = new[] { "name", "id", "image" }
			} );

			Status.Set( -1 );
			ExitCode.Set( -1 );
			CreatedTimestamp.IncTo( -1 );

			logger.LogInformation( "Initalised Prometheus metrics" );
		}

		// Updates the exported Prometheus metrics (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		public override void UpdateOnWindows( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// Parse the methods of connecting to the Docker Engine API
			Match namedPipeMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^npipe://(.+)/pipe/(.+)$" );
			Match tcpMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^tcp://(.+):(\d+)$" );

			// Update the metrics by connecting over a named pipe...
			if ( namedPipeMatch.Success ) {
				string pipeMachine = namedPipeMatch.Groups[ 1 ].Value;
				string pipeName = namedPipeMatch.Groups[ 2 ].Value;
				UpdateOverPipe( pipeMachine, pipeName, configuration );

			// Update the metrics by connecting over a TCP socket...
			} else if ( tcpMatch.Success ) {
				string tcpAddress = tcpMatch.Groups[ 1 ].Value;
				int tcpPort = int.Parse( tcpMatch.Groups[ 2 ].Value );
				UpdateOverTCP( tcpAddress, tcpPort, configuration ).Wait();

			// Unrecognised method of connecting
			} else throw new FormatException( $"Invalid Docker Engine API address '${ configuration.DockerEngineAPIAddress }'" );
		}

		// Updates the metrics by connecting to the Docker Engine API over a named pipe (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		private void UpdateOverPipe( string machine, string name, Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// Connect to the named pipe - https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream?view=net-7.0
			using ( NamedPipeClientStream pipeStream = new( machine, name, PipeDirection.InOut ) ) {
				pipeStream.Connect( 1000 ); // 1 second

				// HTTP request to fetch the list of all Docker containers
				HttpResponseMessage response = HTTPRequestOverPipe( pipeStream, "GET", $"/v{ configuration.DockerEngineAPIVersion }/containers/json?all=true", configuration );
				if ( response.IsSuccessStatusCode == false ) throw new Exception( $"Docker Engine API request failed with HTTP status { response.StatusCode }" );
				string responseContent = response.Content.ReadAsStringAsync().Result;

				// Process the response
				UpdateUsingResponse( responseContent );
			}
		}

		// Sends a HTTP request & receives its response over a named pipe (for Windows)
		[ SupportedOSPlatform( "windows" ) ]
		private HttpResponseMessage HTTPRequestOverPipe( PipeStream pipeStream, string method, string path, Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// Send UTF-8 encoded HTTP request lines joined with CRLF over the named pipe
			pipeStream.Write( Encoding.UTF8.GetBytes( string.Concat( string.Join( "\r\n", new[] {
				$"{ method } { path } HTTP/1.1",
				$"Host: localhost",
				$"Accept: ${ httpClient.DefaultRequestHeaders.Accept }",
				$"User-Agent: { httpClient.DefaultRequestHeaders.UserAgent }",
				"Connection: close"
			} ), "\r\n\r\n" ) ) );
			pipeStream.WaitForPipeDrain();
			pipeStream.Flush();

			// Read the entire response from the pipe
			int bytesRead = -1;
			byte[] readBuffer = new byte[ 65536 ];
			MemoryStream memoryStream = new();
			while ( ( bytesRead = pipeStream.Read( readBuffer ) ) > 0 ) {
				memoryStream.Write( readBuffer, 0, bytesRead );
				Array.Clear( readBuffer, 0, readBuffer.Length );
			}
			
			// Convert the response to a UTF-8 string & remove the seemingly random garbage inside it
			string response = Encoding.UTF8.GetString( memoryStream.GetBuffer(), 0, ( int ) memoryStream.Length );
			int positionOfGarbage = response.IndexOf( "2d3\r\n" );
			if ( positionOfGarbage != -1 ) response = string.Concat( response.Substring( 0, positionOfGarbage ), response.Substring( positionOfGarbage + 5 ) ); // Random 2d3 before body
			response = response.Substring( 0, response.Length - 5 ).Trim(); // Random zero at the end

			// Break up the parts of the HTTP response
			int responseDivisionPosition = response.IndexOf( "\r\n\r\n" );
			string[] responseHeaders = response.Substring( 0, responseDivisionPosition ).Split( "\r\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			string responseBody = response.Substring( responseDivisionPosition + 4 );

			// Parse the response into the standard HTTP response message class
			HttpResponseMessage responseMessage = new();
			foreach ( string responseHeader in responseHeaders ) {
				Match statusLineMatch = Regex.Match( responseHeader, @"^HTTP/1.1 (\d+) .+$" );
				if ( statusLineMatch.Success ) {
					responseMessage.StatusCode = ( HttpStatusCode ) int.Parse( statusLineMatch.Groups[ 1 ].Value );
				} else {
					string[] header = responseHeader.Split( ": ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
					string name = header[ 0 ].ToLower(), value = header[ 1 ];
					if ( name != "content-type" ) responseMessage.Headers.Add( header[ 0 ].ToLower(), header[ 1 ] );
				}
			}
			responseMessage.Content = new StringContent( responseBody, Encoding.UTF8 );

			return responseMessage;
		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			// Parse the methods of connecting to the Docker Engine API
			Match socketMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^unix://(.+)$" );
			Match tcpMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^tcp://(.+):(\d+)$" );

			// Update the metrics by connecting over a unix socket...
			if ( socketMatch.Success ) {
				string socketPath = socketMatch.Groups[ 1 ].Value;
				UpdateOverSocket( socketPath, configuration );

			// Update the metrics by connecting over a TCP socket...
			} else if ( tcpMatch.Success ) {
				string tcpAddress = tcpMatch.Groups[ 1 ].Value;
				int tcpPort = int.Parse( tcpMatch.Groups[ 2 ].Value );
				UpdateOverTCP( tcpAddress, tcpPort, configuration ).Wait();

			// Unrecognised method of connecting
			} else throw new FormatException( $"Invalid Docker Engine API address '${ configuration.DockerEngineAPIAddress }'" );
		}

		// Updates the metrics by connecting to the Docker Engine API over a unix socket (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		private void UpdateOverSocket( string socketPath, Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			throw new NotImplementedException();
		}

		// Updates the metrics by sending a HTTP request (TCP under-the-hood) to the Docker Engine API - https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-7.0
		[ SupportedOSPlatform( "windows" ) ]
		[ SupportedOSPlatform( "linux" ) ]
		private async Task UpdateOverTCP( string address, int port, Config configuration ) {
			using ( HttpResponseMessage response = await httpClient.GetAsync( $"http://{ address }:{ port }/v{ configuration.DockerEngineAPIVersion }/containers/json?all=true" ) ) {
				if ( response.IsSuccessStatusCode == false ) throw new Exception( $"Docker Engine API request failed with HTTP status { response.StatusCode }" );
				UpdateUsingResponse( await response.Content.ReadAsStringAsync() );
			}
		}

		// Updates the metrics using the JSON response from the Docker Engine API
		[ SupportedOSPlatform( "windows" ) ]
		[ SupportedOSPlatform( "linux" ) ]
		private void UpdateUsingResponse( string response ) {

			// Parse the response as a JSON array
			JsonArray? responseJson = JsonSerializer.Deserialize<JsonArray>( response );
			if ( responseJson == null ) throw new JsonException( $"Failed to parse response '{ response }' as JSON array" );

			// Loop through each JSON object in the JSON array...
			foreach ( JsonObject? containerJson in responseJson ) {
				if ( containerJson == null ) throw new JsonException( $"JSON array contains null JSON object" );

				// Get the container identifier
				if ( containerJson.TryGetPropertyValue( "Id", out JsonNode? containerIdProperty ) == false || containerIdProperty == null ) throw new JsonException( $"JSON object '{ containerJson.ToJsonString() }' has no property for container ID" );
				string containerId = containerIdProperty.AsValue().GetValue<string>();
				if ( string.IsNullOrWhiteSpace( containerId ) ) throw new JsonException( $"Container ID '{ containerId }' is null, empty or whitespace" );

				// Get the container name
				if ( containerJson.TryGetPropertyValue( "Names", out JsonNode? containerNamesProperty ) == false || containerNamesProperty == null ) throw new JsonException( $"JSON object '{ containerJson.ToJsonString() }' has no property for container names" );
				JsonArray containerNames = containerNamesProperty.AsArray();
				if ( containerNames.Count <= 0 ) throw new JsonException( $"JSON array '{ containerNames.ToJsonString() }' for container names is empty" );
				string? containerName = containerNames[ 0 ]?.GetValue<string>().TrimStart( '/' );
				if ( string.IsNullOrWhiteSpace( containerName ) ) throw new JsonException( $"Container name '{ containerName }' is null, empty or whitespace" );

				// Get the container image
				if ( containerJson.TryGetPropertyValue( "Image", out JsonNode? containerImageProperty ) == false || containerImageProperty == null ) throw new JsonException( $"JSON object '{ containerJson.ToJsonString() }' has no property for container image" );
				string containerImage = containerImageProperty.AsValue().GetValue<string>();
				if ( string.IsNullOrWhiteSpace( containerImage ) ) throw new JsonException( $"Container image '{ containerImage }' is null, empty or whitespace" );

				// Get the container created timestamp
				if ( containerJson.TryGetPropertyValue( "Created", out JsonNode? containerCreatedProperty ) == false || containerCreatedProperty == null ) throw new JsonException( $"JSON object '{ containerJson.ToJsonString() }' has no property for container created timestamp" );
				long containerCreatedTimestamp = containerCreatedProperty.AsValue().GetValue<long>();
				DateTimeOffset containerCreated = DateTimeOffset.FromUnixTimeSeconds( containerCreatedTimestamp );

				// Get the container state
				if ( containerJson.TryGetPropertyValue( "State", out JsonNode? containerStateProperty ) == false || containerStateProperty == null ) throw new JsonException( $"JSON object '{ containerJson.ToJsonString() }' has no property for container state" );
				string containerState = containerStateProperty.AsValue().GetValue<string>();
				if ( string.IsNullOrWhiteSpace( containerState ) ) throw new JsonException( $"Container state '{ containerState }' is null, empty or whitespace" );

				// Get the container status
				if ( containerJson.TryGetPropertyValue( "Status", out JsonNode? containerStatusProperty ) == false || containerStatusProperty == null ) throw new JsonException( $"JSON object '{ containerJson.ToJsonString() }' has no property for container status" );
				string containerStatus = containerStatusProperty.AsValue().GetValue<string>();
				if ( string.IsNullOrWhiteSpace( containerStatus ) ) throw new JsonException( $"Container status '{ containerStatus }' is null, empty or whitespace" );

				// Update the exported Prometheus metrics
				Status.WithLabels( containerId, containerName, containerImage ).Set( containerState switch {
					"created" => 0,
					"running" => 1,
					"restarting" => 2,
					"dead" => 3,
					"exited" => 4,
					"paused" => 5,
					"removing" => 6,
					_ => throw new Exception( $"Unrecognised status '{ containerState }' for Docker container '{ containerId }'" )
				} );

				Match exitCodeMatch = Regex.Match( containerStatus, @"^\w+ \((\d+)\) .+$" );
				if ( exitCodeMatch.Success == false ) throw new Exception( $"Failed to extract exit code from status '{ containerStatus }' for Docker container '{ containerId }'" );
				ExitCode.WithLabels( containerId, containerName, containerImage ).Set( int.Parse( exitCodeMatch.Groups[ 1 ].Value ) );

				CreatedTimestamp.WithLabels( containerId, containerName, containerImage ).IncTo( containerCreated.ToUnixTimeSeconds() );

				logger.LogDebug( "Updated Prometheus metrics" );

			}
		}

	}
}