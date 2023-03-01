using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.IO;
using System.IO.Pipes;
using System.Text.RegularExpressions;
using System.ServiceProcess;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading.Tasks;
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

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Services" );

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

			Match namedPipeMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^npipe://(.+)/pipe/(.+)$" );
			Match tcpMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^tcp://(.+):(\d+)$" );

			if ( namedPipeMatch.Success ) {
				string pipeMachine = namedPipeMatch.Groups[ 1 ].Value;
				string pipeName = namedPipeMatch.Groups[ 2 ].Value;
				UpdateUsingPipe( pipeMachine, pipeName, configuration );

			} else if ( tcpMatch.Success ) {
				string tcpAddress = tcpMatch.Groups[ 1 ].Value;
				int tcpPort = int.Parse( tcpMatch.Groups[ 2 ].Value );
				Task updateTask = UpdateUsingTCP( tcpAddress, tcpPort, configuration );
				updateTask.Wait();

			} else throw new FormatException( $"Invalid Docker Engine API address '${ configuration.DockerEngineAPIAddress }'" );
		}

		[ SupportedOSPlatform( "windows" ) ]
		private void UpdateUsingPipe( string machine, string name, Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new PlatformNotSupportedException( "Method only available on Windows" );

			// https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream?view=net-7.0
			logger.LogDebug( $"Opening named pipe '{ name }' on machine '{ machine }' ({ configuration.DockerEngineAPIAddress })..." );
			using ( NamedPipeClientStream pipeStream = new( machine, name, PipeDirection.InOut ) ) {
				logger.LogDebug( $"Opened pipe, connecting to Docker daemon..." );
				pipeStream.Connect( 3000 ); // 3 second timeout
				logger.LogDebug( "Connected to Docker daemon, with {0} pipe server instances", pipeStream.NumberOfServerInstances );

				logger.LogDebug( "Sending Docker API request..." );
				using ( StreamWriter streamWriter = new( pipeStream ) ) {
					streamWriter.Write( "GET /v1.42/containers/json HTTP/1.1\r\n" ); // ?all=true
					//streamWriter.Write( "Host: docker\r\n" );
					streamWriter.Write( "Accept: application/json\r\n" );
					//streamWriter.Write( "User-Agent: Server Monitor (Windows)\r\n" );
					//streamWriter.WriteLine( "Connection: close" );
					streamWriter.Write("\r\n\r\n");
				}

				logger.LogDebug( "Reading Docker API response..." );
				using ( StreamReader streamReader = new( pipeStream ) ) {
					string? line;
					while ( ( line = streamReader.ReadLine() ) != null ) {
						logger.LogTrace( "Docker API response: '{0}'", line );
					}
				}

				logger.LogDebug( "Disconnecting from Docker daemon..." );
			}
			logger.LogDebug( "Disconnected from Docker daemon" );

		}

		[ SupportedOSPlatform( "windows" ) ]
		[ SupportedOSPlatform( "linux" ) ]
		private async Task UpdateUsingTCP( string address, int port, Config configuration ) {

			// https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient?view=net-7.0
			/*
			logger.LogDebug( $"Creating TCP client for IPv4..." );
			using ( TcpClient tcpClient = new( AddressFamily.InterNetwork ) ) {
				logger.LogDebug( $"Created TCP client, connecting to Docker daemon at '{ address }:{ port }'..." );
				tcpClient.Connect( address, port );
				logger.LogDebug( "Connected to Docker daemon" );

				using ( NetworkStream networkStream = tcpClient.GetStream() ) {

					logger.LogDebug( "Sending Docker API request..." );
					string[] request = new[] {
						"GET /v1.41/containers/json?all=true HTTP/1.1",
						$"Host: { address }:{ port }",
						"Accept: application/json",
						"User-Agent: Server Monitor (Windows)",
						"Connection: close"
					};
					networkStream.Write( Encoding.UTF8.GetBytes( string.Concat( string.Join( "\r\n", request ), "\r\n\r\n" ) ) );

					logger.LogDebug( "Reading Docker API response..." );
					int bytesRead;
					byte[] readBuffer = new byte[ 65536 ];
					while ( ( bytesRead = networkStream.Read( readBuffer ) ) > 0 ) {
						string response = Encoding.UTF8.GetString( readBuffer, 0, bytesRead );
						logger.LogDebug( "Docker API response: '{0}'", response );
						Array.Clear( readBuffer, 0, readBuffer.Length );
					}
				}

				logger.LogDebug( "Disconnecting from Docker daemon..." );
			}
			logger.LogDebug( "Disconnected from Docker daemon" );
			*/

			// https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-7.0
			HttpClient httpClient = new();
			httpClient.DefaultRequestHeaders.Add( "Accept", "application/json" );
			httpClient.DefaultRequestHeaders.Add( "User-Agent", "Server Monitor" );

			using ( HttpResponseMessage response = await httpClient.GetAsync( $"http://{ address }:{ port }/v{ configuration.DockerEngineAPIVersion }/containers/json?all=true" ) ) {
				if ( response.IsSuccessStatusCode == false ) throw new Exception( $"Docker API request failed with HTTP status { response.StatusCode }" );

				string responseString = await response.Content.ReadAsStringAsync();

				JsonArray? responseJson = JsonSerializer.Deserialize<JsonArray>( responseString );
				if ( responseJson == null ) throw new JsonException( $"Failed to parse Docker API response '{ responseString }' as JSON array" );

				foreach ( JsonObject? containerJson in responseJson ) {
					if ( containerJson == null ) throw new JsonException( $"Docker API response JSON array contains null JSON object" );

					string? containerId = containerJson[ "Id" ]?.GetValue<string>();
					if ( containerId == null ) throw new JsonException( $"Docker API response JSON object has no container ID" );

					logger.LogDebug( "Docker container: '{0}'", containerId );
				}
			}

		}

		// Updates the exported Prometheus metrics (for Linux)
		[ SupportedOSPlatform( "linux" ) ]
		public override void UpdateOnLinux( Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			Match socketMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^unix://(.+)$" );
			Match tcpMatch = Regex.Match( configuration.DockerEngineAPIAddress, @"^tcp://(.+):(\d+)$" );

			if ( socketMatch.Success ) {
				string socketPath = socketMatch.Groups[ 1 ].Value;
				UpdateUsingSocket( socketPath, configuration );

			} else if ( tcpMatch.Success ) {
				string tcpAddress = tcpMatch.Groups[ 1 ].Value;
				int tcpPort = int.Parse( tcpMatch.Groups[ 2 ].Value );
				Task updateTask = UpdateUsingTCP( tcpAddress, tcpPort, configuration );
				updateTask.Wait();

			} else throw new FormatException( $"Invalid Docker Engine API address '${ configuration.DockerEngineAPIAddress }'" );
		}

		[ SupportedOSPlatform( "linux" ) ]
		private void UpdateUsingSocket( string socketPath, Config configuration ) {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new PlatformNotSupportedException( "Method only available on Linux" );

			throw new NotImplementedException();
		}

	}
}