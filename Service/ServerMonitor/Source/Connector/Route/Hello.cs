using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;

namespace ServerMonitor.Connector.Route {

	public static class Hello {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Hello" );

		[ Route( "GET", "/hello" ) ]
		public static void OnRequest( HttpListenerRequest request, HttpListenerResponse response, HttpListener listener, HttpListenerContext context ) {
			logger.LogInformation( "Received hello world request from '{0}'", request.RemoteEndPoint.Address.ToString() );

			Response.SendJson( response, data: new JsonObject() {
				{ "message", "Hello World!" }
			} );
		}

	}

}
