using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.Encodings;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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
			
			httpListener.AuthenticationSchemes = AuthenticationSchemes.Basic;

			string prefix = $"http://{ configuration.ConnectorListenAddress }:{ configuration.ConnectorListenPort }/";
			httpListener.Prefixes.Add( prefix );
			logger.LogDebug( "Added HTTP listener prefix: '{0}'", prefix );

			httpListener.Start();
			logger.LogDebug( "Started HTTP listener" );

			Dictionary<string, string> credentials = configuration.ConnectorCredentials.ToDictionary(
				credential => credential.Username,
				credential => credential.Password
			);

			while ( httpListener.IsListening == true ) {
				IAsyncResult asyncResult = httpListener.BeginGetContext( new AsyncCallback( OnHttpRequest ), new State {
					Listener = httpListener,
					Credentials = credentials
				} );
				asyncResult.AsyncWaitHandle.WaitOne();
			}

			httpListener.Stop();
			logger.LogDebug( "Stopped HTTP listener" );
		}

		private static void OnHttpRequest( IAsyncResult asyncResult ) {
			if ( asyncResult.AsyncState == null ) throw new Exception( $"Invalid state '{ asyncResult.AsyncState }' passed to async callback" );
			State state = ( State ) asyncResult.AsyncState;
			HttpListener httpListener = state.Listener;
			Dictionary<string, string> credentials = state.Credentials;

			HttpListenerContext context = httpListener.EndGetContext( asyncResult );
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			logger.LogDebug( "Received HTTP request: {0} {1}", request.HttpMethod, request.Url );

			HttpListenerBasicIdentity? basicIdentity = ( HttpListenerBasicIdentity? ) context.User?.Identity;
			if ( basicIdentity == null ) {
				logger.LogDebug( "Received HTTP request without authentication" );
				response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
				response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
				response.Close();
				return;
			} else {
				string usernameAttempt = basicIdentity.Name;
				string passwordAttempt = basicIdentity.Password;

				if (
					credentials.TryGetValue( usernameAttempt, out string? actualPassword ) == false ||
					string.IsNullOrWhiteSpace( actualPassword ) == true ||
					passwordAttempt != actualPassword
				) {
					logger.LogDebug( "Received HTTP request with invalid authentication" );
					response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
					response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
					response.Close();
					return;
				}
			}

			byte[] responseBytes = Encoding.UTF8.GetBytes( "Hello World!" );
			response.ContentLength64 = responseBytes.Length;

			Stream output = response.OutputStream;
			output.Write( responseBytes, 0, responseBytes.Length );
			output.Close();
			logger.LogDebug( "Sent HTTP response: {0}", response.StatusCode );
		}

	}

	public struct State {
		public HttpListener Listener;
		public Dictionary<string, string> Credentials;
	}

}
