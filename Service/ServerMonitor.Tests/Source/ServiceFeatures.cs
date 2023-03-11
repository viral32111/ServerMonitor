using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Xunit;

// Completely disable parallel test execution for this entire project because it just breaks everything
[ assembly: CollectionBehavior( CollectionBehavior.CollectionPerAssembly ) ]

namespace ServerMonitor.Tests {

	public class ServerFeatures {

		[ Fact ]
		public void TestLoadingConfiguration() {
			ServerMonitor.Configuration.Load( Path.Combine( Environment.CurrentDirectory, "config.json" ) );
			Assert.NotNull( ServerMonitor.Configuration.Config );
		}

		[ Fact ]
		public void TestLoggingLevels() {
			ILogger logger = Logging.CreateLogger( "ServerFeatureUnitTests" );
			logger.LogDebug( "Debug" );
			logger.LogInformation( "Information" );
			logger.LogWarning( "Warning" );
			logger.LogError( "Error" );
			logger.LogCritical( "Critical" );
		}

	}

}
