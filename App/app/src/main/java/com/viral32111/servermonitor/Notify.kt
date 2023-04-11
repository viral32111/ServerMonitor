package com.viral32111.servermonitor

import android.app.*
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.util.Log
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat

class Notify {

	// Our notification channels - each feature should have its own so the user can adjust preferences in system settings
	enum class Channel( val identifier: String ) {
		TEST( "TEST" ),
		ONGOING( "ONGOING" )
	}

	companion object {
		private lateinit var notificationManager: NotificationManager

		// Should be called on app startup to setup this class
		fun initialise( activity: AppCompatActivity, context: Context ) {

			// Get the system notification manager
			notificationManager = context.getSystemService( Context.NOTIFICATION_SERVICE ) as NotificationManager

			// Remove all our existing notifications (mostly so we can later resend the always ongoing one)
			notificationManager.cancelAll()

			// Register our notification channels
			notificationManager.createNotificationChannel( NotificationChannel( Channel.TEST.identifier, "Test", NotificationManager.IMPORTANCE_DEFAULT ).apply {
				description = "Test notifications."
			} )
			notificationManager.createNotificationChannel( NotificationChannel( Channel.ONGOING.identifier, "Always Ongoing", NotificationManager.IMPORTANCE_LOW ).apply {
				description = "Persistent notification to report overall status."
			} )

			// Request notification permission if this is Android 13
			if ( Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission( context, android.Manifest.permission.POST_NOTIFICATIONS ) != PackageManager.PERMISSION_GRANTED ) {
				activity.registerForActivityResult( ActivityResultContracts.RequestPermission() ) { isGranted ->
					Log.d( Shared.logTag, "Result of requesting post notifications permission: $isGranted" )
				}.launch( android.Manifest.permission.POST_NOTIFICATIONS )
			}

		}

		// Creates a simple notification using the builder that opens an activity when pressed
		fun createTextNotification( context: Context, intent: Intent, channel: Channel, titleId: Int, textId: Int ) = Notification.Builder( context, channel.identifier )
			.setSmallIcon( R.drawable.bolt )
			.setContentTitle( context.getString( titleId ) )
			.setContentText( context.getString( textId ) )
			.setContentIntent( PendingIntent.getActivity( context, 0, intent, PendingIntent.FLAG_IMMUTABLE ) )
			.setAutoCancel( true )
			.build()

		fun createProgressNotification( context: Context, intent: Intent, channel: Channel, titleId: Int, textId: Int ) = Notification.Builder( context, channel.identifier )
			.setSmallIcon( R.drawable.bolt )
			.setContentTitle( context.getString( titleId ) )
			.setContentText( context.getString( textId ) )
			.setContentIntent( PendingIntent.getActivity( context, 0, intent, PendingIntent.FLAG_IMMUTABLE ) )
			.setAutoCancel( true )
			.setProgress( 100, 50, true ) // Just for testing
			.setOngoing( true )
			.build()

		// Sends a created notification to the device, returns the notification identifier (if any) for future updates/removal
		fun sendNotification( context: Context, notification: Notification ): Int? {

			// Do not continue if notifications are disabled
			if ( !notificationManager.areNotificationsEnabled() ) return null

			// Show the notification if this is Android 12 or below (we do not need to request permission)
			if ( Build.VERSION.SDK_INT < 33 ) return showNotification( notification )

			// Only show the notification if we have been granted the permission (as this is Android 13)
			if ( ContextCompat.checkSelfPermission( context, android.Manifest.permission.POST_NOTIFICATIONS ) == PackageManager.PERMISSION_GRANTED ) return showNotification( notification )

			// Something went wrong, notification wasn't shown
			return null

		}

		// Generates a random notification identifier & shows the given notification
		private fun showNotification( notification: Notification ): Int {
			val notificationId = generateRandomInteger( 1, 100 )
			notificationManager.notify( notificationId, notification )
			return notificationId
		}
	}

}
