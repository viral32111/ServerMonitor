package com.viral32111.servermonitor

import android.content.Context
import android.content.SharedPreferences
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import android.widget.Spinner
import androidx.appcompat.app.ActionBar
import androidx.appcompat.content.res.AppCompatResources
import com.google.android.material.appbar.MaterialToolbar
import com.google.android.material.materialswitch.MaterialSwitch

class SettingsActivity : AppCompatActivity() {

	// Will hold all the UI
	private lateinit var instanceUrlEditText: EditText
	private lateinit var credentialsUsernameEditText: EditText
	private lateinit var credentialsPasswordEditText: EditText
	private lateinit var automaticRefreshSwitch: MaterialSwitch
	private lateinit var automaticRefreshIntervalEditText: EditText
	private lateinit var themeSpinner: Spinner
	private lateinit var notificationsAlwaysOngoingSwitch: MaterialSwitch
	private lateinit var notificationsWhenIssueArisesSwitch: MaterialSwitch

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_settings )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )

		// Get all the UI controls
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		val saveButton = findViewById<Button>( R.id.settingsSaveButton )
		instanceUrlEditText = findViewById( R.id.settingsInstanceUrlEditText )
		credentialsUsernameEditText = findViewById( R.id.settingsCredentialsUsernameEditText )
		credentialsPasswordEditText = findViewById( R.id.settingsCredentialsPasswordEditText )
		automaticRefreshSwitch = findViewById( R.id.settingsAutomaticRefreshSwitch )
		automaticRefreshIntervalEditText = findViewById( R.id.settingsAutomaticRefreshIntervalEditText )
		themeSpinner = findViewById( R.id.settingsThemeSpinner )
		notificationsAlwaysOngoingSwitch = findViewById( R.id.settingsNotificationsAlwaysOngoingSwitch )
		notificationsWhenIssueArisesSwitch = findViewById( R.id.settingsNotificationsWhenIssueArisesSwitch )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.settingsActionBarTitle )
		materialToolbar?.isTitleCentered = true

		// Enable the back button on the toolbar
		materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this, R.drawable.ic_baseline_arrow_back_24 )
		materialToolbar?.setNavigationOnClickListener {
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
		}

		// Disable the menu on the toolbar
		materialToolbar?.menu?.clear()

		// Get the persistent settings - https://developer.android.com/training/data-storage/shared-preferences
		val sharedPreferences = getSharedPreferences( "com.viral32111.ServerMonitor.Settings", Context.MODE_PRIVATE )

		// Update UI with settings & save in case it used defaults
		readSettings( sharedPreferences )
		saveSettings( sharedPreferences )

		// Validate & save settings when the save button is pressed
		saveButton.setOnClickListener {
			saveSettings( sharedPreferences ).let {
				if ( it != null ) {
					showBriefMessage( this, it )
					return@setOnClickListener
				}
			}

			// Return to the setup activity
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
		}

		// Disable interval input when automatic refresh is switched off
		automaticRefreshSwitch.setOnCheckedChangeListener { _, isChecked ->
			automaticRefreshIntervalEditText.isEnabled = isChecked
		}

	}

	// Updates the UI with the values from the persistent settings
	private fun readSettings( sharedPreferences: SharedPreferences ) {

		// Get stored settings or default to current UI - https://developer.android.com/training/data-storage/shared-preferences#ReadSharedPreference
		val instanceUrl = sharedPreferences.getString( "instanceUrl", instanceUrlEditText.text.toString() )
		val credentialsUsername = sharedPreferences.getString( "credentialsUsername", credentialsUsernameEditText.text.toString() )
		val credentialsPassword = sharedPreferences.getString( "credentialsPassword", credentialsPasswordEditText.text.toString() )
		val automaticRefresh = sharedPreferences.getBoolean( "automaticRefresh", automaticRefreshSwitch.isChecked )
		val automaticRefreshInterval = sharedPreferences.getInt( "automaticRefreshInterval", 15 ) // Default to 15 seconds
		//val theme = sharedPreferences.getString( "theme", themeSpinner.selectedItem.toString() )
		val notificationAlwaysOngoing = sharedPreferences.getBoolean( "notificationAlwaysOngoing", notificationsAlwaysOngoingSwitch.isChecked )
		val notificationWhenIssueArises = sharedPreferences.getBoolean( "notificationWhenIssueArises", notificationsWhenIssueArisesSwitch.isChecked )

		// Update the UI
		automaticRefreshSwitch.isChecked = automaticRefresh
		automaticRefreshIntervalEditText.setText( automaticRefreshInterval.toString() )
		//themeSpinner.setSelection( 0 )
		notificationsAlwaysOngoingSwitch.isChecked = notificationAlwaysOngoing
		notificationsWhenIssueArisesSwitch.isChecked = notificationWhenIssueArises

		// Enable instance URL & credentials if setup is finished
		if ( !instanceUrl.isNullOrEmpty() ) {
			instanceUrlEditText.setText( instanceUrl )
			instanceUrlEditText.hint = getString( R.string.settingsEditTextInstanceUrlHint )
			instanceUrlEditText.isEnabled = true
		}
		if ( !credentialsUsername.isNullOrEmpty() ) {
			credentialsUsernameEditText.setText( credentialsUsername )
			credentialsUsernameEditText.hint = getString( R.string.settingsEditTextCredentialsUsernameHint )
			credentialsUsernameEditText.isEnabled = true
		}
		if ( !credentialsPassword.isNullOrEmpty() ) {
			credentialsPasswordEditText.setText( credentialsPassword )
			credentialsPasswordEditText.hint = getString( R.string.settingsEditTextCredentialsPasswordHint )
			credentialsPasswordEditText.isEnabled = true
		}

		// Disable interval input when automatic refresh is switched off
		automaticRefreshIntervalEditText.isEnabled = automaticRefreshSwitch.isChecked

	}

	// Saves the values in the UI to the persistent settings
	private fun saveSettings( sharedPreferences: SharedPreferences ): Int? {

		// Get the values from all the UI inputs
		val instanceUrl = instanceUrlEditText.text.toString()
		val credentialsUsername = credentialsUsernameEditText.text.toString()
		val credentialsPassword = credentialsPasswordEditText.text.toString()
		val automaticRefresh = automaticRefreshSwitch.isChecked
		val automaticRefreshInterval = automaticRefreshIntervalEditText.text.toString().toInt()
		//val theme = themeSpinner.selectedItem.toString()
		val notificationAlwaysOngoing = notificationsAlwaysOngoingSwitch.isChecked
		val notificationWhenIssueArises = notificationsWhenIssueArisesSwitch.isChecked

		// TODO: Validate all those values

		// Save those values to shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
		with ( sharedPreferences.edit() ) {
			putString( "instanceUrl", instanceUrl )
			putString( "credentialsUsername", credentialsUsername )
			putString( "credentialsPassword", credentialsPassword )
			putBoolean( "automaticRefresh", automaticRefresh )
			putInt( "automaticRefreshInterval", automaticRefreshInterval )
			//putString( "theme", theme )
			putBoolean( "notificationAlwaysOngoing", notificationAlwaysOngoing )
			putBoolean( "notificationWhenIssueArises", notificationWhenIssueArises )
			apply()
		}

		// All was good, so no string resource ID
		return null

	}

}
