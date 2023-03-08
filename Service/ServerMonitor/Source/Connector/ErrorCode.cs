using System;

namespace ServerMonitor.Connector {

	public enum ErrorCode {
		Success = 0,
		NoAuthentication = 1,
		UnknownUser = 2,
		IncorrectPassword = 3,
		UnknownRoute = 4,
		UncaughtServerError = 5
	}

}
