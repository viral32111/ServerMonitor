using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Logging; // https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

/*using System.Diagnostics;
using System.Diagnostics.PerformanceData;
using System.Resources;
using System.Reflection;*/

namespace ServerMonitor.Collector {

	public static class Collector {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Collector" );

		public static void HandleCommand( string extraConfigurationFilePath ) {
			logger.LogInformation( "Collector mode!" );

			Config configuration = Configuration.Load( extraConfigurationFilePath );
			logger.LogInformation( "Loaded configuration. Test = {0}", configuration.Test );

			// https://stackoverflow.com/a/278088
			/*PerformanceCounter processorCounter = new( "Processor", "% Processor Time", "_Total" );
			PerformanceCounter memoryCounter = new( "Memory", "Available MBytes" );

			logger.LogDebug( "Measuring processor usage, this will take a second..." );
			processorCounter.NextValue();
			Thread.Sleep( 1000 );
			logger.LogInformation( "Processor: {0} %", processorCounter.NextValue() );

			logger.LogInformation( "Memory: {0} MB", memoryCounter.NextValue() );*/

			while ( true ) {
				GetSystemResourceUsageLinux();
				Thread.Sleep( 1000 );
			}
		}

		private static void GetSystemResourceUsageLinux() {

			// Read /proc/meminfo
			using ( FileStream fileStream = new( "/proc/meminfo", FileMode.Open, FileAccess.Read ) ) {
				using ( StreamReader streamReader = new( fileStream ) ) {
					Dictionary<string, float> memoryInformation = new();

					do {
						string? fileLine = streamReader.ReadLine();
						if ( fileLine == null ) break;

						string[] lineParts = fileLine.Split( ":", 2 );
						if ( lineParts.Length != 2 ) throw new Exception( "Unexpected number of parts on line of file" );

						string key = lineParts[ 0 ].Trim();
						string value = lineParts[ 1 ].Trim();
						//logger.LogDebug( "Line: '{0}', Key: '{1}', Value: '{2}'", fileLine, key, value );

						string[] valueParts = value.Split( " ", 2 );
						if ( float.TryParse( valueParts[ 0 ].Trim(), out float valueRaw ) != true ) throw new Exception( "Value is not a float" );
						if ( valueParts.Length == 2 ) {
							string valueType = valueParts[ 1 ].Trim();

							// https://superuser.com/q/1737654
							if ( valueType == "kB" ) {
								float valueInMegabytes = valueRaw / 1024;
								//int valueInGigabytes = valueInMegabytes / 1024;
								memoryInformation.Add( key, valueInMegabytes );
							} else {
								throw new Exception( "Unexpected value type" );
							}
						} else {
							memoryInformation.Add( key, valueRaw );
						}

					} while ( !streamReader.EndOfStream );

					// https://stackoverflow.com/a/41251290
					float totalMemory = memoryInformation[ "MemTotal" ];
					float freeMemory = memoryInformation[ "MemFree" ];
					float cachedMemory = memoryInformation[ "Cached" ];// + memoryInformation[ "SReclaimable" ] - memoryInformation[ "Shmem" ];
					float bufferedMemory = memoryInformation[ "Buffers" ];

					float usedMemory = totalMemory - freeMemory - cachedMemory - bufferedMemory;
					float usedMemoryPercentage = ( usedMemory / totalMemory ) * 100;

					float totalSwap = memoryInformation[ "SwapTotal" ];
					float freeSwap = memoryInformation[ "SwapFree" ];
					float usedSwap = totalSwap - freeSwap;
					float usedSwapPercentage = ( usedSwap / totalSwap ) * 100;

					logger.LogInformation( "Memory: {0} MiB / {1} MiB ({2} MiB free, {2}% usage)", Math.Round( usedMemory, 2 ), Math.Round( totalMemory, 2 ), Math.Round( freeMemory, 2 ), Math.Round( usedMemoryPercentage, 0 ) );
					logger.LogInformation( "Swap: {0} MiB / {1} MiB ({2} MiB free, {2}% usage)", Math.Round( usedSwap, 2 ), Math.Round( totalSwap, 2 ), Math.Round( freeSwap, 2 ), Math.Round( usedSwapPercentage, 0 ) );

					//float totalUsedMemory = memoryInformation[ "MemTotal" ] - memoryInformation[ "MemFree" ]; //- memoryInformation[ "Buffers" ] - memoryInformation[ "Cached" ];
					//float cachedAndBufferedMemory = memoryInformation[ "Buffers" ] + memoryInformation[ "Cached" ]; 

					/*logger.LogInformation( "Total Memory: {0} MiB", memoryInformation[ "MemTotal" ] );
					logger.LogInformation( "Free Memory: {0} MiB", memoryInformation[ "MemFree" ] );
					logger.LogInformation( "Available Memory: {0} MiB", memoryInformation[ "MemAvailable" ] );
					logger.LogInformation( "Buffered Memory: {0} MiB", memoryInformation[ "Buffers" ] );
					logger.LogInformation( "Cached Memory: {0} MiB", memoryInformation[ "Cached" ] );
					logger.LogInformation( "Total Swap: {0} MiB", memoryInformation[ "SwapTotal" ] );
					logger.LogInformation( "Free Swap: {0} MiB", memoryInformation[ "SwapFree" ] );*/
				}
			}

		}

	}

}
