using System;
using System.Web;
using System.Net;
using System.Linq;
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
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "id" }
			} );

			string? serviceName = HttpUtility.ParseQueryString( queryString ).Get( "name" );
			if ( string.IsNullOrWhiteSpace( serviceName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "name" }
			} );

			string? actionName = HttpUtility.ParseQueryString( queryString ).Get( "action" );
			if ( string.IsNullOrWhiteSpace( actionName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "action" }
			} );

			// Try decode the server identifier
			string jobName = string.Empty, instanceAddress = string.Empty;
			try {
				string[] serverIdentifierParts = Helper.Prometheus.DecodeIdentifier( serverIdentifier );
				jobName = serverIdentifierParts[ 0 ];
				instanceAddress = serverIdentifierParts[ 1 ];
			} catch ( FormatException ) {
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new JsonObject() {
					{ "parameter", "id" }
				} );
			}

			// Try fetch the server
			JsonObject? server = ( await Helper.Prometheus.FetchServers( configuration ) ).FirstOrDefault( server => server.NestedGet<string>( "identifier" ) == serverIdentifier );
			if ( server == null ) return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServerNotFound, data: new JsonObject() {
				{ "id", serverIdentifier }
			} );

			// Stop here if the server is offline
			if ( server.NestedGet<double>( "uptimeSeconds" ) == -1 ) return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ServerOffline, data: new JsonObject() {
				{ "id", serverIdentifier }
			} );

			// Fetch the services
			JsonObject[] services = await Helper.Prometheus.FetchServices( configuration, jobName, instanceAddress );

			// Ensure the service exists
			if ( services.Any( service => service.NestedGet<string>( "name" ) == serviceName ) == false ) return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServiceNotFound, data: new JsonObject() {
				{ "id", serverIdentifier },
				{ "name", serviceName }
			} );

			// Ensure the action is valid
			// TODO: Check the array of supportedActions for the service
			if ( actionName != "reboot" && actionName != "shutdown" ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new JsonObject() {
				{ "parameter", "action" }
			} );

			// TODO: Execute the action
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "success", true },
				{ "response", "This is the output of the action." }
			} );

		}

	}

}
