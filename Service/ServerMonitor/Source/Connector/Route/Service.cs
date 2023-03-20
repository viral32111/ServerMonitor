using System;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;
using viral32111.JsonExtensions;

namespace ServerMonitor.Connector.Route {

	public static class Service {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Service" );

		// Executes an action on a service
		[ Route( "POST", "/service" ) ]
		public static async Task<HttpListenerResponse> OnPostRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {

			// Ensure query parameters were provided
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			// Try extract the server identifier, service name & action name from the query parameters
			string? serverIdentifier = HttpUtility.ParseQueryString( queryString ).Get( "id" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new() {
				{ "parameter", "id" }
			} );

			string? serviceName = HttpUtility.ParseQueryString( queryString ).Get( "name" );
			if ( string.IsNullOrWhiteSpace( serviceName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new() {
				{ "parameter", "name" }
			} );

			string? actionName = HttpUtility.ParseQueryString( queryString ).Get( "action" );
			if ( string.IsNullOrWhiteSpace( actionName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new() {
				{ "parameter", "action" }
			} );

			// Try decode the server identifier
			string jobName = string.Empty, instanceAddress = string.Empty;
			try {
				string[] serverIdentifierParts = Helper.Prometheus.DecodeIdentifier( serverIdentifier );
				jobName = serverIdentifierParts[ 0 ];
				instanceAddress = serverIdentifierParts[ 1 ];
			} catch ( FormatException ) {
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new() {
					{ "parameter", "id" }
				} );
			}

			// Try fetch the server
			JsonObject? server = ( await Helper.Prometheus.FetchServers( configuration ) ).FirstOrDefault( server => server.NestedGet<string>( "identifier" ) == serverIdentifier );
			if ( server == null ) return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServerNotFound, data: new() {
				{ "id", serverIdentifier }
			} );

			// Stop here if the server is offline
			if ( server.NestedGet<double>( "uptimeSeconds" ) == -1 ) return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ServerOffline, data: new() {
				{ "id", serverIdentifier }
			} );

			// Fetch the service
			JsonObject? service = ( await Helper.Prometheus.FetchServices( configuration, jobName, instanceAddress ) ).FirstOrDefault( service => service.NestedGet<string>( "service" ) == serviceName );

			// Ensure the service exists
			if ( service == null ) return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServiceNotFound, data: new() {
				{ "id", serverIdentifier },
				{ "name", serviceName }
			} );

			// Ensure the action is valid
			if ( service.NestedGet<JsonObject>( "supportedActions" ).ContainsKey( actionName ) == false ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new() {
				{ "parameter", "action" },
				{ "supportedActions", service.NestedGet<JsonObject>( "supportedActions" ).Clone()!.AsObject() }
			} );

			// Ensure the action can be executed
			if ( service.NestedGet<JsonObject>( "supportedActions" ).NestedGet<bool>( actionName ) == false ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.ActionNotExecutable, data: new() {
				{ "action", actionName }
			} );

			// Fetch information about the action server
			JsonObject? actionServer = await Helper.Prometheus.FetchActionServer( configuration, jobName, instanceAddress );
			if ( actionServer == null ) return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ActionServerUnknown, data: new() {
				{ "id", serverIdentifier }
			} );
			string actionServerAddress = actionServer.NestedGet<string>( "address" );
			int actionServerPort = actionServer.NestedGet<int>( "port" );

			// Create the HTTP request to send to the action server
			HttpRequestMessage httpRequest = new() {
				Method = HttpMethod.Post,
				RequestUri = new Uri( $"{ ( actionServerPort == 443 ? "https" : "http" ) }://{ actionServerAddress }:{ actionServerPort }/service" ),
				Content = new StringContent( ( new JsonObject() {
					{ "server", serverIdentifier },
					{ "service", serviceName },
					{ "action", actionName }
				} ).ToJsonString(), Encoding.UTF8, "application/json" )
			};

			// Add the authentication key, if configured
			if ( string.IsNullOrWhiteSpace( configuration.CollectorActionAuthenticationKey ) == false ) {
				httpRequest.Headers.Authorization = new AuthenticationHeaderValue( "Key", configuration.CollectorActionAuthenticationKey );
			}

			// Attempt to send the HTTP request...
			try {
				using ( HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest ) ) {
					logger.LogDebug( "Sent execute service action HTTP request '{0}' '{1}'", httpRequest.Method, httpRequest.RequestUri );
					httpResponse.EnsureSuccessStatusCode();

					// Parse the response
					string responseContent = await httpResponse.Content.ReadAsStringAsync();
					JsonObject? responsePayload = JsonSerializer.Deserialize<JsonObject>( responseContent );
					if ( responsePayload == null ) throw new Exception( $"Failed to parse execute action response '{ responseContent }' as JSON" );

					// Ensure the required properties exist
					if ( responsePayload.NestedHas( "errorCode" ) == false ) throw new Exception( $"Missing error code property in execute service action response payload" );
					if ( responsePayload.NestedHas( "data" ) == false ) throw new Exception( $"Missing data property in execute service action response payload" );

					// Easy access to the required properties
					int errorCode = responsePayload.NestedGet<int>( "errorCode" );
					JsonObject data = responsePayload.NestedGet<JsonObject>( "data" );
					logger.LogDebug( "Error Code: '{0}', Data: '{1}'", errorCode, data.ToJsonString() );

					// Ensure success
					if ( responsePayload.NestedGet<int>( "errorCode" ) != ( int ) ErrorCode.Success ) throw new Exception( $"Failed to execute service action '{ actionName }'" );

					// Respond with the data (as a copy, not a reference)
					return Response.SendJson( response, statusCode: HttpStatusCode.OK, errorCode: ErrorCode.Success, data: data.Clone()!.AsObject() );

				}
			} catch ( Exception exception ) {
				logger.LogError( exception, "Failed to fetch supported actions for service '{0}' on server '{1}' ({2})", serviceName, serverIdentifier, exception.Message );
				return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ActionServerOffline, data: new() {
					{ "id", serverIdentifier }
				} );
			}

		}

	}

}
