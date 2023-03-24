package com.viral32111.servermonitor

import android.app.Activity
import android.util.Log
import androidx.appcompat.app.AlertDialog
import androidx.constraintlayout.widget.ConstraintLayout
import com.android.volley.Request
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.google.android.material.snackbar.Snackbar

// Shows a toast popup at the bottom of the activity
// https://developer.android.com/develop/ui/views/notifications/snackbar/showing
fun showBriefMessage( activity: Activity, stringId: Int ) {
	Snackbar.make( activity.findViewById<ConstraintLayout>( android.R.id.content ), stringId, Snackbar.LENGTH_SHORT ).show()
}

// Converts a Volley HTTP request method to its name, used in logging
fun requestMethodToName( method: Int ): String {
	return when ( method ) {
		Request.Method.GET -> "GET"
		Request.Method.POST -> "POST"
		Request.Method.PUT -> "PUT"
		Request.Method.DELETE -> "DELETE"
		else -> method.toString()
	}
}

// Creates a popup with a progress spinner that can be cancelled - https://stackoverflow.com/a/14834802
fun createProgressDialog( activity: Activity, titleId: Int, messageId: Int, cancelCallback: () -> Unit ): AlertDialog {
	val dialogProgress = activity.layoutInflater.inflate( R.layout.dialog_progress, null )

	return MaterialAlertDialogBuilder( activity )
		.setTitle( titleId )
		.setMessage( messageId )
		.setView( dialogProgress )
		.setNegativeButton( R.string.dialogProgressNegativeButton ) { _, _ ->
			Log.d( Shared.logTag, "Progress dialog cancelled via negative button" )
			cancelCallback.invoke()
		}
		.setOnCancelListener {
			Log.d( Shared.logTag, "Progress dialog cancelled via dismiss/system back button" )
			cancelCallback.invoke()
		}
		.setOnDismissListener {
			Log.d( Shared.logTag, "Progress dialog dismissed" )
		}
		.create()

}

/**
 * Creates a modern Material 3 confirmation dialog.
 * @param activity The current activity.
 * @param messageId The resource to use as the message in the dialog.
 * @param positiveCallback The callback to execute when the dialog is confirmed.
 * @param negativeCallback The callback to execute when the dialog is aborted.
 */
fun showConfirmDialog(
	activity: Activity,
	messageId: Int,
	positiveCallback: () -> Unit,
	negativeCallback: () -> Unit
): AlertDialog =
	MaterialAlertDialogBuilder( activity )
		.setTitle( R.string.dialogConfirmTitle )
		.setMessage( messageId )
		.setPositiveButton( R.string.dialogConfirmPositiveButton ) { _, _ ->
			Log.d( Shared.logTag, "Confirmation dialog agreed" )
			positiveCallback()
		}
		.setNegativeButton( R.string.dialogConfirmNegativeButton ) { _, _ ->
			Log.d( Shared.logTag, "Confirmation dialog declined" )
			negativeCallback()
		}
		.setCancelable( false )
		.show()
