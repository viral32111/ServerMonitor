using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.Encodings;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Security;
using System.Security.Cryptography;
using System.Collections.Generic;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using Mono.Unix.Native; // https://github.com/mono/mono.posix
using Prometheus; // https://github.com/prometheus-net/prometheus-net

namespace ServerMonitor.Connector {

	public static class Connector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		private static readonly RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();
		
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

			Dictionary<string, string> authenticationCredentials = new();
			foreach ( Credential credential in configuration.ConnectorCredentials ) {
				if ( authenticationCredentials.ContainsKey( credential.Username ) == true ) {
					logger.LogWarning( "Duplicate username '{0}' found in credentials", credential.Username );
					continue;
				}

				logger.LogDebug( "Username: '{0}', Password: '{1}'", credential.Username, credential.Password );

				Match passwordMatch = Regex.Match( credential.Password, @"PBKDF2-(\d+)-(.+)-(.+)" );
				if ( passwordMatch.Success == false ) {
					string hashedPassword = PBKDF2( credential.Password );
					logger.LogWarning( "Password for user '{0}' is NOT hashed yet! The password should be changed to: '{1}'", credential.Username, hashedPassword );
					authenticationCredentials.Add( credential.Username, hashedPassword );
				} else {
					logger.LogDebug( "Password '{0}' for user '{1}' is already hashed", credential.Password, credential.Username );
					authenticationCredentials.Add( credential.Username, credential.Password );
				}
			}

			while ( httpListener.IsListening == true && singleRun == false ) {
				IAsyncResult asyncResult = httpListener.BeginGetContext( new AsyncCallback( OnHttpRequest ), new State {
					Listener = httpListener,
					Credentials = authenticationCredentials
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
			}
				
			string attemptedUsername = basicIdentity.Name;
			string attemptedPassword = basicIdentity.Password;
			logger.LogDebug( "Attempted username: '{0}', Attempted password: '{1}'", attemptedUsername, attemptedPassword );

			if ( credentials.TryGetValue( attemptedUsername, out string? actualPasswordHash ) == false || string.IsNullOrWhiteSpace( actualPasswordHash ) == true ) {
				logger.LogWarning( "Received HTTP request with invalid authentication" );
				response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
				response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
				response.Close();
				return;
			}
			logger.LogDebug( "Actual password hash: '{0}'", actualPasswordHash );

			Match actualPasswordMatch = Regex.Match( actualPasswordHash, @"PBKDF2-(\d+)-(.+)-(.+)" );
			if ( actualPasswordMatch.Success == false ) {
				logger.LogWarning( "Hashed password does not match regular expression" );
				response.StatusCode = ( int ) HttpStatusCode.InternalServerError;
				response.Close();
				return;
			}

			if ( int.TryParse( actualPasswordMatch.Groups[ 1 ].Value, out int iterationCount ) == false ) throw new Exception( $"Failed to parse iteration count '{ actualPasswordMatch.Groups[ 1 ].Value }' as an integer" );
			string actualSaltHex = actualPasswordMatch.Groups[ 2 ].Value;
			string actualHashHex = actualPasswordMatch.Groups[ 3 ].Value;
			logger.LogDebug( "Actual iteration count: '{0}', Actual salt hex: '{1}', Actual hash: '{2}'", iterationCount, actualSaltHex, actualHashHex );

			string attemptedPasswordHash = PBKDF2( attemptedPassword, iterationCount, actualSaltHex );
			logger.LogDebug( "Attempted password hash: '{0}'", attemptedPasswordHash );

			if ( attemptedPasswordHash != actualPasswordHash ) {
				logger.LogWarning( "Received HTTP request with bad authentication" );
				response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
				response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
				response.Close();
				return;
			}

			byte[] responseBytes = Encoding.UTF8.GetBytes( "Hello World!" );
			response.ContentLength64 = responseBytes.Length;

			Stream output = response.OutputStream;
			output.Write( responseBytes, 0, responseBytes.Length );
			output.Close();
			logger.LogDebug( "Sent HTTP response: {0}", response.StatusCode );
		}

		private static string PBKDF2( string text, int iterationCount = 1000, string? existingSaltHex = null ) {
			byte[] saltBytes = new byte[ 16 ];

			if ( string.IsNullOrWhiteSpace( existingSaltHex ) ) {
				randomNumberGenerator.GetBytes( saltBytes );
			} else {
				saltBytes = Enumerable.Range( 0, saltBytes.Length )
					.Where( x => x % 2 == 0 )
					.Select( hex => Convert.ToByte( existingSaltHex.Substring( hex, 2 ), 16 ) )
					.ToArray();
			}

			Rfc2898DeriveBytes pbkdf2 = new( text, saltBytes, iterationCount, HashAlgorithmName.SHA256 );
			byte[] hashBytes = pbkdf2.GetBytes( 256 / 8 );

			string hashHex = BitConverter.ToString( hashBytes ).Replace( "-", "" ).ToLower();
			string saltHex = BitConverter.ToString( saltBytes ).Replace( "-", "" ).ToLower();

			return $"PBKDF2-{ iterationCount }-{ saltHex }-{ hashHex }";
		}

	}

	public struct State {
		public HttpListener Listener;
		public Dictionary<string, string> Credentials;
	}

}
