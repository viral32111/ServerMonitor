using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Xunit;

//[ assembly: CollectionBehavior( DisableTestParallelization = true ) ]
namespace ServerMonitor.Tests.Connector {

	public class Routes {

		private static readonly HttpClient httpClient = new();

		[ Theory ]
		[ InlineData( "GET", "/hello" ) ]
		[ InlineData( "GET", "/server" ) ]
		[ InlineData( "POST", "/server" ) ]
		[ InlineData( "GET", "/servers" ) ]
		[ InlineData( "POST", "/service" ) ]
		public void TestNoAuthentication( string method, string path ) {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector connector = new();

			HttpRequestMessage httpRequest = new() {
				Method = new( method ),
				RequestUri = new( $"http://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }{path}" ),
				Headers = {
					{ "Host", $"{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }" },
					{ "Accept", "application/json" },
					{ "Connection", "close" }
				}
			};

			connector.OnListeningStarted += async ( object? _, EventArgs _ ) => {
				using ( HttpResponseMessage httpResponse = await httpClient.SendAsync( httpRequest ) ) {
					string content = await httpResponse.Content.ReadAsStringAsync();

					Assert.True( httpResponse.StatusCode == HttpStatusCode.Unauthorized, "API response status code is not 401 Unauthorized" );
					Assert.True( httpResponse.Headers.WwwAuthenticate.Count == 1, "API response does not include WWW-Authenticate header" );
					Assert.True( content.Length == 0, "API response contains content" );
				}

				connector.StopServerCompletionSource.SetResult();
			};

			connector.HandleCommand( ServerMonitor.Configuration.Config, true );
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
