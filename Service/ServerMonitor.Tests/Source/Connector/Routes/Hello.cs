using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ServerMonitor.Tests.Connector.Routes {

	[ Collection( "WebServer" ) ]
	public class Hello {

		[ Fact ]
		public void TestGet() {
			ServerMonitor.Configuration.Load( Path.Combine( Directory.GetCurrentDirectory(), "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );

			ServerMonitor.Connector.Connector connector = new();

			string testUsername = ServerMonitor.Configuration.Config.ConnectorCredentials[ 0 ].Username;
			string testPassword = ServerMonitor.Configuration.Config.ConnectorCredentials[ 0 ].Password;
			string encodedCredentials = System.Convert.ToBase64String( System.Text.Encoding.UTF8.GetBytes( $"{ testUsername }:{ testPassword }" ) );

			HttpRequestMessage httpRequest = new() {
				Method = HttpMethod.Get,
				RequestUri = new( $"{ ( ServerMonitor.Configuration.Config.ConnectorListenPort == 443 ? "https" : "http" ) }://{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }/hello" ),
				Headers = {
					{ "Host", $"{ ServerMonitor.Configuration.Config.ConnectorListenAddress }:{ ServerMonitor.Configuration.Config.ConnectorListenPort }" },
					{ "Authorization", $"Basic { encodedCredentials }" }
				}
			};

			connector.OnListeningStarted += async ( object? _, EventArgs _ ) => {
				using ( HttpResponseMessage httpResponse = await Program.HttpClient.SendAsync( httpRequest ) ) {
					string content = await httpResponse.Content.ReadAsStringAsync();

					Assert.True( httpResponse.StatusCode == HttpStatusCode.OK, "API response status code is incorrect" );
					Assert.True( httpResponse.Headers.WwwAuthenticate.Count == 0, "API response includes WWW-Authenticate header" );
					Assert.True( content.Length > 0, "API response does not contain content" );

					JsonObject? jsonBody = JsonSerializer.Deserialize<JsonObject>( content );
					Assert.NotNull( jsonBody );

					Assert.True( jsonBody.ContainsKey( "errorCode" ), "API response JSON content does not contain the error code property" );
					Assert.True( ( ServerMonitor.Connector.ErrorCode ) jsonBody[ "errorCode" ]!.GetValue<int>() == ServerMonitor.Connector.ErrorCode.Success, "API response JSON content error code property is incorrect" );

					Assert.True( jsonBody.ContainsKey( "data" ), "API response JSON content does not contain the data property" );
				}

				connector.StopServerCompletionSource.SetResult();
			};

			connector.HandleCommand( ServerMonitor.Configuration.Config, true, false );
		}

	}

}
