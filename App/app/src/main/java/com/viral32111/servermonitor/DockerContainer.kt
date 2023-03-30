package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a Docker container on a server.
 * @param data The JSON object from the API representing the Docker container.
 */
class DockerContainer( data: JsonObject ) {
	val id: String
	val name: String
	val image: String
	val statusCode: Int
	val exitCode: Int
	val healthStatusCode: Int
	val uptimeSeconds: Long

	// TODO: supportedActions
	// TODO: logs

	init {
		id = data.get( "id" ).asString
		name = data.get( "name" ).asString
		image = data.get( "image" ).asString
		statusCode = data.get( "statusCode" ).asInt
		exitCode = data.get( "exitCode" ).asInt
		healthStatusCode = data.get( "healthStatusCode" ).asInt
		uptimeSeconds = data.get( "uptimeSeconds" ).asLong

		// TODO: supportedActions
		// TODO: logs
	}

	/**
	 * Checks if this Docker container is running.
	 * @return True if this Docker container is running, false otherwise.
	 */
	fun isRunning(): Boolean = statusCode == 1 && uptimeSeconds >= 0.0
}
