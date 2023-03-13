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

namespace ServerMonitor.Collector {

	// Encapsulates the RESTful API for actions
	public class Action {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Action" );

		// Everything for the HTTP listener
		private static readonly HttpListener httpListener = new();
		private static TaskCompletionSource httpListenerLoopCompletionSource = new();
		private static Task? httpListenerLoopTask = null;

		// The configured authentication key, if any
		private static string? authenticationKey = null;

		// Setup everything when instantiated
		public Action( Config configuration ) {

			// Set the authentication key, if one is configured
			authenticationKey = string.IsNullOrWhiteSpace( configuration.CollectorActionAuthenticationKey ) == false ? configuration.CollectorActionAuthenticationKey : null;
			if ( authenticationKey == null ) logger.LogWarning( "Authentication key is empty, requests will NOT require authentication!" );

			// Setup the API routes for the HTTP listener
			string baseUrl = $"http://{ configuration.CollectorActionListenAddress }:{ configuration.CollectorActionListenPort }";
			foreach ( string route in new string[] { "/server", "/service" } ) httpListener.Prefixes.Add( string.Concat( baseUrl, route, "/" ) );

		}

		// Starts the HTTP listener
		public void StartListening() {
			if ( httpListener.IsListening == true ) throw new Exception( "HTTP listener is already listening" );

			httpListener.Start();
			httpListenerLoopTask = Task.Run( ListenerLoop ); // Start in the background
			logger.LogDebug( "Started HTTP listener" );
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
					Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.InvalidAuthentication );
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

			// Ensure the request is POST
			if ( requestMethod != "POST" ) {
				logger.LogWarning( "Bad method '{0}' for HTTP request '{1}' '{2}' from '{3}'", requestMethod, requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.MethodNotAllowed, errorCode: ErrorCode.MethodNotAllowed, data: new JsonObject() {
					{ "expected", "GET" }
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

			// Ensure the request payload contains the action name
			if ( requestPayload.ContainsKey( "action" ) == false || string.IsNullOrWhiteSpace( requestPayload[ "action" ]?.GetValue<string?>() ) == true ) {
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
			string actionName = requestPayload[ "action" ]!.GetValue<string>();
			string? serviceName = requestPath == "/service" ? requestPayload[ "service" ]!.GetValue<string>() : null;
			logger.LogDebug( "Server: '{0}', Action: '{1}', Service: '{2}'", serverIdentifier, actionName, serviceName );

			// TODO: Implement the rest of the API
			response.StatusCode = ( int ) HttpStatusCode.NotImplemented;
			response.OutputStream.Write( Encoding.UTF8.GetBytes( "Example" ) );
			response.Close();

			return response;

		}

	}

}
