package com.viral32111.servermonitor

import android.app.Activity
import android.content.Context
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

/**
 * Creates a modern Material 3 information dialog.
 * @param activity The current activity.
 * @param titleId The resource to use as the title of the dialog.
 * @param messageId The resource to use as the message in the dialog.
 */
fun showInformationDialog(
	activity: Activity,
	titleId: Int,
	messageId: Int
): AlertDialog =
	MaterialAlertDialogBuilder( activity )
		.setTitle( titleId )
		.setMessage( messageId )
		.setPositiveButton( R.string.dialogInformationPositive ) { _, _ ->
			Log.d( Shared.logTag, "Information dialog acknowledged" )
		}
		.show()

/**
 * Shows a modern Material 3 information dialog.
 * @param activity The current activity.
 * @param titleId The resource to use as the title of the dialog.
 * @param message The message to show in the dialog.
 */
fun showInformationDialog(
	activity: Activity,
	titleId: Int,
	message: String
): AlertDialog =
	MaterialAlertDialogBuilder( activity )
		.setTitle( titleId )
		.setMessage( message )
		.setPositiveButton( R.string.dialogInformationPositive ) { _, _ ->
			Log.d( Shared.logTag, "Information dialog acknowledged" )
		}
		.show()

// Gets the color for a given value, fallback to offline/dead
fun colorForValue( context: Context, value: Float?, warnThreshold: Float, badThreshold: Float ) =
	if ( value == null || value < 0.0f ) context.getColor( R.color.statusDead )
	else if ( value >= badThreshold ) context.getColor( R.color.statusBad )
	else if ( value >= warnThreshold ) context.getColor( R.color.statusWarning )
	else context.getColor( R.color.statusGood )
fun colorForValue( context: Context, value: Long?, warnThreshold: Long, badThreshold: Long ) =
	if ( value == null || value < 0L ) context.getColor( R.color.statusDead )
	else if ( value >= badThreshold ) context.getColor( R.color.statusBad )
	else if ( value >= warnThreshold ) context.getColor( R.color.statusWarning )
	else context.getColor( R.color.statusGood )
fun colorForValue( context: Context, value: Double?, warnThreshold: Double, badThreshold: Double ) =
	if ( value == null || value < 0.0 ) context.getColor( R.color.statusDead )
	else if ( value >= badThreshold ) context.getColor( R.color.statusBad )
	else if ( value >= warnThreshold ) context.getColor( R.color.statusWarning )
	else context.getColor( R.color.statusGood )
fun colorForValue( context: Context, value: Int?, warnThreshold: Int, badThreshold: Int ) =
	if ( value == null || value < 0 ) context.getColor( R.color.statusDead )
	else if ( value >= badThreshold ) context.getColor( R.color.statusBad )
	else if ( value >= warnThreshold ) context.getColor( R.color.statusWarning )
	else context.getColor( R.color.statusGood )
fun colorForValueReverse( context: Context, value: Int?, warnThreshold: Int, badThreshold: Int ) =
	if ( value == null || value < 0 ) context.getColor( R.color.statusDead )
	else if ( value <= badThreshold ) context.getColor( R.color.statusBad )
	else if ( value <= warnThreshold ) context.getColor( R.color.statusWarning )
	else context.getColor( R.color.statusGood )

// Returns neutral color for value, or fallback to offline/dead
fun colorAsNeutral( context: Context, value: Float? ) =
	if ( value == null || value < 0.0 ) context.getColor( R.color.statusDead ) else context.getColor( R.color.statusNeutral )
fun colorAsNeutral( context: Context, value: Double? ) =
	if ( value == null || value < 0.0 ) context.getColor( R.color.statusDead ) else context.getColor( R.color.statusNeutral )
fun colorAsNeutral( context: Context, value: Long? ) =
	if ( value == null || value < 0.0 ) context.getColor( R.color.statusDead ) else context.getColor( R.color.statusNeutral )

// Rounds a given value if it is valid, fallback to default text - Suffix is not included in string format so that percentage symbols can be used
fun roundValueOrDefault( value: Float?, suffix: String = "" ) =
	( if ( value == null || value <= 0.0f ) "0" else String.format( "%.1f", value ) ) + suffix
fun roundValueOrDefault( value: Double?, suffix: String = "" ) =
	( if ( value == null || value <= 0.0 ) "0" else String.format( "%.1f", value ) ) + suffix

// Creates a HTML spannable tag with color styling - https://stackoverflow.com/a/41655900
fun createColorText( text: String, color: Int ) =
	String.format( "<span style=\"color: #${ color.toString( 16 ) }\">${ text }</span>" )
