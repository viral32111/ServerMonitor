using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Route;
using ServerMonitor.Connector.Helper;

namespace ServerMonitor.Connector {

	// Type alias for a route request handler, as its quite long - https://stackoverflow.com/a/161484
	using RouteRequestHandler = Func<HttpListenerRequest, HttpListenerResponse, HttpListener, HttpListenerContext, HttpListenerResponse>;

	public static class Connector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		// Dictionary of authentication credentials (usernames & hashed passwords)
		private static readonly Dictionary<string, string> authenticationCredentials = new();

		// Regular expression for the format to store hashed password data
		private static readonly Regex hashedPasswordRegex = new( @"PBKDF2-(\d+)-(.+)-(.+)" );

		// Will hold all the registered request handlers, by method & path
		private static readonly Dictionary<string, Dictionary<string, RouteRequestHandler>> routes = new();

		// List of request handlers for API routes
		private static readonly RouteRequestHandler[] routeRequestHandlers = new[] {
			Hello.OnRequest, // GET /hello
			Server.OnRequest, // GET /server?id=
			Servers.OnRequest, // GET /servers
		};

		// The main entry-point for this mode
		public static void HandleCommand( Config configuration, bool runOnce ) {
			logger.LogInformation( "Launched in connection point mode" );

			// Setup a HTTP listener - https://stackoverflow.com/a/56207032, https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-8.0
			HttpListener httpListener = new HttpListener();
			httpListener.AuthenticationSchemes = AuthenticationSchemes.Basic;
			httpListener.Realm = configuration.ConnectorAuthenticationRealm;
			string baseUrl = $"http://{ configuration.ConnectorListenAddress }:{ configuration.ConnectorListenPort }";

			// Loop through the route request handlers...
			foreach ( RouteRequestHandler routeRequestHandler in routeRequestHandlers ) {

				// Get the route attribute for this request handler
				RouteAttribute? routeAttribute = ( RouteAttribute? ) routeRequestHandler.Method.GetCustomAttributes( typeof( RouteAttribute ), false ).FirstOrDefault();
				if ( routeAttribute == null ) throw new Exception( $"Request handler '{ routeRequestHandler.Method.Name }' does not have a route attribute" );

				// Add the route to the registered request handlers
				if ( routes.ContainsKey( routeAttribute.Method ) == false ) routes.Add( routeAttribute.Method, new Dictionary<string, RouteRequestHandler>() );
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
					string hashedPassword = Hash.PBKDF2( credential.Password );
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
			while ( httpListener.IsListening == true && runOnce == false )
				httpListener.BeginGetContext(
					asyncResult => OnHttpRequest( asyncResult ),
					new State {
						HttpListener = httpListener,
						Config = configuration
					}
				).AsyncWaitHandle.WaitOne();

			// Stop the HTTP listener
			httpListener.Stop();
			logger.LogInformation( "Stopped listening for API requests" );

		}

		// Processes an incoming HTTP request
		private static HttpListenerResponse OnHttpRequest( IAsyncResult asyncResult ) {

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
			string requestAddress = request.RemoteEndPoint.Address.ToString();
			logger.LogDebug( "Incoming HTTP request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );

			// Ensure basic authentication was used - https://stackoverflow.com/q/570605
			HttpListenerBasicIdentity? basicAuthentication = ( HttpListenerBasicIdentity? ) context.User?.Identity;
			if ( basicAuthentication == null ) {
				logger.LogWarning( "No authentication for API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				response.AddHeader( "WWW-Authenticate", $"Basic realm=\"{ configuration.ConnectorAuthenticationRealm }\"" );
				return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.NoAuthentication );
			}

			// Store the username & password attempt
			string usernameAttempt = basicAuthentication.Name;
			string passwordAttempt = basicAuthentication.Password;
			logger.LogInformation( "Received API request '{0}' '{1}' from '{2}' ({3})", requestMethod, requestPath, requestAddress, usernameAttempt );

			// Try get the hashed password associated for this user
			if ( authenticationCredentials.TryGetValue( usernameAttempt, out string? hashedPassword ) == false || string.IsNullOrWhiteSpace( hashedPassword ) == true ) {
				logger.LogWarning( "Unrecognised user '{1}' for API request '{0}' '{1}' from '{2}'", usernameAttempt, requestMethod, requestPath, requestAddress );
				response.AddHeader( "WWW-Authenticate", $"Basic realm=\"{ configuration.ConnectorAuthenticationRealm }\"" );
				return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.UnknownUser );
			}

			// Parse the components from the hashed password
			Match hashedPasswordMatch = hashedPasswordRegex.Match( hashedPassword );
			if ( hashedPasswordMatch.Success == false ) throw new Exception( $"Failed to match hashed password '{ hashedPassword }'" );
			if ( int.TryParse( hashedPasswordMatch.Groups[ 1 ].Value, out int iterationCount ) == false ) throw new Exception( $"Failed to parse iteration count '{ hashedPasswordMatch.Groups[ 1 ].Value }' as an integer" );
			byte[] hashedPasswordSalt = Convert.FromHexString( hashedPasswordMatch.Groups[ 2 ].Value );

			// Check if the hashed password attempt matches the hashed password
			if ( Hash.PBKDF2( passwordAttempt, iterationCount, hashedPasswordSalt ) != hashedPassword ) {
				logger.LogWarning( "Incorrect password for user '{1}' for API request '{0}' '{1}' from '{2}'", usernameAttempt, requestMethod, requestPath, requestAddress );
				response.AddHeader( "WWW-Authenticate", $"Basic realm=\"{ configuration.ConnectorAuthenticationRealm }\"" );
				return Response.SendJson( response, statusCode: HttpStatusCode.Unauthorized, errorCode: ErrorCode.IncorrectPassword );
			}

			// Check if the request is for a valid route (by method)
			if ( routes.TryGetValue( request.HttpMethod, out Dictionary<string, RouteRequestHandler>? routesForMethod ) == false || routesForMethod == null ) {
				logger.LogWarning( "No registered routes for method '{0}' for API request '{1}' '{2}' from '{3}' ({4})", requestMethod, requestMethod, requestPath, requestAddress, usernameAttempt );
				return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownRoute );
			}

			// Check if the request is for a valid route (by path)
			if ( routesForMethod.TryGetValue( requestPath, out RouteRequestHandler? routeHandler ) == false || routeHandler == null ) {
				logger.LogWarning( "No registered route for path '{0}' for API request '{1}' '{2}' from '{3}' ({4})", requestPath, requestMethod, requestPath, requestAddress, usernameAttempt );
				return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.UnknownRoute );
			}

			// Safely run the route request handler
			try {
				return routeHandler( request, response, httpListener, context );
			} catch ( Exception exception ) {
				logger.LogError( exception, "Failed to handle API request '{0}' '{1}' from '{2}'", requestMethod, requestPath, requestAddress );
				return Response.SendJson( response, statusCode: HttpStatusCode.InternalServerError, errorCode: ErrorCode.UncaughtServerError );
			} finally {
				logger.LogDebug( "Completed API request '{0}' '{1}' from '{2}' ({3})", requestMethod, requestPath, requestAddress, usernameAttempt );
				if ( response != null ) response.Close();
			}

		}

	}

	// Variables we want to pass to the incoming request callback
	public struct State {
		public HttpListener HttpListener;
		public Config Config;
	}

}
