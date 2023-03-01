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
		private static readonly HttpClient httpClient = new() {
			DefaultRequestHeaders = {
				{ "Accept", "application/json" },
				{ "User-Agent", "Server Monitor" }
			}
		};

		// Holds the exported Prometheus metrics
		public readonly Gauge State; // Running, exited, etc.

		// Initialise the exported Prometheus metrics
		public Docker( Config configuration ) : base( configuration ) {
			State = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_docker_state", "Docker container state", new GaugeConfiguration {
				LabelNames = new[] { "name", "id", "image" }
			} );

			State.Set( -1 );

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

		/*
		[{"Id":"d76139ff9f2fc813bf4add87329a0af85cb2adfc8e81ab065605f7af0018da66","Names":["/HelloWorld"],"Image":"hello-world:latest","ImageID":"sha256:e7c2385a663fe328a753fa44090c644868949b764b64131d792c49a1f28d151a","Command":"cmd /C 'type C:\\hello.txt'","Created":1677677039,"Ports":[],"Labels":{},"State":"exited","Status":"Exited (0) 4 hours ago","HostConfig":{"NetworkMode":"default"},"NetworkSettings":{"Networks":{"nat":{"IPAMConfig":null,"Links":null,"Aliases":null,"NetworkID":"ae2f59fa5a0540995170a33f5618b4bdf14559d1fccf0d4dcc70dfc21563a60d","EndpointID":"","Gateway":"","IPAddress":"","IPPrefixLen":0,"IPv6Gateway":"","GlobalIPv6Address":"","GlobalIPv6PrefixLen":0,"MacAddress":"","DriverOpts":null}}},"Mounts":[]}]
		*/

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

				// Get the container ID
				string? containerId = containerJson[ "Id" ]?.GetValue<string>();
				if ( containerId == null ) throw new JsonException( $"JSON object has no property for container ID" );
				logger.LogDebug( "Docker container: '{0}'", containerId );
			}
		}

	}
}
