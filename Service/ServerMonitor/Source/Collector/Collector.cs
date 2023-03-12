using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using Mono.Unix.Native; // https://github.com/mono/mono.posix
using Prometheus; // https://github.com/prometheus-net/prometheus-net
using ServerMonitor.Collector.Resource;

namespace ServerMonitor.Collector {

	public class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		// The metrics server
		public MetricServer? MetricsServer { get; private set; }

		// Event that fires when the Metrics Server starts listening
		public event EventHandler<EventArgs>? OnMetricsServerStarted;
		public delegate void OnMetricsServerStartedEventHandler( object sender, EventArgs e );

		// Entry-point for the "collector" sub-command...
		public void HandleCommand( Config configuration, bool singleRun ) {
			logger.LogInformation( "Launched in collector mode" );

			// Setup the global HTTP client
			Program.SetupHTTPClient();

			// Fail if we're not running as administrator/root, and a collector requires it
			if ( IsRunningAsAdmin() == false ) {
				if ( configuration.CollectSNMPMetrics == true && configuration.SNMPManagerListenPort < 1024 ) {
					logger.LogError( "This program must be run as administrator/root" );
					Environment.Exit( 1 );
				}
			}

			// Start the Prometheus metrics server
			MetricsServer = new(
				hostname: configuration.PrometheusListenAddress,
				port: configuration.PrometheusListenPort,
				url: configuration.PrometheusListenPath,
				useHttps: false
			);
			MetricsServer.Start();
			logger.LogInformation( "Prometheus Metrics server listening on http://{0}:{1}/{2}", configuration.PrometheusListenAddress, configuration.PrometheusListenPort, configuration.PrometheusListenPath );
			OnMetricsServerStarted?.Invoke( null, EventArgs.Empty );

			// Create the SNMP manager
			CancellationTokenSource cancellationTokenSource = new();
			SNMP snmp = new( configuration, cancellationTokenSource.Token );

			// Start the SNMP manager
			if ( configuration.CollectSNMPMetrics == true ) snmp.ListenForTraps();

			// Create instances of each resource collector
			Memory memory = new( configuration );
			Processor processor = new( configuration );
			Drive drive = new( configuration );
			Network network = new( configuration );

			// Create an instance of the service collector
			Services services = new( configuration );

			// Create an instance of the Docker collector
			Docker docker = new( configuration );

			// Create an instance of the information collector
			Information information = new( configuration );

			do {
				Console.WriteLine( new string( '-', 100 ) );

				if ( configuration.CollectMemoryMetrics == true ) {
					try {
						memory.Update();
						
						double totalMemory = Math.Round( memory.TotalBytes.Value / 1024 / 1024, 2 );
						double freeMemory = Math.Round( memory.FreeBytes.Value / 1024 / 1024, 2 );
						double usedMemory = Math.Round( ( memory.TotalBytes.Value - memory.FreeBytes.Value ) / 1024 / 1024, 2 );
						double usedMemoryPercentage = Math.Round( ( memory.TotalBytes.Value - memory.FreeBytes.Value ) / memory.TotalBytes.Value * 100, 0 );
						
						double totalSwap = Math.Round( memory.SwapTotalBytes.Value / 1024 / 1024, 2 );
						double freeSwap = Math.Round( memory.SwapFreeBytes.Value / 1024 / 1024, 2 );
						double usedSwap = Math.Round( ( memory.SwapTotalBytes.Value - memory.SwapFreeBytes.Value ) / 1024 / 1024, 2 );
						double usedSwapPercentage = Math.Round( ( memory.SwapTotalBytes.Value - memory.SwapFreeBytes.Value ) / memory.SwapTotalBytes.Value * 100, 0 );
					
						logger.LogInformation( "Memory: {0} MiB / {1} MiB ({2} MiB free, {3}% usage)", usedMemory, totalMemory, freeMemory, usedMemoryPercentage );
						logger.LogInformation( "Swap/Page: {0} MiB / {1} MiB ({2} MiB free, {3}% usage)", usedSwap, totalSwap, freeSwap, usedSwapPercentage );
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect memory metrics" );
					}
				}

				if ( configuration.CollectProcessorMetrics == true ) {
					try {
						processor.Update();
						
						double processorUsage = Math.Round( processor.Usage.Value, 1 );
						double processorFrequency = Math.Round( processor.Frequency.Value, 1 );
						double processorTemperature = Math.Round( processor.Temperature.Value, 1 );
						
						logger.LogInformation( "Processor: {0}% @ {1} MHz ({2} C)", processorUsage, processorFrequency, processorTemperature );
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect processor metrics" );
					}
				}

				if ( configuration.CollectDiskMetrics == true ) {
					try {
						drive.Update();
						
						foreach ( string[] labelValues in drive.ReadBytes.GetAllLabelValues() ) {
							string driveName = labelValues[ 0 ];

							double bytesRead = Math.Round( drive.ReadBytes.WithLabels( driveName ).Value / 1024 / 1024, 2 );
							double bytesWritten = Math.Round( drive.WriteBytes.WithLabels( driveName ).Value / 1024 / 1024, 2 );
							int healthPercentage = ( int ) drive.Health.WithLabels( driveName ).Value;
							
							logger.LogInformation( "Drive ({0}): {1} MiB read, {2} MiB written, {3}% health", driveName, bytesRead, bytesWritten, healthPercentage );
						}
						
						foreach ( string[] labelValues in drive.TotalBytes.GetAllLabelValues() ) {
							string partitionName = labelValues[ 0 ];
							string partitionMountPath = labelValues[ 1 ];

							double totalSpace = Math.Round( drive.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value / 1024 / 1024 / 1024, 2 );
							double freeSpace = Math.Round( drive.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value / 1024 / 1024 / 1024, 2 );
							double usedSpace = Math.Round( ( drive.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value - drive.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value ) / 1024 / 1024 / 1024, 2 );
							double usedSpacePercentage = Math.Round( ( drive.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value - drive.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value ) / drive.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value * 100, 0 );
							
							logger.LogInformation( "Partition ({0}, {1}): {2} GiB / {3} GiB ({4} GiB free, {5}% usage)", partitionName, partitionMountPath, usedSpace, totalSpace, freeSpace, usedSpacePercentage );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect drive metrics" );
					}
				}

				if ( configuration.CollectNetworkMetrics == true ) {
					try {
						network.Update();

						foreach ( string[] labelValues in network.SentBytes.GetAllLabelValues() ) {
							string networkInterface = labelValues[ 0 ];
							double bytesSent = Math.Round( network.SentBytes.WithLabels( networkInterface ).Value / 1024, 2 );
							double bytesReceived = Math.Round( network.ReceivedBytes.WithLabels( networkInterface ).Value / 1024, 2 );
							
							logger.LogInformation( "Network ({0}): {1} KiB sent, {2} KiB received", networkInterface, bytesSent, bytesReceived );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect network metrics" );
					}
				}

				if ( configuration.CollectServiceMetrics == true ) {
					try {
						services.Update();

						foreach ( string[] labelValues in services.StatusCode.GetAllLabelValues() ) {
							string service = labelValues[ 0 ];
							string name = labelValues[ 1 ];
							string description = labelValues[ 2 ];

							logger.LogInformation( "---- Service '{0}' ({1}, {2}) ----", service, name, description );
							logger.LogInformation( "Status Code: {0}", services.StatusCode.WithLabels( service, name, description ).Value );
							logger.LogInformation( "Exit Code: {0}", services.ExitCode.WithLabels( service, name, description ).Value );
							logger.LogInformation( "Uptime: {0} seconds", services.UptimeSeconds.WithLabels( service, name, description ).Value );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect service metrics" );
					}
				}

				if ( configuration.CollectDockerMetrics == true ) {
					try {
						docker.Update();

						foreach ( string[] labelValues in docker.StatusCode.GetAllLabelValues() ) {
							string id = labelValues[ 0 ];
							string name = labelValues[ 1 ];
							string image = labelValues[ 2 ];

							logger.LogInformation( "---- Docker container '{0}' ({1}, {2}) ----", name, id, image );
							logger.LogInformation( "Status: {0}", docker.StatusCode.WithLabels( id, name, image ).Value );
							logger.LogInformation( "Exit Code: {0}", docker.ExitCode.WithLabels( id, name, image ).Value );
							logger.LogInformation( "Uptime: {0}", docker.CreatedTimestamp.WithLabels( id, name, image ).Value );
							logger.LogInformation( "Health: {0}", docker.HealthStatusCode.WithLabels( id, name, image ).Value );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect Docker metrics" );
					}
				}

				if ( configuration.CollectSNMPMetrics == true ) {
					try {
						snmp.Update();

						foreach ( string[] labelValues in snmp.ServiceCount.GetAllLabelValues() ) {
							string address = labelValues[ 0 ];
							string port = labelValues[ 1 ];
							string name = labelValues[ 2 ];
							string description = labelValues[ 3 ];
							string contact = labelValues[ 4 ];
							string location = labelValues[ 5 ];

							logger.LogInformation( "--- SNMP agent '{0}:{1}' ----", address, port );
							logger.LogInformation( "Name: '{0}'", name );
							logger.LogInformation( "Description: '{0}'", description );
							logger.LogInformation( "Contact: '{0}'", contact );
							logger.LogInformation( "Location: '{0}'", location );
							logger.LogInformation( "Traps Received: {0}", snmp.TrapsReceived.WithLabels( address, port, name, description, contact, location ).Value );
							logger.LogInformation( "Uptime: {0} seconds", snmp.UptimeSeconds.WithLabels( address, port, name, description, contact, location ).Value );
							logger.LogInformation( "Service Count: {0}", snmp.ServiceCount.WithLabels( address, port, name, description, contact, location ).Value );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect SNMP metrics" );
					}
				}

				if ( configuration.CollectInformationMetrics == true ) {
					try {
						information.Update();

						foreach ( string[] labelValues in information.UptimeSeconds.GetAllLabelValues() ) {
							string name = labelValues[ 0 ];
							string operatingSystem = labelValues[ 1 ];
							string architecture = labelValues[ 2 ];
							string version = labelValues[ 3 ];

							logger.LogInformation( "--- Information ----" );
							logger.LogInformation( "Name: '{0}'", name );
							logger.LogInformation( "Operating System: '{0}'", operatingSystem );
							logger.LogInformation( "Architecture: '{0}'", architecture );
							logger.LogInformation( "Version: '{0}'", version );
							logger.LogInformation( "Uptime: {0} seconds", information.UptimeSeconds.WithLabels( name, operatingSystem, architecture, version ).Value );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect information metrics" );
					}
				}

				if ( singleRun == false ) Thread.Sleep( 5000 ); // 5 seconds
			} while ( singleRun == false );

			if ( configuration.CollectSNMPMetrics == true ) {
				if ( singleRun == true ) cancellationTokenSource.Cancel(); // Stop the SNMP agent
				snmp.WaitForTrapListener(); // Block until the SNMP agent has stopped
			}

			// Stop the Prometheus metrics server
			//MetricsServer.Stop();

		}

		// Checks if this application is running as administrator/root, which is required for some of the metrics we're collecting
		private bool IsRunningAsAdmin() {
			// Windows - https://stackoverflow.com/a/11660205
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				WindowsIdentity identity = WindowsIdentity.GetCurrent();
				WindowsPrincipal principal = new WindowsPrincipal( identity );
				return principal.IsInRole( WindowsBuiltInRole.Administrator );

			// Linux - https://github.com/dotnet/runtime/issues/25118#issuecomment-367407469
			} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
				return Syscall.getuid() == 0;

			} else throw new PlatformNotSupportedException( "This platform is not supported." );
		}

	}

}
