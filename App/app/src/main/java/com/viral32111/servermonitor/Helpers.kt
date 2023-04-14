package com.viral32111.servermonitor

import android.app.Activity
import android.content.Context
import android.text.Html
import android.util.Log
import android.widget.TextView
import androidx.appcompat.app.AlertDialog
import androidx.constraintlayout.widget.ConstraintLayout
import com.android.volley.Request
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.google.android.material.snackbar.Snackbar
import kotlin.math.roundToInt
import kotlin.math.roundToLong

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

// Gets the color appropriate for a given value - neutral if thresholds not provided, or dead if invalid value
fun Float?.getAppropriateColor( warningThreshold: Float? = null, dangerThreshold: Float? = null ): Int =
	if ( this == null || this.compareTo( 0.0 ) < 0 ) R.color.statusDead
	else if ( warningThreshold == null || dangerThreshold == null ) R.color.statusNeutral
	else if ( this >= dangerThreshold ) R.color.statusBad
	else if ( this >= warningThreshold ) R.color.statusWarning
	else R.color.statusGood
fun Double?.getAppropriateColor( warningThreshold: Double? = null, dangerThreshold: Double? = null ): Int =
	if ( this == null || this.compareTo( 0.0 ) < 0 ) R.color.statusDead
	else if ( warningThreshold == null || dangerThreshold == null ) R.color.statusNeutral
	else if ( this >= dangerThreshold ) R.color.statusBad
	else if ( this >= warningThreshold ) R.color.statusWarning
	else R.color.statusGood
fun Int?.getAppropriateColor( warningThreshold: Int? = null, dangerThreshold: Int? = null ): Int =
	if ( this == null || this.compareTo( 0.0 ) < 0 ) R.color.statusDead
	else if ( warningThreshold == null || dangerThreshold == null ) R.color.statusNeutral
	else if ( this >= dangerThreshold ) R.color.statusBad
	else if ( this >= warningThreshold ) R.color.statusWarning
	else R.color.statusGood
fun Long?.getAppropriateColor( warningThreshold: Long? = null, dangerThreshold: Long? = null ): Int =
	if ( this == null || this.compareTo( 0.0 ) < 0 ) R.color.statusDead
	else if ( warningThreshold == null || dangerThreshold == null ) R.color.statusNeutral
	else if ( this >= dangerThreshold ) R.color.statusBad
	else if ( this >= warningThreshold ) R.color.statusWarning
	else R.color.statusGood

// Same as above but in reverse and only for integers - used for drive S.M.A.R.T health
fun Int?.getAppropriateColorReverse( warningThreshold: Int? = null, dangerThreshold: Int? = null ): Int =
	if ( this == null || this.compareTo( 0.0 ) < 0 ) R.color.statusDead
	else if ( warningThreshold == null || dangerThreshold == null ) R.color.statusNeutral
	else if ( this <= dangerThreshold ) R.color.statusBad
	else if ( this <= warningThreshold ) R.color.statusWarning
	else R.color.statusGood

// Creates a HTML spannable tag with color styling - https://stackoverflow.com/a/41655900
fun Context.createHTMLColoredText( text: String, color: Int ) = String.format( "<span style=\"color: #%s\">%s</span>", this.getColor( color ).toString( 16 ), text )

// Wraps a string in bold/italic HTML tags
fun String.asHTMLBold() = this.prefixWith( "<strong>" ).suffixWith( "</strong>" )
fun String.asHTMLItalic() = this.prefixWith( "<em>" ).suffixWith( "</em>" )

// Sets the color of a text view's content & drawable
fun TextView.setTextIconColor( color: Int ) {
	this.setTextColor( color )
	this.compoundDrawables[ 0 ].setTint( color )
}

// Updates a text view's content using HTML - https://stackoverflow.com/a/37899914
fun TextView.setTextFromHTML( html: String ) {
	this.text = Html.fromHtml( html, Html.FROM_HTML_MODE_LEGACY )
}

// Generates a random number in a range
fun generateRandomInteger( min: Int, max: Int ): Int = ( ( Math.random() * ( max - min ) ) + min ).roundToInt()

// Rounds & clamps a float/double to an int/long respectively
fun Float.atLeastRoundInt( minimum: Int ) = this.roundToInt().coerceAtLeast( minimum )
fun Double.atLeastRoundLong( minimum: Long ) = this.roundToLong().coerceAtLeast( minimum )

// Rounds a float/double to a given decimal place and returns as a string
fun Float.roundAsString( decimals: Int ) = String.format( "%.${ decimals }f", this )
fun Double.roundAsString( decimals: Int ) = String.format( "%.${ decimals }f", this )

// Clamps a float/double at a minimum and rounds it using the function above
fun Float.atLeastRoundAsString( minimum: Float, decimals: Int ) = this.coerceAtLeast( minimum ).roundAsString( decimals )
fun Double.atLeastRoundAsString( minimum: Double, decimals: Int ) = this.coerceAtLeast( minimum ).roundAsString( decimals )

// Concatenate strings
fun String.concat( string: String ) = this + string
fun String.suffixWith( string: String ) = this.concat( string )
fun String.prefixWith( string: String ) = string.concat( this )

// Converts a fixed-length array to a list
fun <T> Array<T>.toArrayList(): ArrayList<T> = arrayListOf<T>().also { it.addAll( this ) }
