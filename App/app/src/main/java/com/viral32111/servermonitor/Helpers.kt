package com.viral32111.servermonitor

import android.app.Activity
import androidx.constraintlayout.widget.ConstraintLayout
import com.android.volley.Request
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
