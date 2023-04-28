package com.viral32111.servermonitor.data

import com.google.gson.JsonObject
import com.viral32111.servermonitor.R

/**
 * Represents a Docker container on a server.
 * @param data The JSON object from the API representing the Docker container.
 */
class DockerContainer( data: JsonObject ) {
	val id: String
	val name: String
	val image: String
	private val statusCode: Int
	private val exitCode: Int
	private val healthStatusCode: Int
	val uptimeSeconds: Long

	// TODO: supportedActions & logs

	init {
		id = data.get( "id" ).asString
		name = data.get( "name" ).asString
		image = data.get( "image" ).asString
		statusCode = data.get( "statusCode" ).asInt
		exitCode = data.get( "exitCode" ).asInt
		healthStatusCode = data.get( "healthStatusCode" ).asInt
		uptimeSeconds = data.get( "uptimeSeconds" ).asLong

		// TODO: supportedActions & logs
	}

	/**
	 * Checks if this Docker container is running.
	 * @return True if this Docker container is running, false otherwise.
	 */
	//fun isRunning(): Boolean = statusCode == 1 && uptimeSeconds >= 0.0

	// Gets the short identifier
	fun getShortIdentifier() = this.id.substring( 0, 12 )

	// Checks if this container is using an old (deleted) image
	fun isImageOld() = this.image.startsWith( "sha256:" )

	// Gets the text representation of the status
	fun getStatusText() = when ( this.statusCode ) {
		0 -> "Created"
		1 -> "Running"
		2 -> "Restarting"
		3 -> "Dead"
		4 -> "Exited"
		5 -> "Paused"
		6 -> "Removing"
		else -> "Unknown"
	}

	// Gets the appropriate color for the status
	fun getStatusColor( statusText: String = this.getStatusText() ) = when ( statusText ) {
		"Created" -> R.color.statusNeutral
		"Running" -> R.color.statusGood
		"Restarting" -> R.color.statusWarning
		"Dead" -> R.color.statusBad
		"Exited" -> R.color.statusBad
		"Paused" -> R.color.statusNeutral
		"Removing" -> R.color.statusWarning
		else -> R.color.statusDead
	}

	// Gets the text representation of the health
	fun getHealthText() = when ( this.statusCode ) {
		0 -> "Unhealthy"
		1 -> "Healthy"
		else -> "Unknown"
	}

	// Gets the appropriate color for the health status
	fun getHealthColor( healthText: String = this.getHealthText() ) = when ( healthText ) {
		"Unhealthy" -> R.color.statusBad
		"Healthy" -> R.color.statusGood
		else -> R.color.statusDead
	}

	// Checks if there are any issues - restarting/dead/exited, unhealthy, error exit code
	fun areThereIssues(): Boolean {
		if ( this.statusCode != 0 && this.statusCode != 1 ) return this.exitCode != 0
		if ( this.healthStatusCode != -1 ) return this.healthStatusCode == 0
		return this.statusCode == 2 || this.statusCode == 3
	}

}
