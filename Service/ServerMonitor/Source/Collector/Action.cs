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
			Listening = Metrics.CreateGauge( $"{ configuration.PrometheusMetricsPrefix }_collector_action_listening", "IP address & port of the HTTP listener", new GaugeConfiguration {
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
			httpListener.Stop();
			httpListenerLoopCompletionSource.SetResult();
			httpListenerLoopTask?.Wait();
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
			if ( requestMethod == "GET" && requestPath == "/server" ) return ReturnServerActions( response );
			else if ( requestMethod == "GET" && requestPath == "/service" ) return ReturnServiceActions( response, serviceName! );
			else if ( requestMethod == "POST" && requestPath == "/server" ) return ExecuteServerAction( response, actionName! );
			else if ( requestMethod == "POST" && requestPath == "/service" ) return ExecuteServiceAction( response, serviceName!, actionName! );

			// Unknown route
			logger.LogWarning( "Unknown route for HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
			return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownRoute );

		}

		// TODO: Return a list of actions this server supports
		private HttpListenerResponse ReturnServerActions( HttpListenerResponse response ) {
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "actions", new JsonObject() {
					{ "shutdown", false },
					{ "reboot", false }
				} }
			} );
		}

		// TODO: Return a list of actions the service supports
		private HttpListenerResponse ReturnServiceActions( HttpListenerResponse response, string serviceName ) {
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "actions", new JsonObject() {
					{ "start", false },
					{ "stop", false },
					{ "restart", false }
				} }
			} );
		}

		// TODO: Execute the specified action on the server
		private HttpListenerResponse ExecuteServerAction( HttpListenerResponse response, string actionName ) {
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "success", false },
				{ "message", "Hello World" }
			} );
		}

		// TODO: Execute the specified action on the service
		private HttpListenerResponse ExecuteServiceAction( HttpListenerResponse response, string serviceName, string actionName ) {
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "success", false },
				{ "message", "Hello World" }
			} );
		}

	}

}
