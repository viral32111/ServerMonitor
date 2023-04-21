package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.Data
import androidx.work.ForegroundInfo
import androidx.work.WorkerParameters
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.Notify
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

// https://developer.android.com/guide/background/persistent/threading/coroutineworker
// https://developer.android.com/guide/background/persistent/how-to/observe

class UpdateWorker(
	applicationContext: Context,
	parameters: WorkerParameters
) : CoroutineWorker( applicationContext, parameters ) {

	companion object {
		const val BASE_URL = "BASE_URL"
		const val CREDENTIALS_USERNAME = "CREDENTIALS_USERNAME"
		const val CREDENTIALS_PASSWORD = "CREDENTIALS_PASSWORD"

		const val FAILURE_REASON = "FAILURE_REASON"
		const val FAILURE_NULL_INPUT_DATA = 1
		const val FAILURE_NULL_API_RESPONSE = 2

		const val FAILURE_API_EXCEPTION_API_ERROR_CODE = "FAILURE_API_EXCEPTION_HTTP_STATUS_CODE"
		const val FAILURE_API_EXCEPTION_HTTP_STATUS_CODE = "FAILURE_API_EXCEPTION_HTTP_STATUS_CODE"

		const val SUCCESS_SERVERS_COUNT = "SERVERS_COUNT"

		const val Progress = "Progress"
		private const val delayDuration = 1000L
	}

	override suspend fun doWork(): Result {
		val baseUrl = inputData.getString( BASE_URL )
		val credentialsUsername = inputData.getString( CREDENTIALS_USERNAME )
		val credentialsPassword = inputData.getString( CREDENTIALS_PASSWORD )

		if ( baseUrl.isNullOrBlank() || credentialsUsername.isNullOrBlank() || credentialsPassword.isNullOrBlank() ) return Result.failure( createFailureData( FAILURE_NULL_INPUT_DATA ) )

		withContext( Dispatchers.IO ) {
			try {
				val servers = API.getServersImproved( baseUrl, credentialsUsername, credentialsPassword ) ?: return@withContext Result.failure( createFailureData( FAILURE_NULL_API_RESPONSE ) )
				val serverCount = servers.count()

				return@withContext Result.success( createSuccessData( serverCount ) )
			} catch ( exception: Exception ) {
				return@withContext Result.failure()
			}
		}

		/*
		val firstUpdate = workDataOf( Progress to 0 )
		val lastUpdate = workDataOf( Progress to 100 )

		delay( delayDuration )
		setProgress( firstUpdate )
		delay( delayDuration )
		setProgress( lastUpdate )
		delay( delayDuration )
		*/

		return Result.success()
	}

	// TODO: https://developer.android.com/guide/background/persistent/how-to/long-running
	/*
	private fun createForegroundInformation(): ForegroundInfo {
		val notificationIntent = Intent( applicationContext, this::class.java ).apply {
			flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
		}

		val alwaysOngoingNotification = Notify.createProgressNotification(
			applicationContext, notificationIntent, Notify.CHANNEL_ALWAYS_ONGOING,
			R.string.notificationOngoingTitle,
			R.string.notificationOngoingTextUnknown,
			applicationContext.getColor( R.color.statusDead )
		)

		Notify.showNotification( applicationContext, alwaysOngoingNotification, Notify.IDENTIFIER_ALWAYS_ONGOING )
		Log.d( Shared.logTag, "Created/updated always ongoing notification (${ Notify.IDENTIFIER_ALWAYS_ONGOING })" )

		return ForegroundInfo( alwaysOngoingNotification, Notify.IDENTIFIER_ALWAYS_ONGOING )
	}
	*/

	private fun createSuccessData( serverCount: Int ) = Data.Builder().putInt( SUCCESS_SERVERS_COUNT, serverCount ).build()
	private fun createFailureData( reason: Int ) = Data.Builder().putInt( FAILURE_REASON, reason ).build()

}
