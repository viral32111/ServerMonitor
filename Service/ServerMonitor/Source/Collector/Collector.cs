using System;
using System.Threading;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using Mono.Unix.Native; // https://github.com/mono/mono.posix
using Prometheus; // https://github.com/prometheus-net/prometheus-net
using ServerMonitor.Collector.Resource;

namespace ServerMonitor.Collector {

	public static class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		// Entry-point for the "collector" sub-command...
		public static void HandleCommand( Config configuration, bool singleRun ) {
			logger.LogInformation( "Launched in collector mode" );

			// Fail if we're not running as administrator/root
			/*if ( IsRunningAsAdmin() == false ) {
				logger.LogError( "This program must be run as administrator/root" );
				Environment.Exit( 1 );
			}*/

			// Start the Prometheus metrics server
			MetricServer server = new(
				hostname: configuration.PrometheusListenAddress,
				port: configuration.PrometheusListenPort,
				url: configuration.PrometheusListenPath,
				useHttps: false
			);
			server.Start();
			logger.LogInformation( "Prometheus Metrics server listening on http://{0}:{1}/{2}", configuration.PrometheusListenAddress, configuration.PrometheusListenPort, configuration.PrometheusListenPath );

			// Create instances of each resource collector
			Memory memory = new( configuration );
			Processor processor = new( configuration );
			Uptime uptime = new( configuration );
			Disk disk = new( configuration );
			Network network = new( configuration );

			// Create an instance of the service collector
			Services services = new( configuration );

			// Create an instance of the Docker collector
			Docker docker = new( configuration );

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

					foreach ( string[] labelValues in docker.Status.GetAllLabelValues() ) {
						string id = labelValues[ 0 ];
						string name = labelValues[ 1 ];
						string image = labelValues[ 2 ];

						logger.LogInformation( "---- Docker container '{0}' ({1}, {2}) ----", name, id, image );
						logger.LogInformation( "Status: {0}", docker.Status.WithLabels( id, name, image ).Value );
						logger.LogInformation( "Exit Code: {0}", docker.ExitCode.WithLabels( id, name, image ).Value );
						logger.LogInformation( "Uptime: {0}", docker.CreatedTimestamp.WithLabels( id, name, image ).Value );
					}
				} catch ( Exception exception ) {
					logger.LogError( exception, "Failed to collect Docker metrics" );
				}
			}

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

				if ( configuration.CollectUptimeMetrics == true ) {
					try {
						uptime.Update();
						logger.LogInformation( "Uptime: {0} seconds", uptime.UptimeSeconds.Value );
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect uptime metrics" );
					}
				}

				if ( configuration.CollectDiskMetrics == true ) {
					try {
						disk.Update();
						
						foreach ( string[] labelValues in disk.ReadBytes.GetAllLabelValues() ) {
							string driveName = labelValues[ 0 ];

							double bytesRead = Math.Round( disk.ReadBytes.WithLabels( driveName ).Value / 1024 / 1024, 2 );
							double bytesWritten = Math.Round( disk.WriteBytes.WithLabels( driveName ).Value / 1024 / 1024, 2 );
							int healthPercentage = ( int ) disk.Health.WithLabels( driveName ).Value;
							
							logger.LogInformation( "Drive ({0}): {1} MiB read, {2} MiB written, {3}% health", driveName, bytesRead, bytesWritten, healthPercentage );
						}
						
						foreach ( string[] labelValues in disk.TotalBytes.GetAllLabelValues() ) {
							string partitionName = labelValues[ 0 ];
							string partitionMountPath = labelValues[ 1 ];

							double totalSpace = Math.Round( disk.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value / 1024 / 1024 / 1024, 2 );
							double freeSpace = Math.Round( disk.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value / 1024 / 1024 / 1024, 2 );
							double usedSpace = Math.Round( ( disk.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value - disk.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value ) / 1024 / 1024 / 1024, 2 );
							double usedSpacePercentage = Math.Round( ( disk.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value - disk.FreeBytes.WithLabels( partitionName, partitionMountPath ).Value ) / disk.TotalBytes.WithLabels( partitionName, partitionMountPath ).Value * 100, 0 );
							
							logger.LogInformation( "Partition ({0}, {1}): {2} GiB / {3} GiB ({4} GiB free, {5}% usage)", partitionName, partitionMountPath, usedSpace, totalSpace, freeSpace, usedSpacePercentage );
						}
					} catch ( Exception exception ) {
						logger.LogError( exception, "Failed to collect disk metrics" );
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

				if ( singleRun == false ) Thread.Sleep( 5000 ); // 5 seconds
			} while ( singleRun == false );
		}

		// Checks if this application is running as administrator/root, which is required for some of the metrics we're collecting
		private static bool IsRunningAsAdmin() {
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
