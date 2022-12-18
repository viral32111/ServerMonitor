package com.viral32111.servermonitor

import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import androidx.appcompat.app.ActionBar
import com.google.android.material.appbar.MaterialToolbar
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
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		val editTextInstanceUrl = findViewById<EditText>( R.id.settingsInstanceUrlEditText )
		val editTextAuthenticationUsername = findViewById<EditText>( R.id.setupAuthenticationUsernameEditText )
		val editTextAuthenticationPassword = findViewById<EditText>( R.id.setupAuthenticationPasswordEditText )
		val buttonContinue = findViewById<Button>( R.id.settingsSaveButton )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.setup_action_bar_title )
		materialToolbar?.isTitleCentered = true

		// When the the continue button is pressed...
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

			// TODO: Check if URL is valid
			// TODO: Check if username is valid (length & character requirements)
			// TODO: Check if password is valid (strength, length & character requirements)

			// TODO: Attempt connection to URL and validate the connection point service is running on it

			// TODO: Save values to shared preferences

			// For debugging!
			showBriefMessage( "Everything is good!" )

		}

		// When a menu item on the action bar is pressed...
		materialToolbar?.setOnMenuItemClickListener { menuItem ->

			// Settings
			if ( menuItem.title?.equals( getString( R.string.action_bar_menu_settings ) ) == true ) {
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			}

			// Always successful
			return@setOnMenuItemClickListener true

		}

	}

	// https://developer.android.com/develop/ui/views/notifications/snackbar/showing
	// TODO: Move this to a shared class
	private fun showBriefMessage( stringId: Int ) {
		Snackbar.make( findViewById( R.id.setupConstraintLayout ), stringId, Snackbar.LENGTH_SHORT ).show()
	}

	// TODO: This shouldn't be used in the final build, always use string IDs
	private fun showBriefMessage( message: String ) {
		Snackbar.make( findViewById( R.id.setupConstraintLayout ), message, Snackbar.LENGTH_SHORT ).show()
	}

}
