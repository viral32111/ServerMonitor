using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ServerMonitor.Connector;

namespace ServerMonitor.Tests.Connector {

	public class Routes {

		[ Theory ]
		[ InlineData( "Get", "/hello" ) ]
		public async Task TestNoAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			Task serverTask = Task.Run( () => {
				ServerMonitor.Connector.Connector.HandleCommand( ServerMonitor.Configuration.Config, true );
			} );

			Console.WriteLine( "Waiting..." );
			Thread.Sleep( 5000 );
			Console.WriteLine( "Waited" );

			string url = $"http://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}";
			Console.WriteLine( "Url: {0}", url );

			using ( HttpClient httpClient = new() ) {
				Console.WriteLine( "Creating request..." );
				HttpRequestMessage request = new() {
					Method = new( method ),
					RequestUri = new( url )
				};
				request.Headers.ConnectionClose = true;

				Console.WriteLine( "Sending request..." );
				using ( HttpResponseMessage response = await httpClient.GetAsync( url ) ) {
					Console.WriteLine( "Response received" );
					Console.WriteLine( "Status Code: {0}", ( int ) response.StatusCode );
					Console.WriteLine( "Content: {0}", ( await response.Content.ReadAsStringAsync() ) );

					Assert.True( response.StatusCode == HttpStatusCode.Unauthorized, "API response status code is not 401 Unauthorized" );
					Assert.True( response.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
				}
			}

			await serverTask;
		}

		/*
		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		public async Task TestBadUsernameAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector.HandleCommand( ServerMonitor.Configuration.Config, true );

			string username = "UnknownUsername";
			string password = "IncorrectPassword";

			HttpRequestMessage request = new() {
				Method = new( method ),
				RequestUri = new( $"http://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}/" )
			};
			request.Headers.Authorization = new( "Basic", System.Convert.ToBase64String( System.Text.Encoding.UTF8.GetBytes( $"{ username }:${ password }" ) ) );

			HttpClient httpClient = new();
			HttpResponseMessage response = await httpClient.SendAsync( request );
			string responseContent = await response.Content.ReadAsStringAsync();

			Assert.True( response.StatusCode == HttpStatusCode.Unauthorized, "API response status code is not 401 Unauthorized" );
			Assert.True( response.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
			Assert.True( responseContent.Length > 0, "API response does not contain any content" );

			JsonObject? jsonBody = JsonSerializer.Deserialize<JsonObject>( responseContent );
			Assert.NotNull( jsonBody );

			Assert.True( jsonBody.ContainsKey( "errorCode" ), "API response JSON content does not contain the error code property" );
			Assert.True( ( ErrorCode ) jsonBody[ "errorCode" ]!.GetValue<int>() == ErrorCode.UnknownUser, "API response JSON content error code property is incorrect" );

			Assert.True( jsonBody.ContainsKey( "data" ), "API response JSON content does not contain the data property" );
			Assert.Null( jsonBody[ "data" ]!.GetValue<JsonNode?>() );
		}

		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		public async Task TestBadPasswordAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector.HandleCommand( ServerMonitor.Configuration.Config, true );

			string username = ServerMonitor.Configuration.Config.ConnectorCredentials[ 0 ].Username;
			string password = "IncorrectPassword";

			HttpRequestMessage request = new() {
				Method = new( method ),
				RequestUri = new( $"http://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}/" )
			};
			request.Headers.Authorization = new( "Basic", System.Convert.ToBase64String( System.Text.Encoding.UTF8.GetBytes( $"{ username }:${ password }" ) ) );

			HttpClient httpClient = new();
			HttpResponseMessage response = await httpClient.SendAsync( request );
			string responseContent = await response.Content.ReadAsStringAsync();

			Assert.True( response.StatusCode == HttpStatusCode.Unauthorized, "API response status code is not 401 Unauthorized" );
			Assert.True( response.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
			Assert.True( responseContent.Length > 0, "API response does not contain any content" );

			JsonObject? jsonBody = JsonSerializer.Deserialize<JsonObject>( responseContent );
			Assert.NotNull( jsonBody );

			Assert.True( jsonBody.ContainsKey( "errorCode" ), "API response JSON content does not contain the error code property" );
			Assert.True( ( ErrorCode ) jsonBody[ "errorCode" ]!.GetValue<int>() == ErrorCode.IncorrectPassword, "API response JSON content error code property is incorrect" );

			Assert.True( jsonBody.ContainsKey( "data" ), "API response JSON content does not contain the data property" );
			Assert.Null( jsonBody[ "data" ]!.GetValue<JsonNode?>() );
		}

		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		public async Task TestGoodAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector.HandleCommand( ServerMonitor.Configuration.Config, true );

			string username = ServerMonitor.Configuration.Config.ConnectorCredentials[ 0 ].Username;
			string password = ServerMonitor.Configuration.Config.ConnectorCredentials[ 1 ].Password;

			HttpRequestMessage request = new() {
				Method = new( method ),
				RequestUri = new( $"http://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}/" )
			};
			request.Headers.Authorization = new( "Basic", System.Convert.ToBase64String( System.Text.Encoding.UTF8.GetBytes( $"{ username }:${ password }" ) ) );

			HttpClient httpClient = new();
			HttpResponseMessage response = await httpClient.SendAsync( request );
			string responseContent = await response.Content.ReadAsStringAsync();

			Assert.True( response.StatusCode == HttpStatusCode.OK, "API response status code is not 200 OK" );
			Assert.True( response.Headers.WwwAuthenticate.Count == 0, "API response includes WWW-Authenticate header" );
			Assert.True( responseContent.Length > 0, "API response does not contain any content" );

			JsonObject? jsonBody = JsonSerializer.Deserialize<JsonObject>( responseContent );
			Assert.NotNull( jsonBody );

			Assert.True( jsonBody.ContainsKey( "errorCode" ), "API response JSON content does not contain the error code property" );
			Assert.True( ( ErrorCode ) jsonBody[ "errorCode" ]!.GetValue<int>() == ErrorCode.Success, "API response JSON content error code property is incorrect" );

			Assert.True( jsonBody.ContainsKey( "data" ), "API response JSON content does not contain the data property" );
			Assert.NotNull( jsonBody[ "data" ]!.GetValue<JsonNode?>() );
		}
		*/

	}

}
