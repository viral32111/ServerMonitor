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
		ServiceNotFound = 12,
		MethodNotAllowed = 13,
		InvalidAuthentication = 14,
		IncorrectAuthentication = 15,
		NoPayload = 16,
		NoContentType = 17,
		InvalidContentType = 18,
		InvalidPayload = 19,
		WrongServer = 20,
		ActionNotExecutable = 21,
		ActionServerUnknown = 22,
		UnknownAction = 23,
	}

}
