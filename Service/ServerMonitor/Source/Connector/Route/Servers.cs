using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;

namespace ServerMonitor.Connector.Route {

	// Route request handlers for /servers
	public static class Servers {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Servers" );

		// Returns a list of all servers
		[ Route( "GET", "/servers" ) ]
		public static async Task<HttpListenerResponse> OnGetRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {
			return Response.SendJson( response, data: new() {
				{ "servers", JSON.CreateJsonArray( await Helper.Prometheus.FetchServers( configuration ) ) }
			} );
		}

	}

}
