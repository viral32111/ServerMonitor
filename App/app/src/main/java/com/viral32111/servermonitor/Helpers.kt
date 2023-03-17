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
