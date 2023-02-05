package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import androidx.appcompat.app.ActionBar
import com.google.android.material.appbar.MaterialToolbar

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
		val instanceUrlEditText = findViewById<EditText>( R.id.settingsInstanceUrlEditText )
		val credentialsUsernameEditText = findViewById<EditText>( R.id.setupCredentialsUsernameEditText )
		val credentialsPasswordEditText = findViewById<EditText>( R.id.setupCredentialsPasswordEditText )
		val continueButton = findViewById<Button>( R.id.settingsSaveButton )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.setupActionBarTitle )
		materialToolbar?.isTitleCentered = true

		// Get the persistent settings - https://developer.android.com/training/data-storage/shared-preferences
		val sharedPreferences = getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE )

		// When the the continue button is pressed...
		continueButton.setOnClickListener {

			// Get the values in the text inputs
			val instanceUrl = instanceUrlEditText.text.toString()
			val credentialsUsername = credentialsUsernameEditText.text.toString()
			val credentialsPassword = credentialsPasswordEditText.text.toString()

			// Do not continue if an instance URL wasn't provided
			if ( instanceUrl.isEmpty() ) {
				showBriefMessage( this, R.string.setupToastInstanceUrlEmpty )
				return@setOnClickListener
			}

			// Do not continue if the URL isn't valid
			if ( !validateInstanceUrl( instanceUrl ) ) {
				showBriefMessage( this, R.string.setupToastInstanceUrlInvalid )
				return@setOnClickListener
			}

			// Do not continue if a username wasn't provided
			if ( credentialsUsername.isEmpty() ) {
				showBriefMessage( this, R.string.setupToastCredentialsUsernameEmpty )
				return@setOnClickListener
			}

			// Do not continue if the username isn't valid
			if ( !validateCredentialsUsername( credentialsUsername ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsUsernameInvalid )
				return@setOnClickListener
			}

			// Do not continue if a password wasn't provided
			if ( credentialsPassword.isEmpty() ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordEmpty )
				return@setOnClickListener
			}

			// Do not continue if the password isn't valid
			if ( !validateCredentialsPassword( credentialsPassword ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordInvalid )
				return@setOnClickListener
			}

			if ( !testInstance( instanceUrl ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordInvalid )
				return@setOnClickListener
			}

			// Save values to shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
			with ( sharedPreferences.edit() ) {
				putString( "instanceUrl", instanceUrl )
				putString( "credentialsUsername", credentialsUsername )
				putString( "credentialsPassword", credentialsPassword )
				apply()
			}

			// Change to the next activity, whatever it may be
			Log.d( Shared.logTag, "Setup is complete! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
			switchActivity()

		}

		// Open settings when its action bar menu item is clicked
		materialToolbar?.setOnMenuItemClickListener { menuItem ->
			if ( menuItem.title?.equals( getString( R.string.action_bar_menu_settings ) ) == true ) {
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			}

			return@setOnMenuItemClickListener true
		}

		// Change to the next activity if we have already gone through the setup
		val instanceUrl = sharedPreferences.getString( "instanceUrl", null )
		val credentialsUsername = sharedPreferences.getString( "credentialsUsername", null )
		val credentialsPassword = sharedPreferences.getString( "credentialsPassword", null )
		if ( !instanceUrl.isNullOrEmpty() && !credentialsUsername.isNullOrEmpty() && !credentialsPassword.isNullOrEmpty() ) {
			Log.d( Shared.logTag, "We're already setup! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
			switchActivity()
		} else {
			Log.d( Shared.logTag, "We're not setup yet! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
		}

	}

	// TODO: Attempt connection to URL and validate the connection point service is running on it
	private fun testInstance( instanceUrl: String ): Boolean {
		return true
	}

	// TODO: Is there multiple servers? If so, switch to Servers activity. If not, switch to Server activity.
	private fun switchActivity() {
		startActivity( Intent( this, ServersActivity::class.java ) )
		overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
		finish()
	}

}
