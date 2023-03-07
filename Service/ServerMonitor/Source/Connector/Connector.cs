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

			// https://stackoverflow.com/a/56207032
			// https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-8.0
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

			// Get the variables within state that was passed to us
			if ( asyncResult.AsyncState == null ) throw new Exception( $"Invalid state '{ asyncResult.AsyncState }' passed to async callback" );
			State state = ( State ) asyncResult.AsyncState;
			HttpListener httpListener = state.Listener;
			Dictionary<string, string> credentials = state.Credentials;

			// Get the request and response
			HttpListenerContext context = httpListener.EndGetContext( asyncResult );
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			logger.LogDebug( "Received HTTP request: {0} {1}", request.HttpMethod, request.Url );

			// Ensure basic authentication credentials were provided - https://stackoverflow.com/q/570605
			HttpListenerBasicIdentity? basicIdentity = ( HttpListenerBasicIdentity? ) context.User?.Identity;
			if ( basicIdentity == null ) {
				logger.LogWarning( "Received HTTP request without authentication" );
				response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
				response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
				response.OutputStream.Write( Encoding.UTF8.GetBytes( "No authentication provided" ) );
				response.Close();
				return;
			}

			// Get the username & password they sent
			string attemptedUsername = basicIdentity.Name;
			string attemptedPassword = basicIdentity.Password;
			logger.LogDebug( "Attempted username: '{0}', Attempted password: '{1}'", attemptedUsername, attemptedPassword );

			// Try to get the actual hashed password associated with the username they sent
			if ( credentials.TryGetValue( attemptedUsername, out string? actualPasswordHash ) == false || string.IsNullOrWhiteSpace( actualPasswordHash ) == true ) {
				logger.LogWarning( "Received HTTP request with unknown username for authentication" );
				response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
				response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
				response.OutputStream.Write( Encoding.UTF8.GetBytes( "Unknown username" ) );
				response.Close();
				return;
			}
			logger.LogDebug( "Actual password hash: '{0}' for user: '{1}'", actualPasswordHash, attemptedUsername );

			// Match the actual hashed password against a regular expression
			Match actualPasswordMatch = Regex.Match( actualPasswordHash, @"PBKDF2-(\d+)-(.+)-(.+)" );
			if ( actualPasswordMatch.Success == false ) {
				logger.LogWarning( "Actual hashed password does not match regular expression" );
				response.StatusCode = ( int ) HttpStatusCode.InternalServerError;
				response.OutputStream.Write( Encoding.UTF8.GetBytes( "Could not break apart hashed password" ) );
				response.Close();
				return;
			}

			// Parse & extract the components from the regular expression match
			if ( int.TryParse( actualPasswordMatch.Groups[ 1 ].Value, out int iterationCount ) == false ) throw new Exception( $"Failed to parse iteration count '{ actualPasswordMatch.Groups[ 1 ].Value }' as an integer" );
			string actualSaltHex = actualPasswordMatch.Groups[ 2 ].Value;
			string actualHashHex = actualPasswordMatch.Groups[ 3 ].Value;
			logger.LogDebug( "Actual iteration count: '{0}', Actual salt (hex): '{1}', Actual hash (hex): '{2}'", iterationCount, actualSaltHex, actualHashHex );

			// Hash the attempted password using the same iteration count & salt
			string attemptedPasswordHash = PBKDF2( attemptedPassword, iterationCount, actualSaltHex );
			logger.LogDebug( "Attempted password hash: '{0}'", attemptedPasswordHash );

			// Compare the attempted password hash against the actual password hash
			if ( attemptedPasswordHash != actualPasswordHash ) {
				logger.LogWarning( "Received HTTP request with incorrect authentication" );
				response.StatusCode = ( int ) HttpStatusCode.Unauthorized;
				response.AddHeader( "WWW-Authenticate", "Basic realm=\"Server Monitor\"" );
				response.OutputStream.Write( Encoding.UTF8.GetBytes( "Incorrect authentication" ) );
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
				logger.LogDebug( "Generating fresh salt" );
			} else {
				logger.LogDebug( "Using provided salt" );
				saltBytes = Convert.FromHexString( existingSaltHex );
			}

			// https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes?view=net-7.0
			Rfc2898DeriveBytes pbkdf2 = new( text, saltBytes, iterationCount, HashAlgorithmName.SHA512 );
			byte[] hashBytes = pbkdf2.GetBytes( 512 / 8 );

			// https://stackoverflow.com/a/311179
			string hashHex = Convert.ToHexString( hashBytes ).ToLower();
			string saltHex = Convert.ToHexString( saltBytes ).ToLower();

			return $"PBKDF2-{ iterationCount }-{ saltHex }-{ hashHex }";
		}

	}

	public struct State {
		public HttpListener Listener;
		public Dictionary<string, string> Credentials;
	}

}
