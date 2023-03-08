using System;
using System.Web;
using System.Net;
using System.Text.Json.Nodes;
using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;

namespace ServerMonitor.Connector.Route {

	public static class Service {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Service" );

		// TODO: Executing an action on a server
		[ Route( "POST", "/service" ) ]
		public static HttpListenerResponse OnPostRequest( HttpListenerRequest request, HttpListenerResponse response, HttpListener listener, HttpListenerContext context ) {
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			NameValueCollection queryParameters = HttpUtility.ParseQueryString( queryString );
			string? serverIdentifier = queryParameters.Get( "server" );
			string? serviceName = queryParameters.Get( "name" );
			string? actionName = queryParameters.Get( "action" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "server" }
			} );
			if ( string.IsNullOrWhiteSpace( serviceName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "name" }
			} );
			if ( string.IsNullOrWhiteSpace( actionName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "action" }
			} );

			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "success", true },
				{ "response", "This is the output of the action." }
			} );
		}

	}

}