using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Routes;

namespace ServerMonitor.Connector {

	public static class Connector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		// Secure random number generator for generating hash salts
		private static readonly RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();

		// Dictionary of authentication credentials (usernames & hashed passwords)
		private static readonly Dictionary<string, string> authenticationCredentials = new();

		// Regular expression for the format to store hashed password data
		private static readonly Regex hashedPasswordRegex = new( @"PBKDF2-(\d+)-(.+)-(.+)" );

		// Will hold all the registered request handlers, by method & path
		private static readonly Dictionary<string, Dictionary<string, Action<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext>>> routes = new();

		// List of request handlers for API routes
		private static readonly Action<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext>[] routeRequestHandlers = new[] {
			Hello.OnRequest // GET /hello
		};

		// The main entry-point for this mode
		public static void HandleCommand( Config configuration, bool runOnce ) {
			logger.LogInformation( "Launched in connection point mode" );

			// Setup a HTTP listener - https://stackoverflow.com/a/56207032, https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-8.0
			HttpListener httpListener = new HttpListener();
			httpListener.AuthenticationSchemes = AuthenticationSchemes.Basic;
			string baseUrl = $"http://{ configuration.ConnectorListenAddress }:{ configuration.ConnectorListenPort }";

			// Loop through the route request handlers...
			foreach ( Action<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext> routeRequestHandler in routeRequestHandlers ) {

				// Get the route attribute for this request handler
				RouteAttribute? routeAttribute = ( RouteAttribute? ) routeRequestHandler.Method.GetCustomAttributes( typeof( RouteAttribute ), false ).FirstOrDefault();
				if ( routeAttribute == null ) throw new Exception( $"Request handler '{ routeRequestHandler.Method.Name }' does not have a route attribute" );

				// Add the route to the registered request handlers
				if ( routes.ContainsKey( routeAttribute.Method ) == false ) routes.Add( routeAttribute.Method, new Dictionary<string, Action<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext>>() );
				if ( routes[ routeAttribute.Method ].TryAdd( routeAttribute.Path, routeRequestHandler ) == false ) throw new Exception( $"Duplicate route '{ routeAttribute.Method } { routeAttribute.Path }' found for request handler '{ routeRequestHandler.Method.Name }'" );

				// Add the route path to the HTTP listener
				httpListener.Prefixes.Add( string.Concat( baseUrl, routeAttribute.Path, "/" ) );
	
				logger.LogInformation( "Registered API route '{0}' '{1}' ({2})", routeAttribute.Method, routeAttribute.Path, routeRequestHandler.Method.Name );

			}

			// Loop through the configured credentials...
			foreach ( Credential credential in configuration.ConnectorCredentials ) {
				
				// Skip if this user has already been added
				if ( authenticationCredentials.ContainsKey( credential.Username ) == true ) {
					logger.LogWarning( "Duplicate user '{0}' found in credentials! Skipping...", credential.Username );
					continue;
				}

				// Hash the password if it isn't already hashed
				Match passwordMatch = hashedPasswordRegex.Match( credential.Password );
				if ( passwordMatch.Success == false ) {
					string hashedPassword = PBKDF2( credential.Password );
					logger.LogWarning( "Password for user '{0}' is NOT hashed yet! Change password to: '{1}'", credential.Username, hashedPassword );
					authenticationCredentials.Add( credential.Username, hashedPassword );
				} else {
					authenticationCredentials.Add( credential.Username, credential.Password );
				}

				logger.LogInformation( "Added user '{0}' to credentials list", credential.Username );

			}

			// Start the HTTP listener
			httpListener.Start();
			logger.LogInformation( "Listening for API requests on '{0}'", baseUrl );

			// Forever process for incoming HTTP requests...
			while ( httpListener.IsListening == true && runOnce == false ) httpListener.BeginGetContext( new AsyncCallback( OnHttpRequest ), new State {
				HttpListener = httpListener,
				Config = configuration
			} ).AsyncWaitHandle.WaitOne();

			// Stop the HTTP listener
			httpListener.Stop();
			logger.LogInformation( "Stopped listening for API requests" );

		}

		// Processes an incoming HTTP request
		private static void OnHttpRequest( IAsyncResult asyncResult ) {

			// Get the variables within state that was passed to us
			if ( asyncResult.AsyncState == null ) throw new Exception( $"Invalid state '{ asyncResult.AsyncState }' passed to incoming request callback" );
			State state = ( State ) asyncResult.AsyncState;
			HttpListener httpListener = state.HttpListener;
			Config configuration = state.Config;

			// Get the request & response
			HttpListenerContext context = httpListener.EndGetContext( asyncResult );
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			string requestMethod = request.HttpMethod;
			string requestPath = request.Url?.AbsolutePath ?? "/";
			logger.LogDebug( "Received HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );

			// Ensure basic authentication was used - https://stackoverflow.com/q/570605
			HttpListenerBasicIdentity? basicIdentity = ( HttpListenerBasicIdentity? ) context.User?.Identity;
			if ( basicIdentity == null ) {
				logger.LogWarning( "No authentication for API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				response.AddHeader( "WWW-Authenticate", $"Basic realm=\"{ configuration.ConnectorAuthenticationRealm }\"" );
				QuickResponse( response, HttpStatusCode.Unauthorized, "No authentication attempt" );
				return;
			}

			// Store the username & password attempt
			string usernameAttempt = basicIdentity.Name;
			string passwordAttempt = basicIdentity.Password;

			// Try get the hashed password associated for this user
			if ( authenticationCredentials.TryGetValue( usernameAttempt, out string? hashedPassword ) == false || string.IsNullOrWhiteSpace( hashedPassword ) == true ) {
				logger.LogWarning( "Unrecognised user '{1}' for API request '{0}' '{1}' from '{2}'", usernameAttempt, requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				response.AddHeader( "WWW-Authenticate", $"Basic realm=\"{ configuration.ConnectorAuthenticationRealm }\"" );
				QuickResponse( response, HttpStatusCode.Unauthorized, "Unrecognised user" );
				return;
			}

			// Parse the components from the hashed password
			Match hashedPasswordMatch = hashedPasswordRegex.Match( hashedPassword );
			if ( hashedPasswordMatch.Success == false ) throw new Exception( $"Failed to match hashed password '{ hashedPassword }'" );
			if ( int.TryParse( hashedPasswordMatch.Groups[ 1 ].Value, out int iterationCount ) == false ) throw new Exception( $"Failed to parse iteration count '{ hashedPasswordMatch.Groups[ 1 ].Value }' as an integer" );
			byte[] hashedPasswordSalt = Convert.FromHexString( hashedPasswordMatch.Groups[ 2 ].Value );

			// Check if the hashed password attempt matches the hashed password
			if ( PBKDF2( passwordAttempt, iterationCount, hashedPasswordSalt ) != hashedPassword ) {
				logger.LogWarning( "Incorrect password for user '{1}' for API request '{0}' '{1}' from '{2}'", usernameAttempt, requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				response.AddHeader( "WWW-Authenticate", $"Basic realm=\"{ configuration.ConnectorAuthenticationRealm }\"" );
				QuickResponse( response, HttpStatusCode.Unauthorized, "Incorrect password" );
				return;
			}

			// Check if the request is for a valid route (by method)
			if ( routes.TryGetValue( request.HttpMethod, out Dictionary<string, Action<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext>>? routesForMethod ) == false || routesForMethod == null ) {
				logger.LogWarning( "No registered route (by method) for API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				QuickResponse( response, HttpStatusCode.NotFound, "Route does not exist" );
				return;
			}

			// Check if the request is for a valid route (by path)
			if ( routesForMethod.TryGetValue( requestPath, out Action<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext>? routeHandler ) == false || routeHandler == null ) {
				logger.LogWarning( "No registered route (by path) for API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				QuickResponse( response, HttpStatusCode.NotFound, "Route does not exist" );
				return;
			}

			// Safely run the route request handler
			try {
				routeHandler( request, response, httpListener, context );
			} catch ( Exception exception ) {
				logger.LogError( exception, "Failed to handle API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				QuickResponse( response, HttpStatusCode.InternalServerError, "Failure handling request" );
			} finally {
				logger.LogDebug( "Completed API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, request.RemoteEndPoint.Address.ToString() );
				response.Close();
			}

		}

		// Helper to send a HTTP response
		private static void QuickResponse( HttpListenerResponse response, HttpStatusCode statusCode, string body ) {
			response.StatusCode = ( int ) statusCode;
			response.OutputStream.Write( Encoding.UTF8.GetBytes( body ) );
			response.Close();
		}

		// Hashes text using the PBKDF2 algorithm
		private static string PBKDF2( string text, int iterationCount = 1000, byte[]? saltBytes = null ) {

			// Generate fresh salt if none was provided
			if ( saltBytes == null ) {
				saltBytes = new byte[ 16 ];
				randomNumberGenerator.GetBytes( saltBytes );
			}

			// Securely hash the text - https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes?view=net-7.0
			using ( Rfc2898DeriveBytes pbkdf2 = new( text, saltBytes, iterationCount, HashAlgorithmName.SHA512 ) ) {
				byte[] hashBytes = pbkdf2.GetBytes( 512 / 8 );

				// Return the hash in the custom format, with the bytes converted to hexadecimal - https://stackoverflow.com/a/311179
				return $"PBKDF2-{ iterationCount }-{ Convert.ToHexString( saltBytes ).ToLower() }-{ Convert.ToHexString( hashBytes ).ToLower() }";
			}

		}

	}

	// Variables we want to pass to the incoming request callback
	public struct State {
		public HttpListener HttpListener;
		public Config Config;
	}

}
