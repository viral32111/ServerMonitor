using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;

namespace ServerMonitor.Connector.Route {

	public static class Hello {

		private static readonly ILogger logger = Logging.CreateLogger( "Connector/Routes/Hello" );

		#pragma warning disable CS1998 // Async method lacks await operators and will run synchronously

		// Used to test if an instance is running & valid
		[ Route( "GET", "/hello" ) ]
		public static async Task<HttpListenerResponse> OnGetRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {
			logger.LogInformation( "Hello, {0}!", context.User!.Identity!.Name );

			return Response.SendJson( response, data: new() {
				{ "message", "Hello World!" },
				{ "user", context.User!.Identity!.Name },
				{ "version", Program.Version },
				{ "contact", new JsonObject() {
					{ "name", configuration.ContactName },
					{ "methods", Helper.JSON.CreateJsonArray( configuration.ContactMethods ) }
				} }
			} );
		}

		// Redirect to the /hello endpoint
		[ Route( "GET", "/" ) ]
		public static async Task<HttpListenerResponse> OnIndexRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {
			response.Redirect( "/hello" );
			return response;
		}

		#pragma warning restore CS1998 // Async method lacks await operators and will run synchronously

	}

}
