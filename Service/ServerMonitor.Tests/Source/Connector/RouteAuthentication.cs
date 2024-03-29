using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ServerMonitor.Tests.Connector {

	public class RouteAuthentication {

		/*
		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		[ InlineData( "GET", "/server" ) ]
		[ InlineData( "POST", "/server" ) ]
		[ InlineData( "GET", "/servers" ) ]
		[ InlineData( "POST", "/service" ) ]
		public void TestWithoutAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector connector = new();

			HttpRequestMessage httpRequest = new() {
				Method = new( method ),
				RequestUri = new( $"{ ( ServerMonitor.Configuration.Config.ConnectorListenPort == 443 ? "https" : "http" ) }://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}" ),
				Headers = {
					{ "Host", $"{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }" }
				}
			};

			connector.OnListeningStarted += async ( object? _, EventArgs _ ) => {
				using ( HttpResponseMessage httpResponse = await httpClient.SendAsync( httpRequest ) ) {
					string content = await httpResponse.Content.ReadAsStringAsync();

					Assert.True( httpResponse.StatusCode == HttpStatusCode.Unauthorized, "API response status code is incorrect" );
					Assert.True( httpResponse.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
					Assert.True( content.Length == 0, "API response contains content" );
				}

				connector.StopServerCompletionSource.SetResult();
			};

			connector.HandleCommand( ServerMonitor.Configuration.Config, true, false );
		}
		*/

		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		[ InlineData( "GET", "/server" ) ]
		[ InlineData( "POST", "/server" ) ]
		[ InlineData( "GET", "/servers" ) ]
		[ InlineData( "POST", "/service" ) ]
		public void TestIncorrectUsernameAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector connector = new();

			string testUsername = "IncorrectUsername";
			string testPassword = "IncorrectPassword";
			string encodedCredentials = System.Convert.ToBase64String( System.Text.Encoding.UTF8.GetBytes( $"{ testUsername }:{ testPassword }" ) );

			HttpRequestMessage httpRequest = new() {
				Method = new( method ),
				RequestUri = new( $"{ ( ServerMonitor.Configuration.Config.ConnectorListenPort == 443 ? "https" : "http" ) }://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}" ),
				Headers = {
					{ "Host", $"{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }" },
					{ "Authorization", $"Basic { encodedCredentials }" }
				}
			};

			connector.OnListeningStarted += async ( object? _, EventArgs _ ) => {
				using ( HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest ) ) {
					string content = await httpResponse.Content.ReadAsStringAsync();

					Assert.True( httpResponse.StatusCode == HttpStatusCode.Unauthorized, "API response status code is incorrect" );
					Assert.True( httpResponse.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
					Assert.True( content.Length > 0, "API response does not contain content" );

					JsonObject? jsonBody = JsonSerializer.Deserialize<JsonObject>( content );
					Assert.NotNull( jsonBody );

					Assert.True( jsonBody.ContainsKey( "errorCode" ), "API response JSON content does not contain the error code property" );
					Assert.True( ( ServerMonitor.Connector.ErrorCode ) jsonBody[ "errorCode" ]!.GetValue<int>() == ServerMonitor.Connector.ErrorCode.UnknownUser, "API response JSON content error code property is incorrect" );

					Assert.True( jsonBody.ContainsKey( "data" ), "API response JSON content does not contain the data property" );
				}

				connector.StopServerCompletionSource.SetResult();
			};

			connector.HandleCommand( ServerMonitor.Configuration.Config, true, false );
		}

		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		[ InlineData( "GET", "/server" ) ]
		[ InlineData( "POST", "/server" ) ]
		[ InlineData( "GET", "/servers" ) ]
		[ InlineData( "POST", "/service" ) ]
		public void TestIncorrectPasswordAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector connector = new();

			string testUsername = ServerMonitor.Configuration.Config.ConnectorAuthenticationCredentials[ 0 ].Username;
			string testPassword = "IncorrectPassword";
			string encodedCredentials = System.Convert.ToBase64String( System.Text.Encoding.UTF8.GetBytes( $"{ testUsername }:{ testPassword }" ) );

			HttpRequestMessage httpRequest = new() {
				Method = new( method ),
				RequestUri = new( $"{ ( ServerMonitor.Configuration.Config.ConnectorListenPort == 443 ? "https" : "http" ) }://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}" ),
				Headers = {
					{ "Host", $"{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }" },
					{ "Authorization", $"Basic { encodedCredentials }" }
				}
			};

			connector.OnListeningStarted += async ( object? _, EventArgs _ ) => {
				using ( HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest ) ) {
					string content = await httpResponse.Content.ReadAsStringAsync();

					Assert.True( httpResponse.StatusCode == HttpStatusCode.Unauthorized, "API response status code is incorrect" );
					Assert.True( httpResponse.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
					Assert.True( content.Length > 0, "API response does not contain content" );

					JsonObject? jsonBody = JsonSerializer.Deserialize<JsonObject>( content );
					Assert.NotNull( jsonBody );

					Assert.True( jsonBody.ContainsKey( "errorCode" ), "API response JSON content does not contain the error code property" );
					Assert.True( ( ServerMonitor.Connector.ErrorCode ) jsonBody[ "errorCode" ]!.GetValue<int>() == ServerMonitor.Connector.ErrorCode.IncorrectPassword, "API response JSON content error code property is incorrect" );

					Assert.True( jsonBody.ContainsKey( "data" ), "API response JSON content does not contain the data property" );
				}

				connector.StopServerCompletionSource.SetResult();
			};

			connector.HandleCommand( ServerMonitor.Configuration.Config, true, false );
		}

	}

}
