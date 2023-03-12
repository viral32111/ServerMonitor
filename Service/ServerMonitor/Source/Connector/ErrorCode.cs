using System;

namespace ServerMonitor.Connector {

	public enum ErrorCode {
		Success = 0,
		NoAuthentication = 1,
		UnknownUser = 2,
		IncorrectPassword = 3,
		UnknownRoute = 4,
		UncaughtServerError = 5,
		ExampleData = 6,
		NoParameters = 7,
		MissingParameter = 8,
		ServerNotFound = 9,
		InvalidParameter = 10,
		ServerOffline = 11,
	}

}
