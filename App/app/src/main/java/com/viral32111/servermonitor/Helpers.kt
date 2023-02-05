package com.viral32111.servermonitor

import android.app.Activity
import androidx.constraintlayout.widget.ConstraintLayout
import com.google.android.material.snackbar.Snackbar

// Shows a toast popup at the bottom of the activity
// https://developer.android.com/develop/ui/views/notifications/snackbar/showing
fun showBriefMessage( activity: Activity, stringId: Int ) {
	Snackbar.make( activity.findViewById<ConstraintLayout>( android.R.id.content ), stringId, Snackbar.LENGTH_SHORT ).show()
}
