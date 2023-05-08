using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Connector.Helper {
	
	public static class Response {

		private static readonly ILogger logger = Logging.CreateLogger( "Connector/Helper/Response" );

		public static void SendText( HttpListenerResponse response, HttpStatusCode statusCode, string body ) {
			response.StatusCode = ( int ) statusCode;
			response.ContentType = "text/plain; charset=utf-8";
			response.OutputStream.Write( Encoding.UTF8.GetBytes( body ) );
			response.Close();
		}

		public static HttpListenerResponse SendJson( HttpListenerResponse response, HttpStatusCode? statusCode = null, JsonObject? data = null, ErrorCode? errorCode = null ) {
			JsonObject payload = new() {
				{ "errorCode", ( int ) ( errorCode ?? ErrorCode.Success ) },
				{ "data", data }
			};

			response.StatusCode = ( int ) ( statusCode ?? HttpStatusCode.OK );
			response.ContentType = "application/json; charset=utf-8";
			response.OutputStream.Write( Encoding.UTF8.GetBytes( payload.ToJsonString() ) );
			response.Close();

			return response;
		}
	
	}

}
