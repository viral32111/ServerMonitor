using System;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Connector.Routes {

	public static class Hello {

		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Hello" );

		[ Route( "GET", "/hello" ) ]
		public static void OnRequest( HttpListenerRequest request, HttpListenerResponse response, HttpListener listener, HttpListenerContext context ) {
			logger.LogInformation( "Received hello world request from '{0}'", request.RemoteEndPoint.Address.ToString() );

			response.StatusCode = ( int ) HttpStatusCode.OK;
			response.AddHeader( "Content-Type", "text/plain" );
			response.OutputStream.Write( Encoding.UTF8.GetBytes( "Hello World!" ) );
			response.Close();
		}

	}

}
