package com.viral32111.servermonitor

import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
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

			// For debugging!
			showBriefMessage( "Everything is good!" )

		}

	}

	// https://developer.android.com/develop/ui/views/notifications/snackbar/showing
	private fun showBriefMessage( stringId: Int ) {
		Snackbar.make( findViewById( R.id.setupConstraintLayout ), stringId, Snackbar.LENGTH_SHORT ).show()
	}

	private fun showBriefMessage( message: String ) {
		Snackbar.make( findViewById( R.id.setupConstraintLayout ), message, Snackbar.LENGTH_SHORT ).show()
	}

}
