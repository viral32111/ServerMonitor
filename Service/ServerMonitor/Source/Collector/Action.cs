using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Diagnostics;
using System.ServiceProcess;
using System.Security;
using Microsoft.Extensions.Logging;
using viral32111.JsonExtensions;
using ServerMonitor.Connector.Helper;
using ServerMonitor.Connector;
using Prometheus;

namespace ServerMonitor.Collector {

	// Encapsulates the RESTful API for actions
	public class Action {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Action" );

		// The configuration
		private readonly Config configuration;

		// Everything for the HTTP listener
		private readonly HttpListener httpListener = new();
		private TaskCompletionSource httpListenerLoopCompletionSource = new();
		private Task? httpListenerLoopTask = null;

		// The configured authentication key, if any
		private string? authenticationKey = null;

		// Prometheus metric mainly to publish the IP address & port of the HTTP listener
		public readonly Gauge Listening;

		// Setup everything when instantiated
		public Action( Config config ) {
			configuration = config;

			// Initialise the Prometheus metric
			Listening = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_action_listening", "IP address & port of the HTTP listener", new GaugeConfiguration {
				LabelNames = new string[] { "address", "port" }
			} );
			Listening.Set( -1 );
			logger.LogDebug( "Initalised Prometheus metric" );

			// Set the authentication key, if one is configured
			authenticationKey = string.IsNullOrWhiteSpace( configuration.CollectorActionAuthenticationKey ) == false ? configuration.CollectorActionAuthenticationKey : null;
			if ( authenticationKey == null ) logger.LogWarning( "Authentication key is empty, requests will NOT require authentication!" );

			// Setup the API routes for the HTTP listener
			string baseUrl = $"http://{ configuration.CollectorActionListenAddress }:{ configuration.CollectorActionListenPort }";
			foreach ( string route in new string[] { "/server", "/service" } ) httpListener.Prefixes.Add( string.Concat( baseUrl, route, "/" ) );
			logger.LogDebug( "Setup API routes" );

		}

		// Starts the HTTP listener
		public void StartListening() {
			if ( httpListener.IsListening == true ) throw new Exception( "HTTP listener is already listening" );

			httpListener.Start();
			httpListenerLoopTask = Task.Run( ListenerLoop ); // Start in the background
			logger.LogDebug( "Started HTTP listener" );

			Listening.WithLabels( configuration.CollectorActionListenAddress, configuration.CollectorActionListenPort.ToString() ).Set( 1 );
		}

		// Stops the HTTP listener
		public void StopListening() {
			if ( httpListener.IsListening == false ) throw new Exception( "HTTP listener is not listening" );

			logger.LogDebug( "Stopping HTTP listener" );
			httpListenerLoopCompletionSource.SetResult();
			httpListenerLoopTask?.Wait();
			httpListener.Stop();
			httpListenerLoopCompletionSource = new(); // Reset for next time
			logger.LogDebug( "Stopped HTTP listener" );

			Listening.WithLabels( configuration.CollectorActionListenAddress, configuration.CollectorActionListenPort.ToString() ).Set( 0 );
		}

		// Main receive loop that should be ran in the background
		private void ListenerLoop() {
			logger.LogDebug( "HTTP listener loop started" );

			// Loop forever until we get the signal to stop
			while ( httpListener.IsListening == true ) {
				Task<HttpListenerContext> getContextTask = httpListener.GetContextAsync();
				if ( Task.WhenAny( getContextTask, httpListenerLoopCompletionSource.Task ).Result == httpListenerLoopCompletionSource.Task ) {
					logger.LogDebug( "Received signal to stop HTTP listener loop" );
					break;
				};

				// Process this request
				ProcessHTTPRequest( getContextTask.Result );
			}

			logger.LogDebug( "HTTP listener loop finished" );
		}

		// Processes an incoming HTTP request
		private HttpListenerResponse ProcessHTTPRequest( HttpListenerContext context ) {

			// Get the request & response from the context
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			string requestMethod = request.HttpMethod;
			string requestPath = request.Url?.AbsolutePath ?? "/";
			string requestAddress = string.Empty;
			try {
				requestAddress = request.RemoteEndPoint.Address.ToString();
			} catch ( NullReferenceException ) {
				logger.LogError( "No remote IP address for HTTP request '{0}' '{1}'", requestMethod, requestPath );
				return response;
			}
			logger.LogInformation( "HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );

			// If authentication is required...
			if ( authenticationKey != null ) {
				string[]? authorizationHeader = request.Headers.GetValues( "Authorization" )?.FirstOrDefault()?.Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );

				// Ensure authentication is provided
				if ( authorizationHeader == null ) {
					logger.LogWarning( "No authentication for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
					return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.NoAuthentication );
				}

				// Ensure the authentication is not malformed
				if ( authorizationHeader.Length != 2 ) {
					logger.LogWarning( "Invalid authentication '{0}' for HTTP request '{1}' '{2}' from '{3}'", string.Join( ' ', authorizationHeader ), requestMethod, requestPath, requestAddress );
					return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.InvalidAuthentication );
				}

				// Ensure the authentication type is valid
				if ( authorizationHeader[ 0 ] != "Key" ) {
					logger.LogWarning( "Incorrect authentication type '{0}' (expected 'Key') for HTTP request '{1}' '{2}' from '{3}'", authorizationHeader[ 0 ], requestMethod, requestPath, requestAddress );
					return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.IncorrectAuthentication, data: new JsonObject() {
						{ "expected", "Key" }
					} );
				}

				// Ensure the authentication key is correct
				if ( authorizationHeader[ 1 ] != authenticationKey ) {
					logger.LogWarning( "Incorrect authentication key '{0}' for HTTP request '{1}' '{2}' from '{3}'", authorizationHeader[ 1 ], requestMethod, requestPath, requestAddress );
					return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.IncorrectAuthentication );
				}
			}

			// Ensure the request is either GET or POST
			if ( requestMethod != "GET" && requestMethod != "POST" ) {
				logger.LogWarning( "Bad method '{0}' for HTTP request '{1}' '{2}' from '{3}'", requestMethod, requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.MethodNotAllowed, errorCode: ErrorCode.MethodNotAllowed, data: new JsonObject() {
					{ "expected", new JsonArray() { "GET", "POST" } }
				} );
			};

			// Get the content type from the headers
			string? contentType = request.Headers.GetValues( "Content-Type" )?.FirstOrDefault();

			// Ensure a content type is provided
			if ( contentType == null ) {
				logger.LogWarning( "No content type for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.UnsupportedMediaType, errorCode: ErrorCode.NoContentType, data: new JsonObject() {
					{ "expected", "application/json" }
				} );
			}

			// Ensure the content type is JSON
			if ( contentType?.Contains( "application/json" ) == false ) {
				logger.LogWarning( "Invalid content type '{0}' for HTTP request '{1}' '{2}' from '{3}'", contentType, requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.UnsupportedMediaType, errorCode: ErrorCode.InvalidContentType, data: new JsonObject() {
					{ "expected", "application/json" }
				} );
			}

			// Read the request body - https://stackoverflow.com/a/5198080
			string requestBody = string.Empty;
			using ( Stream requestBodyStream = request.InputStream ) {
				using ( StreamReader requestBodyStreamReader = new( requestBodyStream, request.ContentEncoding ) ) {
					requestBody = requestBodyStreamReader.ReadToEnd();
					logger.LogDebug( "HTTP request body: '{0}'", requestBody );
				}
			}

			// Ensure a request body is provided
			if ( string.IsNullOrWhiteSpace( requestBody ) == true ) {
				logger.LogWarning( "No content for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoPayload );
			}

			// Parse the request body as JSON
			JsonObject? requestPayload = null;
			try {
				requestPayload = JsonSerializer.Deserialize<JsonObject>( requestBody );
				if ( requestPayload == null ) throw new JsonException();
			} catch ( JsonException ) {
				logger.LogWarning( "Failed to parse content '{0}' as JSON for HTTP request '{1}' '{2}' from '{3}'", requestBody, requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidPayload );
			}

			// Ensure the request payload contains the server identifier
			if ( requestPayload.ContainsKey( "server" ) == false || string.IsNullOrWhiteSpace( requestPayload[ "server" ]?.GetValue<string?>() ) == true ) {
				logger.LogWarning( "Missing server identifier property in payload for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
					{ "property", "server" }
				} );
			}

			// Ensure the request payload contains the action name, if applicable
			if ( requestMethod == "POST" && ( requestPayload.ContainsKey( "action" ) == false || string.IsNullOrWhiteSpace( requestPayload[ "action" ]?.GetValue<string?>() ) == true ) ) {
				logger.LogWarning( "Missing action property in payload for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
					{ "property", "action" }
				} );
			}

			// Ensure the request payload contains the service name, if applicable
			if ( requestPath == "/service" && ( requestPayload.ContainsKey( "service" ) == false || string.IsNullOrWhiteSpace( requestPayload[ "service" ]?.GetValue<string?>() ) == true ) ) {
				logger.LogWarning( "Missing service property in payload for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
					{ "property", "service" }
				} );
			}

			// Store each of the properties for later use
			string serverIdentifier = requestPayload[ "server" ]!.GetValue<string>();
			string? actionName = requestMethod == "POST" ? requestPayload[ "action" ]!.GetValue<string>() : null;
			string? serviceName = requestPath == "/service" ? requestPayload[ "service" ]!.GetValue<string>() : null;
			logger.LogDebug( "Server: '{0}', Action: '{1}', Service: '{2}'", serverIdentifier, actionName, serviceName );

			// Decode the server identifier
			string jobName = string.Empty, instanceAddress = string.Empty;
			try {
				string[] identifierParts = Connector.Helper.Prometheus.DecodeIdentifier( serverIdentifier );
				jobName = identifierParts[ 0 ];
				instanceAddress = identifierParts[ 1 ];
			} catch ( Exception ) {
				logger.LogError( "Failed to decode server identifier '{0}' for HTTP request '{1}' '{2}' from '{3}'", serverIdentifier, requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new JsonObject() {
					{ "property", "server" }
				} );
			}
			logger.LogDebug( "Job: '{0}', Instance: '{1}'", jobName, instanceAddress );

			// Ensure this request is for this server
			string myInstanceAddress = string.Concat( configuration.PrometheusListenAddress, ":", configuration.PrometheusListenPort );
			if ( instanceAddress != myInstanceAddress ) {
				logger.LogWarning( "Mismatching server instance address '{0}' (expected '{1}') for HTTP request '{1}' '{2}' from '{3}'", instanceAddress, myInstanceAddress, requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.WrongServer, data: new JsonObject() {
					{ "address", myInstanceAddress }
				} );
			}

			// Handle the request appropriately
			try {
				if ( requestMethod == "GET" && requestPath == "/server" ) return ReturnServerActions( response );
				else if ( requestMethod == "GET" && requestPath == "/service" ) return ReturnServiceActions( response, serviceName! );
				else if ( requestMethod == "POST" && requestPath == "/server" ) return ExecuteServerAction( response, actionName! );
				else if ( requestMethod == "POST" && requestPath == "/service" ) return ExecuteServiceAction( response, serviceName!, actionName! );
			} catch ( Exception exception ) {
				logger.LogError( exception, "Error handling HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.InternalServerError, errorCode: ErrorCode.UncaughtServerError );
			}

			// Unknown route
			logger.LogWarning( "Unknown route for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
			return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownRoute );

		}

		// Returns a list of actions this server supports
		// NOTE: These are always true, because if this code is running then the server is obviously running too
		private HttpListenerResponse ReturnServerActions( HttpListenerResponse response ) =>
			Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "shutdown", true },
				{ "reboot", true }
			} );

		// Returns a list of actions the service supports
		private HttpListenerResponse ReturnServiceActions( HttpListenerResponse response, string serviceName ) {

			// Get services for the current operating system
			List<Services.Service> services = new();
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				services = Services.GetServicesForWindows( configuration ).ToList();
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				services.AddRange( Services.GetServicesForLinux( "system" ) );
				services.AddRange( Services.GetServicesForLinux( "user" ) );
			} else throw new PlatformNotSupportedException( $"Unsupported operating system '{ RuntimeInformation.OSDescription }'" );

			// Try to find the service
			Services.Service? service = services.FirstOrDefault( service => service.Name == serviceName );
			if ( service == null ) {
				logger.LogWarning( "Unknown service '{0}'", serviceName );
				return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServiceNotFound, data: new JsonObject() {
					{ "service", serviceName }
				} );
			}

			// Return the actions
			return Response.SendJson( response, data: new JsonObject() {
				{ "start", service?.StatusCode != 0 },
				{ "stop", service?.StatusCode == 1 },
				{ "restart", service?.StatusCode != 0 }
			} );

		}

		// Executes the specified action on the server
		private HttpListenerResponse ExecuteServerAction( HttpListenerResponse response, string actionName ) {
			// Create the command
			Process command = new() {
				StartInfo = new() {
					FileName = "shutdown", // Always the same on Windows & Linux, even for reboot
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};

			// Set the arguments for the command for Windows - https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/shutdown
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) == true ) {
				if ( actionName == "shutdown" ) command.StartInfo.Arguments = "/s /t 60"; // Delay of 1 minute
				else if ( actionName == "reboot" ) command.StartInfo.Arguments = "/r /t 60"; // Delay of 1 minute
				else {
					logger.LogWarning( "Unknown server action '{0}'", actionName );
					return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownAction, data: new JsonObject() {
						{ "action", actionName }
					} );
				}

			// Set the arguments for the command for Linux - https://linux.die.net/man/8/shutdown
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) == true ) {
				if ( actionName == "shutdown" ) command.StartInfo.Arguments = "-P +1m"; // Delay of 1 minute
				else if ( actionName == "reboot" ) command.StartInfo.Arguments = "-r +1m"; // Delay of 1 minute
				else {
					logger.LogWarning( "Unknown server action '{0}'", actionName );
					return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownAction, data: new JsonObject() {
						{ "action", actionName }
					} );
				}
			
			// Fail if unsupported operating system
			} else throw new PlatformNotSupportedException( $"Unsupported operating system '{ RuntimeInformation.OSDescription }'" );

			// Run the command & store all output
			logger.LogInformation( "Executing command '{0}' '{1}' for server action '{2}'", command.StartInfo.FileName, command.StartInfo.Arguments, actionName );
			command.Start();
			string outputText = command.StandardOutput.ReadToEnd();
			string errorText = command.StandardError.ReadToEnd();
			command.WaitForExit();

			// Respond with the results
			return Response.SendJson( response, data: new JsonObject() {
				{ "exitCode", command.ExitCode },
				{ "outputText", outputText },
				{ "errorText", errorText }
			} );

		}

		// Executes the specified action on the service
		private HttpListenerResponse ExecuteServiceAction( HttpListenerResponse response, string serviceName, string actionName ) {

			// Get services for the current operating system
			List<Services.Service> services = new();
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				services = Services.GetServicesForWindows( configuration ).ToList();
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				services.AddRange( Services.GetServicesForLinux( "system" ) );
				services.AddRange( Services.GetServicesForLinux( "user" ) );
			} else throw new PlatformNotSupportedException( $"Unsupported operating system '{ RuntimeInformation.OSDescription }'" );

			// Try to find the service
			Services.Service? service = services.FirstOrDefault( service => service.Name == serviceName );
			if ( service == null ) {
				logger.LogWarning( "Unknown service '{0}'", serviceName );
				return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServiceNotFound, data: new JsonObject() {
					{ "service", serviceName }
				} );
			}
			logger.LogTrace( "Found service '{0}'", service?.Name );

			// Windows...
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) == true ) {

				#pragma warning disable CA1416 // IDE thinks we aren't running on Windows here, despite the check above!

				// Find the service controller
				ServiceController? serviceController = ServiceController.GetServices().FirstOrDefault( serviceController => serviceController.ServiceName == service?.Name );

				if ( serviceController == null ) {
					logger.LogWarning( "Unknown service '{0}'", serviceName );
					return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServiceNotFound, data: new JsonObject() {
						{ "service", serviceName }
					} );
				}

				// Try to execute the action
				try {
					TimeSpan timeout = TimeSpan.FromSeconds( 30 ); // Sometimes Windows can take its time...

					// Start the service..
					if ( actionName == "start" ) {
						logger.LogInformation( "Starting service '{0}'", serviceController.ServiceName );
						serviceController.Start();
						logger.LogDebug( "Waiting for service '{0}' to start", serviceController.ServiceName );
						serviceController.WaitForStatus( ServiceControllerStatus.Running, timeout );
						logger.LogInformation( "Started service '{0}'", serviceController.ServiceName );

					// Stop the service...
					} else if ( actionName == "stop" ) {
						logger.LogInformation( "Stopping service '{0}'", serviceController.ServiceName );
						serviceController.Stop();
						logger.LogDebug( "Waiting for service '{0}' to stop", serviceController.ServiceName );
						serviceController.WaitForStatus( ServiceControllerStatus.Stopped, timeout );
						logger.LogInformation( "Stopped service '{0}'", serviceController.ServiceName );

					// Restart the service...
					} else if ( actionName == "restart" ) {
						logger.LogInformation( "Stopping service '{0}'", serviceController.ServiceName );
						serviceController.Stop();
						logger.LogDebug( "Waiting for service '{0}' to stop", serviceController.ServiceName );
						serviceController.WaitForStatus( ServiceControllerStatus.Stopped, timeout );
						logger.LogInformation( "Stopped service '{0}'", serviceController.ServiceName );

						logger.LogInformation( "Starting service '{0}'", serviceController.ServiceName );
						serviceController.Start();
						logger.LogDebug( "Waiting for service '{0}' to start", serviceController.ServiceName );
						serviceController.WaitForStatus( ServiceControllerStatus.Running, timeout );
						logger.LogInformation( "Started service '{0}'", serviceController.ServiceName );

					// Unknown action
					} else {
						logger.LogWarning( "Unknown service action '{0}'", actionName );
						return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownAction, data: new JsonObject() {
							{ "action", actionName }
						} );
					}

				// Something bad happened...
				} catch ( Exception exception ) {
					logger.LogError( exception, "Failed to execute service action '{0}' on service '{1}'", actionName, serviceName );
					return Response.SendJson( response, data: new JsonObject() {
						{ "exitCode", 1 },
						{ "outputText", string.Empty },
						{ "errorText", exception.Message }
					} );
				}

				// If we got here, the action was successful
				return Response.SendJson( response, data: new JsonObject() {
					{ "exitCode", 0 },
					{ "outputText", string.Empty },
					{ "errorText", string.Empty }
				} );

				#pragma warning restore CA1416

			// Linux...
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) == true ) {

				// Create the command
				Process command = new() {
					StartInfo = new() {
						FileName = "systemctl",
						Arguments = $"{ actionName } { serviceName }",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};

				// Run the command & store all output
				logger.LogInformation( "Executing command '{0}' '{1}' for service action '{2}' on service '{3}'", command.StartInfo.FileName, command.StartInfo.Arguments, actionName, serviceName );
				command.Start();
				string outputText = command.StandardOutput.ReadToEnd();
				string errorText = command.StandardError.ReadToEnd();
				command.WaitForExit();

				// Respond with the results
				return Response.SendJson( response, data: new JsonObject() {
					{ "exitCode", command.ExitCode },
					{ "outputText", outputText },
					{ "errorText", errorText }
				} );

			// Fail if unsupported operating system
			} else throw new PlatformNotSupportedException( $"Unsupported operating system '{ RuntimeInformation.OSDescription }'" );

		}

	}

}
