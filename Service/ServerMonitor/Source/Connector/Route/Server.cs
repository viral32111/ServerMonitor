using System;
using System.Web;
using System.Net;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;
using viral32111.JsonExtensions;

namespace ServerMonitor.Connector.Route {

	public static class Server {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Server" );

		#pragma warning disable CS1998 // Async method lacks await operators and will run synchronously

		// Returns all metrics data for a specific server
		[ Route( "GET", "/server" ) ]
		public static async Task<HttpListenerResponse> OnGetRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {

			// Ensure query parameters were provided
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			// Try extract the server identifier from the query parameters
			string? serverIdentifier = HttpUtility.ParseQueryString( queryString ).Get( "id" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "id" }
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
			if ( server == null ) return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServerNotFound, data: new JsonObject() { { "id", serverIdentifier } } );

			// TODO
			server[ "supportedActions" ] = new JsonObject() {
				{ "shutdown", false },
				{ "reboot", false }
			};

			// Stop here if the server is offline
			if ( server.NestedGet<double>( "uptimeSeconds" ) == -1 ) return Response.SendJson( response, statusCode: HttpStatusCode.ServiceUnavailable, errorCode: ErrorCode.ServerOffline, data: server );

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

		// TODO: Executing an action on a server
		[ Route( "POST", "/server" ) ]
		public static async Task<HttpListenerResponse> OnPostRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			NameValueCollection queryParameters = HttpUtility.ParseQueryString( queryString );
			string? serverIdentifier = queryParameters.Get( "id" );
			string? actionName = queryParameters.Get( "action" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "id" }
			} );
			if ( string.IsNullOrWhiteSpace( actionName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "action" }
			} );

			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "success", true },
				{ "response", "This is the output of the action." }
			} );
		}

		#pragma warning restore CS1998 // Async method lacks await operators and will run synchronously

	}

}
