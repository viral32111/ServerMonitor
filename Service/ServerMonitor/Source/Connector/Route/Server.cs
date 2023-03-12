using System;
using System.Web;
using System.Net;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using ServerMonitor.Connector.Helper;
using viral32111.JsonExtensions;

namespace ServerMonitor.Connector.Route {

	public static class Server {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Routes/Server" );

		#pragma warning disable CS1998 // Async method lacks await operators and will run synchronously

		// Returns all metrics data for a specific server
		[ Route( "GET", "/server" ) ]
		public static async Task<HttpListenerResponse> OnGetRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {

			// Ensure query parameters were provided
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			// Try extract the server identifier from the query parameters
			string? serverIdentifier = HttpUtility.ParseQueryString( queryString ).Get( "id" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "id" }
			} );

			// Try decode the server identifier
			string jobName = string.Empty, instanceAddress = string.Empty;
			try {
				string[] serverIdentifierParts = Helper.Prometheus.DecodeIdentifier( serverIdentifier );
				jobName = serverIdentifierParts[ 0 ];
				instanceAddress = serverIdentifierParts[ 1 ];
			} catch ( FormatException ) {
				return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.InvalidParameter, data: new JsonObject() {
					{ "parameter", "id" }
				} );
			}

			// Try fetch the server
			JsonObject? server = ( await Helper.Prometheus.FetchServers( configuration ) ).FirstOrDefault( server => server.NestedGet<string>( "identifier" ) == serverIdentifier );
			if ( server == null ) return Response.SendJson( response, statusCode: HttpStatusCode.NotFound, errorCode: ErrorCode.ServerNotFound, data: new JsonObject() { { "id", serverIdentifier } } );

			// TODO: Fetch network metrics
			// TODO: Fetch services metrics
			// TODO: Fetch Docker containers metrics
			// TODO: Fetch SNMP metrics

			return Response.SendJson( response, data: new JsonObject() {
				{ "processor", await Helper.Prometheus.FetchProcessor( configuration, jobName, instanceAddress ) },
				{ "memory", await Helper.Prometheus.FetchMemory( configuration, jobName, instanceAddress ) },
				{ "drives", JSON.CreateJsonArray( await Helper.Prometheus.FetchDrives( configuration, instanceAddress, jobName ) ) }
			} );

			/*
			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "id", serverIdentifier },
				{ "name", "DEBIAN-SERVER-01" },
				{ "address", "127.0.0.1" },
				{ "lastUpdate", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
				{ "description", "Example server for testing purposes." },
				{ "uptimeSeconds", 60 * 60 * 24 * 7 },

				{ "supportedActions", new JsonObject() {
					{ "shutdown", false },
					{ "reboot", false }
				} },
				
				{ "metrics", new JsonObject() {
					{ "resources", new JsonObject() {
						{ "processor", new JsonObject() {
							{ "usage", 50.00 },
							{ "temperature", 35.00 },
							{ "frequency", 1000.00 }
						} },
				
						{ "memory", new JsonObject() {
							{ "totalBytes", 1024 },
							{ "freeBytes", 512 },
							{ "swap", new JsonObject() {
								{ "totalBytes", 1024 },
								{ "freeBytes", 512 }
							} }
						} },
						{ "drives", new JsonArray() {
							new JsonObject() {
								{ "name", "sda" },
								{ "health", 99 },
								{ "bytesWritten", 1024 },
								{ "bytesRead", 1024 },
								{ "partitions", new JsonArray() {
									new JsonObject() {
										{ "name", "sda1" },
										{ "mountpoint", "/boot" },
										{ "totalBytes", 1024 },
										{ "freeBytes", 256 }
									},
									new JsonObject() {
										{ "name", "sda2" },
										{ "mountpoint", "/" },
										{ "totalBytes", 4096 },
										{ "freeBytes", 512 }
									}
								} }
							},
							new JsonObject() {
								{ "name", "sdb" },
								{ "health", 100 },
								{ "bytesWritten", 512 },
								{ "bytesRead", 512 },
								{ "partitions", new JsonArray() {
									new JsonObject() {
										{ "name", "sdb1" },
										{ "mountpoint", "/mnt/shares" },
										{ "totalBytes", 8192 },
										{ "freeBytes", 4096 }
									}
								} }
							}
						} },
						{ "networkInterfaces", new JsonArray() {
							new JsonObject() {
								{ "name", "eth0" },
								{ "bytesSent", 1024 },
								{ "bytesReceived", 1024 }
							},
							new JsonObject() {
								{ "name", "wlan0" },
								{ "bytesSent", 512 },
								{ "bytesReceived", 256 }
							}
						} }
					} },
					{ "services", new JsonArray() {
						new JsonObject() {
							{ "service", "dhcpcd" },
							{ "name", "DHCP Client Daemon" },
							{ "description", "Requests IP addresses" },
							{ "statusCode", 1 },
							{ "exitCode", -1 },
							{ "uptimeSeconds", 60 * 60 * 24 },
							{ "supportedActions", new JsonObject() {
								{ "restart", true },
								{ "stop", true },
								{ "start", false }
							} },
							{ "logs", new JsonArray() }
						},
						new JsonObject() {
							{ "service", "dnsmasq" },
							{ "name", "DNSMasq" },
							{ "description", "Local DNS cache" },
							{ "statusCode", 0 },
							{ "exitCode", 1 },
							{ "uptimeSeconds", -1 },
							{ "supportedActions", new JsonObject() {
								{ "restart", false },
								{ "stop", false },
								{ "start", true }
							} },
							{ "logs", new JsonArray() }
						},
					} },
					{ "dockerContainers", new JsonArray() {
						new JsonObject() {
							{ "name", "web-server" },
							{ "id", "abcdefghijklmnopqrstuvwxyz" },
							{ "image", "viral32111/example-web-server:latest" },
							{ "statusCode", 1 },
							{ "exitCode", -1 },
							{ "createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
							{ "healthStatusCode", 1 },
							{ "supportedActions", new JsonObject() {
								{ "restart", true },
								{ "stop", true },
								{ "start", false },
								{ "remove", false }
							} },
							{ "logs", new JsonArray() }
						},
						new JsonObject() {
							{ "name", "prometheus" },
							{ "id", "123456789klmnopqrstuvwxyz" },
							{ "image", "prometheus:local" },
							{ "statusCode", 2 },
							{ "exitCode", 1 },
							{ "createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
							{ "healthStatusCode", 2 },
							{ "supportedActions", new JsonObject() {
								{ "restart", false },
								{ "stop", false },
								{ "start", true },
								{ "remove", true }
							} },
							{ "logs", new JsonArray() }
						}
					} },
					{ "snmp", new JsonObject() {
						{ "community", "public" },
						{ "agents", new JsonArray() {
							new JsonObject() {
								{ "ipAddress", "1.2.3.4" },
								{ "port", 167 },
								{ "name", "WINDOWS-DC-02" },
								{ "description", "Microsoft Windows Enterprise Server 2016" },
								{ "contact", "admin@example.com" },
								{ "location", "Office" },
								{ "receivedTraps", new JsonObject() {
									{ "count", 5 },
									{ "logs", new JsonArray() }
								} },
								{ "uptimeSeconds", 60 * 60 * 24 * 7 },
								{ "serviceCount", 50 }
							} }
						} }
					}
				} }
			} );
			*/

		}

		// TODO: Executing an action on a server
		[ Route( "POST", "/server" ) ]
		public static async Task<HttpListenerResponse> OnPostRequest( Config configuration, HttpListenerRequest request, HttpListenerResponse response, HttpListenerContext context ) {
			string? queryString = request.Url?.Query;
			if ( string.IsNullOrWhiteSpace( queryString ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.NoParameters );

			NameValueCollection queryParameters = HttpUtility.ParseQueryString( queryString );
			string? serverIdentifier = queryParameters.Get( "id" );
			string? actionName = queryParameters.Get( "action" );
			if ( string.IsNullOrWhiteSpace( serverIdentifier ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "id" }
			} );
			if ( string.IsNullOrWhiteSpace( actionName ) ) return Response.SendJson( response, statusCode: HttpStatusCode.BadRequest, errorCode: ErrorCode.MissingParameter, data: new JsonObject() {
				{ "parameter", "action" }
			} );

			return Response.SendJson( response, statusCode: HttpStatusCode.NotImplemented, errorCode: ErrorCode.ExampleData, data: new JsonObject() {
				{ "success", true },
				{ "response", "This is the output of the action." }
			} );
		}

		#pragma warning restore CS1998 // Async method lacks await operators and will run synchronously

	}

}
