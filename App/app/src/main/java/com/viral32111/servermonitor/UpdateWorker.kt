package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.util.Log
import androidx.lifecycle.LifecycleOwner
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.Data
import androidx.work.ExistingWorkPolicy
import androidx.work.ForegroundInfo
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkInfo
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import androidx.work.workDataOf
import com.viral32111.servermonitor.activity.ServersActivity
import com.viral32111.servermonitor.database.Issue
import com.viral32111.servermonitor.database.initialiseDatabase
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.Notify
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import java.util.concurrent.TimeUnit

// Coroutine worker for the always on-going notification - https://developer.android.com/guide/background/persistent/threading/coroutineworker

class UpdateWorker(
	applicationContext: Context,
	parameters: WorkerParameters
) : CoroutineWorker( applicationContext, parameters ) {

	companion object {

		// Unique name for this worker
		private const val NAME = "ALWAYS_ONGOING_NOTIFICATION_WORKER"

		// Keys for input data
		const val BASE_URL = "BASE_URL"
		const val CREDENTIALS_USERNAME = "CREDENTIALS_USERNAME"
		const val CREDENTIALS_PASSWORD = "CREDENTIALS_PASSWORD"
		const val AUTOMATIC_REFRESH_INTERVAL = "AUTOMATIC_REFRESH_INTERVAL"
		const val NOTIFICATION_WHEN_ISSUE_ARISES = "NOTIFICATION_WHEN_ISSUE_ARISES"

		// Keys for success output data
		//const val SUCCESS = "SUCCESS"

		// Keys for failure output data
		const val FAILURE_REASON = "FAILURE_REASON"
		const val FAILURE_NULL_INPUT_DATA = 1
		const val FAILURE_NULL_API_RESPONSE = 2
		//const val FAILURE_API_REQUEST_EXCEPTION = 3

		// Keys for progress output data
		const val PROGRESS_ARE_THERE_ISSUES = "PROGRESS_ARE_THERE_ISSUES"

		// Creates this worker - https://developer.android.com/guide/background/persistent/getting-started/define-work
		fun setup( applicationContext: Context, lifecycleOwner: LifecycleOwner, baseUrl: String, credentialsUsername: String, credentialsPassword: String, automaticRefreshInterval: Int, notificationWhenIssueArises: Boolean, shouldEnqueue: Boolean = true ) {

			// Data to give to the worker - https://developer.android.com/guide/background/persistent/getting-started/define-work#input_output
			val workerInputData = Data.Builder()
				.putString( this.BASE_URL, baseUrl )
				.putString( this.CREDENTIALS_USERNAME, credentialsUsername )
				.putString( this.CREDENTIALS_PASSWORD, credentialsPassword )
				.putInt( this.AUTOMATIC_REFRESH_INTERVAL, automaticRefreshInterval )
				.putBoolean( this.NOTIFICATION_WHEN_ISSUE_ARISES, notificationWhenIssueArises )
				.build()

			// Requirements for deferring the worker - https://developer.android.com/guide/background/persistent/getting-started/define-work#schedule_periodic_work
			val workerConstraints = Constraints.Builder()
				.setRequiredNetworkType( NetworkType.CONNECTED ) // Only run when connected to a network
				.setRequiresBatteryNotLow( true ) // Do not run when battery is low
				.setRequiresCharging( false ) // Doesn't matter if the device is charging
				.setRequiresDeviceIdle( false ) // Doesn't matter if the device is idle
				.setRequiresStorageNotLow( false ) // Doesn't matter if storage space is low
				.build()

			// Create the worker request - https://developer.android.com/guide/background/persistent/getting-started/define-work#schedule_one-time_work
			val workerRequest = OneTimeWorkRequestBuilder<UpdateWorker>()
				.setConstraints( workerConstraints )
				.setInputData( workerInputData )
				//.setInitialDelay( 5L, TimeUnit.SECONDS ) // Wait 5 seconds before starting
				.setBackoffCriteria( BackoffPolicy.LINEAR, 5L, TimeUnit.SECONDS ) // Retry on failure after 5 seconds
				.build()

			// Get our worker manager
			val workerManager = WorkManager.getInstance( applicationContext )

			// Observe any updates on the worker
			this.observe( workerManager, lifecycleOwner )

			// Cancel all existing workers - This is needed as a worker is automatically created on launch due to the service in the manifest
			workerManager.cancelAllWork()
			Log.d( Shared.logTag, "Cancelled all existing workers" )

			// Queue up the worker - https://developer.android.com/guide/background/persistent/how-to/manage-work
			if ( shouldEnqueue ) {
				workerManager.enqueueUniqueWork( this.NAME, ExistingWorkPolicy.REPLACE, workerRequest )
				Log.d( Shared.logTag, "Enqueued always on-going notification worker" )
			} else {
				Log.d( Shared.logTag, "Skipped enqueueing always on-going notification worker" )
			}
		}

		// Observe all the always on-going notification workers (ideally only 1) for the rest of time - https://developer.android.com/guide/background/persistent/how-to/observe
		private fun observe( workerManager: WorkManager, lifecycleOwner: LifecycleOwner ) {

			// Remove existing observers
			workerManager.getWorkInfosForUniqueWorkLiveData( this.NAME ).removeObservers( lifecycleOwner )
			Log.d( Shared.logTag, "Removed all observers for the always on-going notification worker, in favour of a new observer..." )

			// Register new observer
			workerManager.getWorkInfosForUniqueWorkLiveData( this.NAME ).observe( lifecycleOwner ) { workInfos: List<WorkInfo> ->

				// How could there possibly be more than one UNIQUE worker?
				if ( workInfos.count() > 1 ) Log.wtf( Shared.logTag, "There are ${ workInfos.count() } always on-going notification workers?!" )

				// Loop through each of the workers...
				for ( workInfo in workInfos ) {

					// Get the progress value
					val areThereIssues = workInfo.progress.getBoolean( this.PROGRESS_ARE_THERE_ISSUES, false )

					when ( workInfo.state ) {

						// When the worker finishes successfully...
						WorkInfo.State.SUCCEEDED -> {
							Log.d( Shared.logTag, "Always on-going notification worker '${ workInfo.id }' finished with success (Are there issues? ${ areThereIssues })" )
						}

						// When the worker finishes erroneously...
						WorkInfo.State.FAILED -> {
							val failureReason = workInfo.outputData.getInt( this.FAILURE_REASON, -1 )
							Log.e( Shared.logTag, "Always on-going notification worker '${ workInfo.id }' finished with failure '${ failureReason }' (Are there issues? ${ areThereIssues })" )
						}

						// Some other state, or just a progress update
						else -> Log.d( Shared.logTag, "Always on-going notification worker '${ workInfo.id }' observed to be in state '${ workInfo.state }' (Are there issues? ${ areThereIssues })" )

					}

				}

			}

		}

		// Helper for calling the observation function with context instead of an existing work manager
		//fun observe( applicationContext: Context, lifecycleOwner: LifecycleOwner ) = this.observe( WorkManager.getInstance( applicationContext ), lifecycleOwner )

	}

	override suspend fun doWork(): Result {

		// Get the input data
		val baseUrl = inputData.getString( BASE_URL )
		val credentialsUsername = inputData.getString( CREDENTIALS_USERNAME )
		val credentialsPassword = inputData.getString( CREDENTIALS_PASSWORD )
		val automaticRefreshInterval = inputData.getInt( AUTOMATIC_REFRESH_INTERVAL, -1 )
		val notificationWhenIssueArises = inputData.getBoolean( NOTIFICATION_WHEN_ISSUE_ARISES, true )

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

					// If servers were returned...
					if ( servers.isNotEmpty() ) {

						// Get the database & table
						val database = initialiseDatabase( applicationContext )
						val issueHistory = database.issueHistory()

						// Fetch the latest on-going issue from the database
						val ongoingIssue = issueHistory.fetchOngoing()

						// Check if there are issues with any of the servers
						val serversWithIssues = servers.filter { server -> server.areThereIssues() }

						// If an issue has been detected...
						if ( serversWithIssues.isNotEmpty() ) {

							// If there is an on-going issue then increment the total number of issue detections
							if ( ongoingIssue != null ) {
								issueHistory.incrementTotalCountByIdentifier( ongoingIssue.identifier, 1 )
								Log.d( Shared.logTag, "Incremented on-going issue ${ ongoingIssue.identifier } (was ${ ongoingIssue.totalCount }, now ${ ongoingIssue.totalCount + 1 })" )

							// Begin a new on-going issue if there isn't one already
							} else {
								val newIssueIdentifier = issueHistory.create( Issue() )
								Log.d( Shared.logTag, "Began new on-going issue $newIssueIdentifier" )

								// Show an additional notification for this issue detection
								if ( notificationWhenIssueArises ) withContext( Dispatchers.Main ) {
									val notification = Notify.createTextNotification(
										applicationContext,
										Intent( applicationContext, ServersActivity::class.java ),
										Notify.CHANNEL_WHEN_ISSUE_ARISES,
										R.string.notificationIssueTitle,
										applicationContext.getString( R.string.notificationIssueText ).format( serversWithIssues.joinToString( ", " ) { server -> server.hostName.uppercase() } ),
										applicationContext.getColor( R.color.statusBad )
									)

									Notify.showNotification( applicationContext, notification )
									Log.d( Shared.logTag, "Showing notification as an issue has arisen..." )
								}
							}

							// Update the always on-going notification to reflect there are issues
							setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextBad, R.color.statusBad, serversActivityIntent ) )

						// No issues detected...
						} else {

							// If there is an on-going issue then finish it
							if ( ongoingIssue != null ) {
								issueHistory.updateFinishedAtByIdentifier( ongoingIssue.identifier, System.currentTimeMillis() )
								Log.d( Shared.logTag, "Finished on-going issue ${ ongoingIssue.identifier } at count ${ ongoingIssue.totalCount }" )
							}

							// Update the always on-going notification to reflect everything is good
							setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextGood, R.color.statusGood, serversActivityIntent ) )

						}

						// Update the worker's progress
						setProgress( workDataOf( PROGRESS_ARE_THERE_ISSUES to serversWithIssues.isNotEmpty() ) )

					// No servers were returned...
					} else {
						setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextEmpty, R.color.statusDead, serversActivityIntent ) )
					}

					// Pause for the automatic refresh interval
					delay( automaticRefreshIntervalMillis )

				// Fail if an exception occurs
				} catch ( exception: Exception ) {
					Log.e( Shared.logTag, "Exception inside always on-going notification worker! ('${ exception.cause }', '${ exception.message }')" )

					// Update the always on-going notification with an error message - ongoing mode is disabled so the user can dismiss it
					setForeground( createAlwaysOngoingNotification( R.string.notificationOngoingTextError, R.color.statusDead, serversActivityIntent, false ) )

					// We failed, try again...
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
