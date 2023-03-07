using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Encodings;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using Mono.Unix.Native; // https://github.com/mono/mono.posix
using Prometheus; // https://github.com/prometheus-net/prometheus-net

namespace ServerMonitor.Connector {

	public static class Connector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );
		
		public static void HandleCommand( Config configuration, bool singleRun ) {
			logger.LogInformation( "Launched in connection point mode" );

			HttpListener httpListener = new HttpListener();
			logger.LogDebug( "Created HTTP listener" );
			//httpListener.AuthenticationSchemes = AuthenticationSchemes.Basic;

			string prefix = $"http://{ configuration.ConnectorListenAddress }:{ configuration.ConnectorListenPort }/";
			httpListener.Prefixes.Add( prefix );
			logger.LogDebug( "Added HTTP listener prefix: '{0}'", prefix );

			httpListener.Start();
			logger.LogDebug( "Started HTTP listener" );

			while ( httpListener.IsListening == true ) {
				HttpListenerContext context = httpListener.GetContext();
				HttpListenerRequest request = context.Request;
				HttpListenerResponse response = context.Response;
				logger.LogDebug( "Received HTTP request: {0} {1}", request.HttpMethod, request.Url );

				byte[] responseBytes = Encoding.UTF8.GetBytes( "Hello World!" );
				response.ContentLength64 = responseBytes.Length;

				Stream output = response.OutputStream;
				output.Write( responseBytes, 0, responseBytes.Length );
				output.Close();
				logger.LogDebug( "Sent HTTP response: {0}", response.StatusCode );
			}

			httpListener.Stop();
			logger.LogDebug( "Stopped HTTP listener" );
		}

	}

}
