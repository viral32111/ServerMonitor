package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.ForegroundInfo
import androidx.work.WorkerParameters
import androidx.work.workDataOf
import com.viral32111.servermonitor.activity.ServersActivity
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.Notify
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext

// https://developer.android.com/guide/background/persistent/threading/coroutineworker

class UpdateWorker(
	applicationContext: Context,
	parameters: WorkerParameters
) : CoroutineWorker( applicationContext, parameters ) {

	companion object {

		// Unique name for this worker
		const val NAME = "ALWAYS_ONGOING_NOTIFICATION_WORKER"

		// Keys for input data
		const val BASE_URL = "BASE_URL"
		const val CREDENTIALS_USERNAME = "CREDENTIALS_USERNAME"
		const val CREDENTIALS_PASSWORD = "CREDENTIALS_PASSWORD"
		const val AUTOMATIC_REFRESH_INTERVAL = "AUTOMATIC_REFRESH_INTERVAL"

		// Keys for success output data
		const val SUCCESS = "SUCCESS"

		// Keys for failure output data
		const val FAILURE_REASON = "FAILURE_REASON"
		const val FAILURE_NULL_INPUT_DATA = 1
		const val FAILURE_NULL_API_RESPONSE = 2
		const val FAILURE_API_REQUEST_EXCEPTION = 3

		// Keys for progress output data
		const val PROGRESS_ARE_THERE_ISSUES = "PROGRESS_ARE_THERE_ISSUES"

	}

	override suspend fun doWork(): Result {

		// Get the input data
		val baseUrl = inputData.getString( BASE_URL )
		val credentialsUsername = inputData.getString( CREDENTIALS_USERNAME )
		val credentialsPassword = inputData.getString( CREDENTIALS_PASSWORD )
		val automaticRefreshInterval = inputData.getInt( AUTOMATIC_REFRESH_INTERVAL, -1 )

		// Ensure the input data is valid
		if (
			baseUrl.isNullOrBlank() ||
			credentialsUsername.isNullOrBlank() ||
			credentialsPassword.isNullOrBlank() ||
			automaticRefreshInterval == -1
		) return Result.failure( workDataOf( FAILURE_REASON to FAILURE_NULL_INPUT_DATA ) )

		// Convert automatic refresh interval to milliseconds
		val automaticRefreshIntervalMillis = automaticRefreshInterval * 1000L

		// Intent to open the Servers activity when the always on-going notification is pressed
		val serversActivityIntent = Intent( applicationContext, ServersActivity::class.java ).apply {
			flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
		}

		// Create the always on-going notification
		setForeground( createAlwaysOngoingNotification(
			R.string.notificationOngoingTextUnknown,
			R.color.statusDead,
			serversActivityIntent
		) )

		// Wait 5 seconds before starting
		delay( 5000L )

		// Loop on the I/O task pool until we're told to stop (i.e., cancelled)
		withContext( Dispatchers.IO ) {
			while ( !isStopped ) {

				// Attempt to fetch the servers
				try {
					val servers = API.getServersImproved( baseUrl, credentialsUsername, credentialsPassword ) ?: return@withContext Result.failure( workDataOf( FAILURE_REASON to FAILURE_NULL_API_RESPONSE ) )

					// Check if there are issues with any of the servers
					val areThereIssues = servers.any { server -> server.areThereIssues() }

					// Update the always on-going notification to reflect if there are issues
					if ( areThereIssues ) {
						setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextBad, R.color.statusBad, serversActivityIntent ) )
					} else {
						setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextGood, R.color.statusGood, serversActivityIntent ) )
					}

					// Update the worker's progress
					setProgress( workDataOf( PROGRESS_ARE_THERE_ISSUES to areThereIssues ) )

					// Pause for the automatic refresh interval
					delay( automaticRefreshIntervalMillis )

				// Fail if an exception occurs
				} catch ( exception: Exception ) {
					Log.e( Shared.logTag, "Exception inside always on-going notification worker! ('${ exception.cause }', '${ exception.message }')" )

					// TODO: Update the always on-going notification with an error message instead of failure, depending on why it failed (e.g., API error vs device has no Internet connection)
					// If the error is unrecoverable, disable ongoing mode so the user can swipe the notification away
					setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextError, R.color.statusDead, serversActivityIntent, false ) )

					// TODO: Or use Result.retry() so that the retry policy will automatically retry the worker?
					return@withContext Result.retry()

					//return@withContext Result.failure( workDataOf( FAILURE_REASON to FAILURE_API_REQUEST_EXCEPTION ) )
				}
			}
		}

		// Success if we got to this point
		return Result.success()

	}

	// Creates the always on-going notification required for a long-running foreground worker - https://developer.android.com/guide/background/persistent/how-to/long-running
	private fun createAlwaysOngoingNotification( textId: Int, colorId: Int, intent: Intent, isOngoing: Boolean = true ): ForegroundInfo {

		// Create the notification
		val notification = Notify.createProgressNotification(
			applicationContext,
			intent,
			Notify.CHANNEL_ALWAYS_ONGOING,
			R.string.notificationOngoingTitle,
			textId,
			applicationContext.getColor( colorId ),
			isOngoing = isOngoing
		)

		/*
		Notify.showNotification( applicationContext, alwaysOngoingNotification, Notify.IDENTIFIER_ALWAYS_ONGOING )
		Log.d( Shared.logTag, "Created always ongoing notification (${ Notify.IDENTIFIER_ALWAYS_ONGOING })" )
		*/

		// Return foreground information, with service type if Android 10 (API 29+)
		return if ( Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q ) {
			ForegroundInfo( Notify.IDENTIFIER_ALWAYS_ONGOING, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC )
		} else {
			ForegroundInfo( Notify.IDENTIFIER_ALWAYS_ONGOING, notification )
		}

	}

	// Helpers to create output data for success/failure results
	/*
	private fun createSuccessData( serverCount: Int ) = Data.Builder().putInt( SUCCESS_SERVERS_COUNT, serverCount ).build()
	private fun createFailureData( reason: Int ) = Data.Builder().putInt( FAILURE_REASON, reason ).build()
	*/

}
