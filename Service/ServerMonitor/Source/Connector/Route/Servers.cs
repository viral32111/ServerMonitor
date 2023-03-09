using System;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;

namespace ServerMonitor.Connector.Route {

	public static class Servers {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Servers" );

		// TODO: Return an array of basic server data objects
		[ Route( "GET", "/servers" ) ]
		public static HttpListenerResponse OnGetRequest( HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonArray() {
				new JsonObject() {
					{ "id", "abcdefghijklmnopqrstuvwxyz" },
					{ "name", "DEBIAN-SERVER-01" },
					{ "uptimeSeconds", 60 * 60 * 24 * 7 },
					{ "lastUpdate", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
				},
				new JsonObject() {
					{ "id", "123456789jklmnopqrstuvwxyz" },
					{ "name", "WINDOWS-DC-02" },
					{ "uptimeSeconds", 60 * 60 * 24 * 7 },
					{ "lastUpdate", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
				},
				new JsonObject() {
					{ "id", "987654321jklmnopqrstuvwxyz" },
					{ "name", "UBUNTU-VIRT-03" },
					{ "uptimeSeconds", -1 },
					{ "lastUpdate", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
				}
			} );
		}

	}

}
