package com.viral32111.servermonitor.helper

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.util.Log
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.Shared

class Notify {

	companion object {

		// Our notification channels - each feature should have its own so the user can adjust preferences in system settings
		const val CHANNEL_TEST = "TEST"
		const val CHANNEL_ALWAYS_ONGOING = "ONGOING"
		const val CHANNEL_WHEN_ISSUE_ARISES = "ISSUE"

		// Can be anything, so long as it is not in the range below
		const val IDENTIFIER_ALWAYS_ONGOING = 123

		// Random range for generating notification IDs
		private const val IDENTIFIER_RANDOM_MIN = 1
		private const val IDENTIFIER_RANDOM_MAX = 100

		private lateinit var notificationManager: NotificationManager

		// Should be called on app startup to setup this class
		fun initialise( activity: AppCompatActivity, context: Context ) {

			// Get the system notification manager
			notificationManager = context.getSystemService( Context.NOTIFICATION_SERVICE ) as NotificationManager

			// Remove all our existing notifications (mostly so we can later resend the always ongoing one)
			notificationManager.cancelAll()

			// Register our notification channels
			notificationManager.createNotificationChannel( NotificationChannel( CHANNEL_TEST, context.getString( R.string.notificationTestName ), NotificationManager.IMPORTANCE_DEFAULT ).apply {
				description = context.getString( R.string.notificationTestDescription )
			} )
			notificationManager.createNotificationChannel( NotificationChannel( CHANNEL_ALWAYS_ONGOING, context.getString( R.string.notificationOngoingName ), NotificationManager.IMPORTANCE_LOW ).apply {
				description = context.getString( R.string.notificationOngoingDescription )
			} )
			notificationManager.createNotificationChannel( NotificationChannel( CHANNEL_WHEN_ISSUE_ARISES, context.getString( R.string.notificationIssueName ), NotificationManager.IMPORTANCE_HIGH ).apply {
				description = context.getString( R.string.notificationIssueDescription )
			} )

			// Request notification permission if this is Android 13
			if ( Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission( context, android.Manifest.permission.POST_NOTIFICATIONS ) != PackageManager.PERMISSION_GRANTED ) {
				activity.registerForActivityResult( ActivityResultContracts.RequestPermission() ) { isGranted ->
					Log.d( Shared.logTag, "Post notifications permission ${ if ( isGranted ) "granted" else "denied" }" )
				}.launch( android.Manifest.permission.POST_NOTIFICATIONS )
			}

		}

		// Creates a simple notification using the builder that opens an activity when pressed
		fun createTextNotification( context: Context, intent: Intent, channel: String, titleId: Int, textId: Int, color: Int, timestamp: Long = System.currentTimeMillis() ) = Notification.Builder( context, channel )
			.setSmallIcon( R.drawable.monitor_heart )
			.setContentTitle( context.getString( titleId ) )
			.setContentText( context.getString( textId ) )
			.setContentIntent( PendingIntent.getActivity( context, 0, intent, PendingIntent.FLAG_IMMUTABLE ) )
			.setAutoCancel( true )
			.setColor( color )
			.setShowWhen( true )
			.setWhen( timestamp )
			.build()

		fun createProgressNotification( context: Context, intent: Intent, channel: String, titleId: Int, textId: Int, color: Int, timestamp: Long = System.currentTimeMillis(), isOngoing: Boolean = true ) = Notification.Builder( context, channel )
			.setSmallIcon( R.drawable.monitor_heart )
			.setTicker( context.getString( titleId ) )
			.setContentTitle( context.getString( titleId ) )
			.setContentText( context.getString( textId ) )
			.setContentIntent( PendingIntent.getActivity( context, 0, intent, PendingIntent.FLAG_IMMUTABLE ) )
			.setAutoCancel( false ) // Do not remove when pressed
			.setProgress( 100, 0, true ) // TODO: Dynamically update progress
			.setOngoing( isOngoing )
			.setColor( color )
			.setShowWhen( true )
			.setWhen( timestamp )
			.build()

		// Shows a notification
		fun showNotification( context: Context, notification: Notification, identifier: Int = generateRandomInteger( IDENTIFIER_RANDOM_MIN, IDENTIFIER_RANDOM_MAX ) ) {

			// Do not continue if notifications are disabled
			if ( !notificationManager.areNotificationsEnabled() ) return

			// Show the notification if this is Android 12 or below (we do not need to request permission)
			if ( Build.VERSION.SDK_INT < 33 ) {
				notificationManager.notify( identifier, notification )
				return
			}

			// Only show the notification if we have been granted the permission (as this is Android 13)
			if ( ContextCompat.checkSelfPermission( context, android.Manifest.permission.POST_NOTIFICATIONS ) == PackageManager.PERMISSION_GRANTED ) {
				notificationManager.notify( identifier, notification )
				return
			}

		}

		/*
		// Removes a notification
		fun clearNotification( notificationIdentifier: Int ) = notificationManager.cancel( notificationIdentifier )

		// Finds an active notification
		fun findActiveNotification( notificationIdentifier: Int ): StatusBarNotification? {
			for ( notification in notificationManager.activeNotifications ) Log.d( Shared.logTag, "Notification: ${ notification.id }" ) // TODO: Debugging

			return notificationManager.activeNotifications.find { notification -> notification.id == notificationIdentifier }
		}
		*/

	}

}
