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

	public static class Server {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Server" );

		// Returns all metrics data for a specific server
		[ Route( "GET", "/server" ) ]
		public static async Task<HttpListenerResponse> OnGetRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {

			// Ensure query parameters were provided
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			// Try extract the server identifier from the query parameters
			string? serverIdentifier = HttpUtility.ParseQueryString( queryString ).Get( "id" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new() {
				{ "parameter", "id" }
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

			// Fetch information about the action server
			JsonObject? actionServer = await Helper.Prometheus.FetchActionServer( configuration, jobName, instanceAddress );
			if ( actionServer == null ) return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ActionServerUnknown, data: new() {
				{ "id", serverIdentifier }
			} );
			string actionServerAddress = actionServer.NestedGet<string>( "address" );
			int actionServerPort = actionServer.NestedGet<int>( "port" );
			logger.LogTrace( "Action server address: '{0}', port: '{1}'", actionServerAddress, actionServerPort );

			// Add the supported actions
			server[ "supportedActions" ] = await FetchSupportedActions( configuration, actionServerAddress, actionServerPort, serverIdentifier );

			// Add metrics for resources
			server[ "resources" ] = new JsonObject() {
				{ "processor", await Helper.Prometheus.FetchProcessor( configuration, jobName, instanceAddress ) },
				{ "memory", await Helper.Prometheus.FetchMemory( configuration, jobName, instanceAddress ) },
				{ "drives", JSON.CreateJsonArray( await Helper.Prometheus.FetchDrives( configuration, jobName, instanceAddress ) ) },
				{ "networkInterfaces", JSON.CreateJsonArray( await Helper.Prometheus.FetchNetworkInterfaces( configuration, jobName, instanceAddress ) ) },

				{ "power", new JsonObject() }, // TODO
				{ "fans", new JsonArray() } // TODO
			};

			// Add metrics for services & Docker containers
			server[ "services" ] = JSON.CreateJsonArray( await Helper.Prometheus.FetchServices( configuration, jobName, instanceAddress ) );
			server[ "dockerContainers" ] = JSON.CreateJsonArray( await Helper.Prometheus.FetchDockerContainers( configuration, jobName, instanceAddress, server.NestedGet<long>( "lastScrape" ) ) );

			// Add metrics for SNMP
			server[ "snmp" ] = new JsonObject() {
				{ "community", configuration.SNMPCommunity },
				{ "agents", JSON.CreateJsonArray( await Helper.Prometheus.FetchSNMPAgents( configuration, jobName, instanceAddress ) ) }
			};

			// Return all this data
			return Response.SendJson( response, data: server );

		}

		// Executes an action on a server
		[ Route( "POST", "/server" ) ]
		public static async Task<HttpListenerResponse> OnPostRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {

			// Ensure query parameters were provided
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			// Try extract the server identifier & action name from the query parameters
			string? serverIdentifier = HttpUtility.ParseQueryString( queryString ).Get( "id" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new() {
				{ "parameter", "id" }
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

			// Fetch information about the action server
			JsonObject? actionServer = await Helper.Prometheus.FetchActionServer( configuration, jobName, instanceAddress );
			if ( actionServer == null ) return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ActionServerUnknown, data: new() {
				{ "id", serverIdentifier }
			} );
			string actionServerAddress = actionServer.NestedGet<string>( "address" );
			int actionServerPort = actionServer.NestedGet<int>( "port" );

			// Fetch the supported actions
			JsonObject supportedActions = await FetchSupportedActions( configuration, actionServerAddress, actionServerPort, serverIdentifier );

			// Ensure the action is valid
			if ( supportedActions.ContainsKey( actionName ) == false ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new() {
				{ "parameter", "action" },
				{ "supportedActions", supportedActions }
			} );

			// Ensure the action can be executed
			if ( supportedActions.NestedGet<bool>( actionName ) == false ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.ActionNotExecutable, data: new() {
				{ "action", actionName }
			} );

			// Create the HTTP request to send to the action server
			HttpRequestMessage httpRequest = new() {
				Method = HttpMethod.Post,
				RequestUri = new Uri( $"{ ( actionServerPort == 443 ? "https" : "http" ) }://{ actionServerAddress }:{ actionServerPort }/server" ),
				Content = new StringContent( ( new JsonObject() {
					{ "server", serverIdentifier },
					{ "action", actionName }
				} ).ToJsonString(), Encoding.UTF8, "application/json" )
			};

			// Add the authentication key, if configured
			if ( string.IsNullOrWhiteSpace( configuration.CollectorActionAuthenticationKey ) == false ) {
				httpRequest.Headers.Authorization = new AuthenticationHeaderValue( "Key", configuration.CollectorActionAuthenticationKey );
			}

			// Send the HTTP request...
			using ( HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest ) ) {
				logger.LogDebug( "Sent execute action HTTP request '{0}' '{1}'", httpRequest.Method, httpRequest.RequestUri );
				// TODO: httpResponse.EnsureSuccessStatusCode();

				// Parse the response
				string responseContent = await httpResponse.Content.ReadAsStringAsync();
				JsonObject? responsePayload = JsonSerializer.Deserialize<JsonObject>( responseContent );
				if ( responsePayload == null ) throw new Exception( $"Failed to parse execute action response '{ responseContent }' as JSON" );

				// Ensure the required properties exist
				if ( responsePayload.NestedHas( "errorCode" ) == false ) throw new Exception( $"Missing error code property in execute action response payload" );
				if ( responsePayload.NestedHas( "data" ) == false ) throw new Exception( $"Missing data property in execute action response payload" );

				// Easy access to the required properties
				int errorCode = responsePayload.NestedGet<int>( "errorCode" );
				JsonObject data = responsePayload.NestedGet<JsonObject>( "data" );
				logger.LogDebug( "Error Code: '{0}', Data: '{1}'", errorCode, data.ToJsonString() );

				// TODO: Ensure success
				//if ( responsePayload.NestedGet<int>( "errorCode" ) != ( int ) ErrorCode.Success ) throw new Exception( $"Failed to execute action '{ actionName }'" );

				// Respond with the data (as a copy, not a reference)
				return Response.SendJson( response, statusCode: HttpStatusCode.OK, errorCode: ErrorCode.Success, data: data.Clone()!.AsObject() );

			}

		}

		// Fetches the supported actions for a server
		private static async Task<JsonObject> FetchSupportedActions( Config configuration, string actionServerAddress, int actionServerPort, string serverIdentifier ) {

			// Create the HTTP request
			HttpRequestMessage httpRequest = new() {
				Method = HttpMethod.Get,
				RequestUri = new Uri( $"{ ( actionServerPort == 443 ? "https" : "http" ) }://{ actionServerAddress }:{ actionServerPort }/server" ),
				Content = new StringContent( ( new JsonObject() {
					{ "server", serverIdentifier }
				} ).ToJsonString(), Encoding.UTF8, "application/json" )
			};

			// Add the authentication key, if configured
			if ( string.IsNullOrWhiteSpace( configuration.CollectorActionAuthenticationKey ) == false ) {
				httpRequest.Headers.Authorization = new AuthenticationHeaderValue( "Key", configuration.CollectorActionAuthenticationKey );
			}

			// Send the HTTP request...
			logger.LogDebug( "Sending fetch supported actions for server '{0}' ('{1}' '{2}')", serverIdentifier, httpRequest.Method, httpRequest.RequestUri );
			using ( HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest ) ) {
				// TODO: httpResponse.EnsureSuccessStatusCode();

				// Parse the response
				string responseContent = await httpResponse.Content.ReadAsStringAsync();
				JsonObject? responsePayload = JsonSerializer.Deserialize<JsonObject>( responseContent );
				if ( responsePayload == null ) throw new Exception( $"Failed to parse response '{ responseContent }' as JSON" );

				// Ensure the required properties exist
				if ( responsePayload.NestedHas( "errorCode" ) == false ) throw new Exception( $"Missing error code property in response payload" );
				if ( responsePayload.NestedHas( "data" ) == false ) throw new Exception( $"Missing data property in response payload" );

				// Easy access to the required properties
				int errorCode = responsePayload.NestedGet<int>( "errorCode" );
				JsonObject data = responsePayload.NestedGet<JsonObject>( "data" );
				logger.LogDebug( "Error Code: '{0}', Data: '{1}'", errorCode, data.ToJsonString() );

				// TODO: Ensure success
				//if ( responsePayload.NestedGet<int>( "errorCode" ) != ( int ) ErrorCode.Success ) throw new Exception( "Failed to fetch supported actions" );

				// Return the data (as a copy, not a reference)
				return data.Clone()!.AsObject();

			}

		}

	}

}
