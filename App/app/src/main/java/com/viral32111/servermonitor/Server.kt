package com.viral32111.servermonitor

import android.app.Activity
import android.util.Log
import com.android.volley.*
import com.google.gson.JsonObject
import kotlin.math.round

// Holds data about a server
class Server( data: JsonObject ) {
	var identifier: String
	var jobName: String
	var instanceAddress: String
	var lastScrape: Long
	var hostName: String
	var operatingSystem: String
	var architecture: String
	var version: String
	var uptimeSeconds: Long

	var processorUsage: Float? = null
	var processorFrequency: Float? = null
	var processorTemperature: Float? = null

	// Decode the JSON object from the GET /servers array
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

		/*
		Log.d( Shared.logTag, "Identifier: '${ Identifier }'" )
		Log.d( Shared.logTag, "Job Name: '${ JobName }'" )
		Log.d( Shared.logTag, "Instance Address: '${ InstanceAddress }'" )
		Log.d( Shared.logTag, "Last Scrape: '${ LastScrape }'" )
		Log.d( Shared.logTag, "Host Name: '${ HostName }'" )
		Log.d( Shared.logTag, "Operating System: '${ OperatingSystem }'" )
		Log.d( Shared.logTag, "Architecture: '${ Architecture }'" )
		Log.d( Shared.logTag, "Version: '${ Version }'" )
		Log.d( Shared.logTag, "Uptime: '${ UptimeSeconds }' seconds" )
		*/
	}

	// Checks if the server is online or offline
	fun isOnline(): Boolean = uptimeSeconds >= 0

	// TODO: API call for GET /server, populate more properties with data
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

	suspend fun update( instanceUrl: String, credentialsUsername: String, credentialsPassword: String ) {
		val data = API.getServer( instanceUrl, credentialsUsername, credentialsPassword, identifier )!!

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
		processorTemperature = processor.get( "temperature" ).asFloat
	}
}
