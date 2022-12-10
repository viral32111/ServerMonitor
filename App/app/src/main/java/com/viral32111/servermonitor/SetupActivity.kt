package com.viral32111.servermonitor

import android.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.view.Menu
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import android.widget.Toolbar
import androidx.appcompat.app.ActionBar
import com.google.android.material.snackbar.Snackbar

class SetupActivity : AppCompatActivity() {

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_setup )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )

		// Get all UI controls
		val editTextInstanceUrl = findViewById<EditText>( R.id.setupUrlEditText )
		val editTextAuthenticationUsername = findViewById<EditText>( R.id.setupAuthenticationUsernameEditText )
		val editTextAuthenticationPassword = findViewById<EditText>( R.id.setupAuthenticationPasswordEditText )
		val buttonContinue = findViewById<Button>( R.id.setupContinueButton )

		// Register event handler for when the the continue button is pressed...
		buttonContinue.setOnClickListener {

			// Get the values in the text inputs
			val instanceUrl = editTextInstanceUrl.text
			val authUsername = editTextAuthenticationUsername.text
			val authPassword = editTextAuthenticationPassword.text

			// Do not continue if an instance URL wasn't provided
			if ( instanceUrl.isEmpty() ) {
				showBriefMessage( R.string.setupToastInstanceUrlEmpty )
				return@setOnClickListener
			}

			// Do not continue if a username wasn't provided
			if ( authUsername.isEmpty() ) {
				showBriefMessage( R.string.setupToastAuthenticationUsernameEmpty )
				return@setOnClickListener
			}

			// Do not continue if a password wasn't provided
			if ( authPassword.isEmpty() ) {
				showBriefMessage( R.string.setupToastAuthenticationPasswordEmpty )
				return@setOnClickListener
			}

			/*val alertDialogBuilder = AlertDialog.Builder( this )
			alertDialogBuilder.setTitle( "Error" )
			alertDialogBuilder.setMessage( "The URL to an instance must be provided." )
			alertDialogBuilder.setNeutralButton( "OK" ) { _, _ ->
				Toast.makeText( this, "neutral", Toast.LENGTH_SHORT ).show()
			}
			alertDialogBuilder.setOnCancelListener {
				Toast.makeText( this, "cancelled", Toast.LENGTH_SHORT ).show()
			}
			alertDialogBuilder.setOnDismissListener {
				Toast.makeText( this, "dismissed", Toast.LENGTH_SHORT ).show()
			}
			val alertDialog = alertDialogBuilder.create()
			alertDialog.show()*/

			showBriefMessage( "Everything is good!" )

		}

	}

	private fun showBriefMessage( stringId: Int ) {
		Toast.makeText( this, getString( stringId ), Toast.LENGTH_SHORT ).show()
	}

	private fun showBriefMessage( message: String ) {
		Toast.makeText( this, message, Toast.LENGTH_SHORT ).show()
	}

}
