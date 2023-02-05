package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
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
		val authenticationUsernameEditText = findViewById<EditText>( R.id.setupCredentialsUsernameEditText )
		val authenticationPasswordEditText = findViewById<EditText>( R.id.setupCredentialsPasswordEditText )
		val continueButton = findViewById<Button>( R.id.settingsSaveButton )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.setupActionBarTitle )
		materialToolbar?.isTitleCentered = true

		// When the the continue button is pressed...
		continueButton.setOnClickListener {

			// Get the values in the text inputs
			val instanceUrl = instanceUrlEditText.text.toString()
			val authUsername = authenticationUsernameEditText.text.toString()
			val authPassword = authenticationPasswordEditText.text.toString()

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
			if ( authUsername.isEmpty() ) {
				showBriefMessage( this, R.string.setupToastCredentialsUsernameEmpty )
				return@setOnClickListener
			}

			// Do not continue if the username isn't valid
			if ( !validateCredentialsUsername( authUsername ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsUsernameInvalid )
				return@setOnClickListener
			}

			// Do not continue if a password wasn't provided
			if ( authPassword.isEmpty() ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordEmpty )
				return@setOnClickListener
			}

			// Do not continue if the password isn't valid
			if ( !validateCredentialsPassword( authPassword ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordInvalid )
				return@setOnClickListener
			}

			// TODO: Attempt connection to URL and validate the connection point service is running on it

			// Save values to shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
			val sharedPreferences = getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE )
			with ( sharedPreferences.edit() ) {
				putString( "instanceUrl", instanceUrl )
				putString( "credentialsUsername", authUsername )
				putString( "credentialsPassword", authPassword )
				apply()
			}

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

}
