using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Encapsulates collecting system disk metrics
	public class Disk : Resource {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Resource/Disk" );

		// Holds the metrics for each disk
		public List<Disk> Disks { get; private set; } = new();

		// Holds the total metrics for all disks
		public double CurrentWriteBytes { get; private set; } = 0;
		public double CurrentReadBytes { get; private set; } = 0;
		public string Name { get; private set; } = string.Empty;
		public string Mountpoint { get; private set; } = string.Empty;
		public double TotalBytes { get; private set; } = 0;
		public double FreeBytes { get; private set; } = 0;
		public int SmartHealth { get; private set; } = 0;

		// Simply returns the utilization
		public double GetUsedBytes() => TotalBytes - FreeBytes;
		public double GetUsedPercentage() => ( GetUsedBytes() / TotalBytes ) * 100;

		// Updates the metrics for Windows...
		public override void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			throw new NotImplementedException();
		}

		// Updates the metrics for Linux...
		public override void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) throw new InvalidOperationException( "Method only available on Linux" );

			throw new NotImplementedException();
		}

	}
}
