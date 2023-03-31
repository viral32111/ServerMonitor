package com.viral32111.servermonitor

import android.app.Activity
import android.util.Log
import com.android.volley.*
import com.google.gson.JsonObject
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import kotlin.math.round

// Holds data about a server
class Server( data: JsonObject, extended: Boolean = false ) {
	var identifier: String
	var jobName: String
	var instanceAddress: String
	var lastScrape: Long
	var hostName: String
	var operatingSystem: String
	var architecture: String
	var version: String
	var uptimeSeconds: Long

	// TODO: supportedActions

	var processorUsage: Float? = null
	var processorFrequency: Float? = null
	var processorTemperature: Float? = null

	var memoryTotalBytes: Long? = null
	var memoryFreeBytes: Long? = null
	var swapTotalBytes: Long? = null
	var swapFreeBytes: Long? = null

	var drives: Array<Drive>? = null
	var networkInterfaces: Array<NetworkInterface>? = null

	var services: Array<Service>? = null
	var dockerContainers: Array<DockerContainer>? = null

	var snmpCommunity: String? = null
	var snmpAgents: Array<SNMPAgent>? = null

	// Deseralise the JSON object
	init {
		identifier = data.get( "identifier" ).asString
		jobName = data.get( "jobName" ).asString
		instanceAddress = data.get( "instanceAddress" ).asString
		lastScrape = data.get( "lastScrape" ).asLong
		hostName = data.get( "hostName" ).asString
		operatingSystem = data.get( "operatingSystem" ).asString
		architecture = data.get( "architecture" ).asString
		version = data.get( "version" ).asString
		uptimeSeconds = round( data.get( "uptimeSeconds" ).asDouble ).toLong()

		if ( extended ) updateUsingAPIData( data )
	}

	// Checks if the server is online or offline
	fun isOnline(): Boolean = uptimeSeconds >= 0

	fun fetchMetrics( activity: Activity, instanceUrl: String, credentialsUsername: String, credentialsPassword: String, successCallback: () -> Unit, errorCallback: () -> Unit ) {
		API.getServer( instanceUrl, credentialsUsername, credentialsPassword, identifier, { data ->
			if ( data != null ) {
				identifier = data.get( "identifier" ).asString
				jobName = data.get( "jobName" ).asString
				instanceAddress = data.get( "instanceAddress" ).asString
				lastScrape = data.get( "lastScrape" ).asLong
				hostName = data.get( "hostName" ).asString
				operatingSystem = data.get( "operatingSystem" ).asString
				architecture = data.get( "architecture" ).asString
				version = data.get( "version" ).asString
				uptimeSeconds = round( data.get( "uptimeSeconds" ).asDouble ).toLong()

				val resources = data.get( "resources" ).asJsonObject

				val processor = resources.get( "processor" ).asJsonObject
				processorUsage = processor.get( "usage" ).asFloat

				successCallback.invoke()

			} else {
				Log.e( Shared.logTag, "Data from API is null?!" )
				errorCallback.invoke()
				showBriefMessage( activity, R.string.serversToastServersNull )
			}

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Failed to get server '${ hostName }' ('${ identifier }', '${ jobName }', '${ instanceAddress }') from API due to '${ error }' (Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

			// Run the custom callback
			errorCallback.invoke()

			when ( error ) {

				// Bad authentication
				is AuthFailureError -> when ( errorCode ) {
					ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serversToastServersAuthenticationUnknownUser )
					ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serversToastServersAuthenticationIncorrectPassword )
					else -> showBriefMessage( activity, R.string.serversToastServersAuthenticationFailure )
				}

				// HTTP 4xx
				is ClientError -> when ( statusCode ) {
					404 -> showBriefMessage( activity, R.string.serversToastServersNotFound )
					else -> showBriefMessage( activity, R.string.serversToastServersClientFailure )
				}

				// HTTP 5xx
				is ServerError -> when ( statusCode ) {
					502 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
					503 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
					504 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
					else -> showBriefMessage( activity, R.string.serversToastServersServerFailure )
				}

				// No Internet connection, malformed domain
				is NoConnectionError -> showBriefMessage( activity, R.string.serversToastServersNoConnection )
				is NetworkError -> showBriefMessage( activity, R.string.serversToastServersNoConnection )

				// Connection timed out
				is TimeoutError -> showBriefMessage( activity, R.string.serversToastServersTimeout )

				// Couldn't parse as JSON
				is ParseError -> showBriefMessage( activity, R.string.serversToastServersParseFailure )

				// ¯\_(ツ)_/¯
				else -> showBriefMessage( activity, R.string.serversToastServersFailure )

			}
		} )

	}

	/**
	 * Updates this server's data.
	 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
	 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
	 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
	 * @throws NullPointerException The API response contained an unexpected null property.
	 */
	fun updateUsingAPIData( data: JsonObject ) {

		// Update basic information
		identifier = data.get( "identifier" ).asString
		jobName = data.get( "jobName" ).asString
		instanceAddress = data.get( "instanceAddress" ).asString
		lastScrape = data.get( "lastScrape" ).asLong
		hostName = data.get( "hostName" ).asString
		operatingSystem = data.get( "operatingSystem" ).asString
		architecture = data.get( "architecture" ).asString
		version = data.get( "version" ).asString
		uptimeSeconds = round( data.get( "uptimeSeconds" ).asDouble ).toLong()

		// TODO: supportedActions

		val resources = data.get( "resources" ).asJsonObject

		// Update processor metrics
		val processor = resources.get( "processor" ).asJsonObject
		processorUsage = processor.get( "usage" ).asFloat
		processorFrequency = processor.get( "frequency" ).asFloat
		processorTemperature = processor.get( "temperature" ).asFloat

		// Update memory metrics
		val memory = resources.get( "memory" ).asJsonObject
		memoryTotalBytes = memory.get( "totalBytes" ).asLong
		memoryFreeBytes = memory.get( "freeBytes" ).asLong

		// Update swap/page-file metrics
		val swap = memory.get( "swap" ).asJsonObject
		swapTotalBytes = swap.get( "totalBytes" ).asLong
		swapFreeBytes = swap.get( "freeBytes" ).asLong

		// Drives
		val drivesList = ArrayList<Drive>()
		for ( drive in resources.get( "drives" ).asJsonArray ) drivesList.add( Drive( drive.asJsonObject ) )
		drives = drivesList.toTypedArray()

		// Network Interfaces
		val netInterfacesList = ArrayList<NetworkInterface>()
		for ( netInterface in resources.get( "networkInterfaces" ).asJsonArray ) netInterfacesList.add( NetworkInterface( netInterface.asJsonObject ) )
		networkInterfaces = netInterfacesList.toTypedArray()

		// Services
		val servicesList = ArrayList<Service>()
		for ( service in data.get( "services" ).asJsonArray ) servicesList.add( Service( service.asJsonObject ) )
		services = servicesList.toTypedArray()

		// Docker containers
		val dockerContainersList = ArrayList<DockerContainer>()
		for ( dockerContainer in data.get( "dockerContainers" ).asJsonArray ) dockerContainersList.add( DockerContainer( dockerContainer.asJsonObject ) )
		dockerContainers = dockerContainersList.toTypedArray()

		// SNMP
		val snmp = data.get( "snmp" ).asJsonObject
		snmpCommunity = snmp.get( "community" ).asString
		val snmpAgentsList = ArrayList<SNMPAgent>()
		for ( snmpAgent in snmp.get( "agents" ).asJsonArray ) snmpAgentsList.add( SNMPAgent( snmpAgent.asJsonObject ) )
		snmpAgents = snmpAgentsList.toTypedArray()

	}

	/**
	 * Updates this server's data.
	 * @param instanceUrl The URL to the connector instance, using the HTTPS schema.
	 * @param credentialsUsername The user to authenticate as.
	 * @param credentialsPassword The password to authenticate with.
	 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
	 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
	 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
	 * @throws NullPointerException The API response contained an unexpected null property.
	 */
	suspend fun updateFromAPI( instanceUrl: String, credentialsUsername: String, credentialsPassword: String ) {

		// Fetch the server, will throw a null pointer exception if null
		val data = API.getServer( instanceUrl, credentialsUsername, credentialsPassword, identifier )!!
		Log.d( Shared.logTag, "Fetched server '${ hostName }' ('${ identifier }', '${ jobName }', '${ instanceAddress }') from API" )

		// Set the properties
		updateUsingAPIData( data )

	}
}
