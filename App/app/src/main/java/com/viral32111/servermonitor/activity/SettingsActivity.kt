package com.viral32111.servermonitor.activity

import android.os.Bundle
import android.util.Log
import android.widget.AutoCompleteTextView
import android.widget.Button
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.content.res.AppCompatResources
import androidx.core.widget.doOnTextChanged
import androidx.work.WorkManager
import com.android.volley.AuthFailureError
import com.android.volley.ClientError
import com.android.volley.NetworkError
import com.android.volley.NoConnectionError
import com.android.volley.ParseError
import com.android.volley.ServerError
import com.android.volley.TimeoutError
import com.google.android.material.appbar.MaterialToolbar
import com.google.android.material.materialswitch.MaterialSwitch
import com.google.android.material.textfield.TextInputEditText
import com.google.android.material.textfield.TextInputLayout
import com.viral32111.servermonitor.ErrorCode
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.Shared
import com.viral32111.servermonitor.UpdateWorker
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.Settings
import com.viral32111.servermonitor.helper.createProgressDialog
import com.viral32111.servermonitor.helper.showBriefMessage
import com.viral32111.servermonitor.helper.showConfirmDialog
import com.viral32111.servermonitor.helper.validateCredentialsPassword
import com.viral32111.servermonitor.helper.validateCredentialsUsername
import com.viral32111.servermonitor.helper.validateInstanceUrl

class SettingsActivity : AppCompatActivity() {

	// UI
	private lateinit var instanceUrlTextInputLayout: TextInputLayout
	private lateinit var instanceUrlEditText: TextInputEditText
	private lateinit var credentialsUsernameInputTextLayout: TextInputLayout
	private lateinit var credentialsUsernameEditText: TextInputEditText
	private lateinit var credentialsPasswordInputTextLayout: TextInputLayout
	private lateinit var credentialsPasswordEditText: TextInputEditText
	private lateinit var automaticRefreshSwitch: MaterialSwitch
	private lateinit var automaticRefreshIntervalTextInputLayout: TextInputLayout
	private lateinit var automaticRefreshIntervalEditText: TextInputEditText
	private lateinit var themeTextInputLayout: TextInputLayout
	private lateinit var themeAutoCompleteTextView: AutoCompleteTextView
	private lateinit var notificationsAlwaysOngoingSwitch: MaterialSwitch
	private lateinit var notificationsWhenIssueArisesSwitch: MaterialSwitch
	private lateinit var saveButton: Button

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_settings )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView(R.layout.action_bar)
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Set the title on the toolbar
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		materialToolbar?.title = getString( R.string.settingsActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Disable the menu on the toolbar
		materialToolbar?.menu?.clear()
		Log.d( Shared.logTag, "Disabled menu items on Material Toolbar" )

		// Get all the UI
		instanceUrlTextInputLayout = findViewById( R.id.settingsInstanceUrlTextInputLayout )
		instanceUrlEditText = findViewById( R.id.settingsInstanceUrlTextInputEditText )
		credentialsUsernameInputTextLayout = findViewById( R.id.settingsCredentialsUsernameTextInputLayout )
		credentialsUsernameEditText = findViewById( R.id.settingsCredentialsUsernameTextInputEditText )
		credentialsPasswordInputTextLayout = findViewById( R.id.settingsCredentialsPasswordTextInputLayout )
		credentialsPasswordEditText = findViewById( R.id.settingsCredentialsPasswordTextInputEditText )
		automaticRefreshSwitch = findViewById( R.id.settingsAutomaticRefreshSwitch )
		automaticRefreshIntervalTextInputLayout = findViewById( R.id.settingsAutomaticRefreshIntervalTextInputLayout )
		automaticRefreshIntervalEditText = findViewById( R.id.settingsAutomaticRefreshIntervalTextInputEditText )
		themeTextInputLayout = findViewById( R.id.settingsThemeTextInputLayout )
		themeAutoCompleteTextView = findViewById( R.id.settingsThemeAutoCompleteTextView )
		notificationsAlwaysOngoingSwitch = findViewById( R.id.settingsNotificationAlwaysOngoingSwitch )
		notificationsWhenIssueArisesSwitch = findViewById( R.id.settingsNotificationWhenIssueArisesSwitch )
		saveButton = findViewById( R.id.settingsSaveButton )
		Log.d( Shared.logTag, "Got UI controls" )

		// Force theme selection by disabling interaction
		// TODO: Remove this once dark theme is implemented
		//themeAutoCompleteTextView.setText( "Light", false )
		themeTextInputLayout.isEnabled = false
		themeAutoCompleteTextView.isEnabled = false
		Log.d( Shared.logTag, "Forced theme selection" )

		// Get settings
		val settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, MODE_PRIVATE ) )
		Log.d( Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Update UI with values from settings
		updateUIWithSettings( settings )

		// Validate & save values when the save button is pressed, then return to the previous activity
		saveButton.setOnClickListener {
			saveSettings( settings ) {
				finish()
				overridePendingTransition(R.anim.slide_in_from_left, R.anim.slide_out_to_right)
			}
		}

		// Disables interval input when automatic refresh is switched off
		automaticRefreshSwitch.setOnCheckedChangeListener { _, isChecked ->
			automaticRefreshIntervalTextInputLayout.isEnabled = isChecked
			automaticRefreshIntervalEditText.isEnabled = isChecked
		}

		// Enable the back button on the toolbar
		materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this,
			R.drawable.arrow_back
		)
		materialToolbar?.setNavigationOnClickListener {
			Log.d( Shared.logTag, "Navigation back button pressed" )
			confirmBack( settings )
		}

		// Register the back button pressed callback - https://medium.com/tech-takeaways/how-to-migrate-the-deprecated-onbackpressed-function-e66bb29fa2fd
		onBackPressedDispatcher.addCallback( this, onBackPressed )

		// Show error when interval is not a number, or less than 1
		automaticRefreshIntervalEditText.doOnTextChanged { text, _, _, _ ->
			val value = text.toString().toIntOrNull()
			if ( value == null ) {
				automaticRefreshIntervalEditText.error = getString(R.string.settingsToastIntervalEmpty)
			} else if ( value < 1 ) {
				automaticRefreshIntervalEditText.error = getString(R.string.settingsToastIntervalInvalid)
			} else {
				automaticRefreshIntervalEditText.error = null
			}
		}

		// Register the observer for the always on-going notification worker
		UpdateWorker.observe( this, this )
		Log.d( Shared.logTag, "Registered observer for always on-going notification worker" )

	}

	// When the activity is closed...
	override fun onStop() {
		super.onStop()
		Log.d( Shared.logTag, "Stopped settings activity" )

		// Cancel all pending HTTP requests
		//API.cancelQueue()

		// Remove all observers for the always on-going notification worker
		//WorkManager.getInstance( applicationContext ).getWorkInfosForUniqueWorkLiveData( UpdateWorker.NAME ).removeObservers( this )
		//Log.d( Shared.logTag, "Removed all observers for the always on-going notification worker" )

		// Enable input
		enableInputs( true, Settings( getSharedPreferences( Shared.sharedPreferencesName, MODE_PRIVATE ) ).isSetup() )

	}

	// Show a confirmation when the back button is pressed - https://medium.com/tech-takeaways/how-to-migrate-the-deprecated-onbackpressed-function-e66bb29fa2fd
	private val onBackPressed: OnBackPressedCallback = object : OnBackPressedCallback( true ) {
		override fun handleOnBackPressed() {
			Log.d( Shared.logTag, "System back button pressed" )
			confirmBack( Settings( getSharedPreferences( Shared.sharedPreferencesName, MODE_PRIVATE ) ) )
		}
	}

	// Updates the UI with the settings from the persistent settings
	private fun updateUIWithSettings( settings: Settings) {
		Log.d( Shared.logTag, "Populating UI with settings from shared preferences..." )

		// Update the values
		automaticRefreshSwitch.isChecked = settings.automaticRefresh
		automaticRefreshIntervalEditText.setText( settings.automaticRefreshInterval.toString() )
		themeAutoCompleteTextView.setText( settings.theme, false )
		notificationsAlwaysOngoingSwitch.isChecked = settings.notificationAlwaysOngoing
		notificationsWhenIssueArisesSwitch.isChecked = settings.notificationWhenIssueArises

		// Enable instance URL & credentials if setup is finished
		if ( !settings.instanceUrl.isNullOrBlank() ) {
			instanceUrlEditText.setText( settings.instanceUrl )
			instanceUrlTextInputLayout.hint = getString(R.string.settingsTextInputLayoutInstanceUrlHint)
			instanceUrlEditText.isEnabled = true
			instanceUrlTextInputLayout.isEnabled = true
		}
		if ( !settings.credentialsUsername.isNullOrBlank() ) {
			credentialsUsernameEditText.setText( settings.credentialsUsername )
			credentialsUsernameInputTextLayout.hint = getString(R.string.settingsTextInputLayoutCredentialsUsernameHint)
			credentialsUsernameEditText.isEnabled = true
			credentialsUsernameInputTextLayout.isEnabled = true
		}
		if ( !settings.credentialsPassword.isNullOrBlank() ) {
			credentialsPasswordEditText.setText( settings.credentialsPassword )
			credentialsPasswordInputTextLayout.hint = getString(R.string.settingsTextInputLayoutCredentialsPasswordHint)
			credentialsPasswordEditText.isEnabled = true
			credentialsPasswordInputTextLayout.isEnabled = true
		}

		// Disable interval input if automatic refresh is switched off
		automaticRefreshIntervalTextInputLayout.isEnabled = automaticRefreshSwitch.isChecked
		automaticRefreshIntervalEditText.isEnabled = automaticRefreshSwitch.isChecked

	}

	// Saves the values to the persistent settings
	private fun saveSettings( settings: Settings, successCallback: () -> Unit ) {
		Log.d( Shared.logTag, "Saving settings to shared preferences..." )

		// Disable input
		enableInputs( false, settings.isSetup() )

		// Get the values from all the UI inputs
		val instanceUrl = instanceUrlEditText.text.toString()
		val credentialsUsername = credentialsUsernameEditText.text.toString()
		val credentialsPassword = credentialsPasswordEditText.text.toString()
		val automaticRefresh = automaticRefreshSwitch.isChecked
		val automaticRefreshInterval = automaticRefreshIntervalEditText.text.toString().toIntOrNull()
		val theme = themeAutoCompleteTextView.text.toString()
		val notificationAlwaysOngoing = notificationsAlwaysOngoingSwitch.isChecked
		val notificationWhenIssueArises = notificationsWhenIssueArisesSwitch.isChecked

		// Do not continue if the refresh interval wasn't provided
		if ( automaticRefreshInterval == null ) {
			enableInputs( true, settings.isSetup() )
			showBriefMessage( this, R.string.settingsToastIntervalEmpty )
			Log.w( Shared.logTag, "Refresh interval is empty" )
			return
		}

		// Do not continue if the refresh interval is too low
		if ( automaticRefreshInterval < 1 ) {
			enableInputs( true, settings.isSetup() )
			showBriefMessage( this, R.string.settingsToastIntervalInvalid )
			Log.w( Shared.logTag, "Refresh interval '${ automaticRefreshInterval }' is too low" )
			return
		}

		// Are we already setup?
		if ( settings.isSetup() ) {

			// Do not continue if an instance URL wasn't provided
			if ( instanceUrl.isBlank() ) {
				enableInputs( true, settings.isSetup() )
				showBriefMessage( this, R.string.settingsToastInstanceUrlEmpty )
				Log.w( Shared.logTag, "Instance URL is empty" )
				return
			}

			// Do not continue if the URL isn't valid
			if ( !validateInstanceUrl( instanceUrl ) ) {
				enableInputs( true, settings.isSetup() )
				showBriefMessage( this, R.string.settingsToastInstanceUrlInvalid )
				Log.w( Shared.logTag, "Instance URL '${ instanceUrl }' is invalid" )
				return
			}

			// Do not continue if a username wasn't provided
			if ( credentialsUsername.isBlank() ) {
				enableInputs( true, settings.isSetup() )
				showBriefMessage( this, R.string.settingsToastCredentialsUsernameEmpty )
				Log.w( Shared.logTag, "Username is empty" )
				return
			}

			// Do not continue if the username isn't valid
			if ( !validateCredentialsUsername( credentialsUsername ) ) {
				enableInputs( true, settings.isSetup() )
				showBriefMessage( this, R.string.settingsToastCredentialsUsernameInvalid )
				Log.w( Shared.logTag, "Username '${ credentialsUsername }' is invalid" )
				return
			}

			// Do not continue if a password wasn't provided
			if ( credentialsPassword.isBlank() ) {
				enableInputs( true, settings.isSetup() )
				showBriefMessage( this, R.string.settingsToastCredentialsPasswordEmpty )
				Log.w( Shared.logTag, "Password is empty" )
				return
			}

			// Do not continue if the password isn't valid
			if ( !validateCredentialsPassword( credentialsPassword ) ) {
				enableInputs( true, settings.isSetup() )
				showBriefMessage( this, R.string.settingsToastCredentialsPasswordInvalid )
				Log.w( Shared.logTag, "Password '${ credentialsPassword }' is invalid" )
				return
			}

			// Create a progress dialog
			val progressDialog = createProgressDialog( this, R.string.dialogProgressInstanceTestTitle, R.string.dialogProgressInstanceTestMessage ) {
				API.cancelQueue() // Cancel all pending HTTP requests
				enableInputs( true, settings.isSetup() ) // Enable input
				showBriefMessage( this, R.string.toastInstanceTestCancel )
			}

			// Test if a connector instance is running on this URL
			API.getHello( instanceUrl, credentialsUsername, credentialsPassword, { helloData ->
				Log.d( Shared.logTag, "Instance '${ instanceUrl }' is running! (Message: '${ helloData?.get( "message" )?.asString }')" )

				// Hide progress dialog & enable input
				progressDialog.dismiss()
				enableInputs( true, settings.isSetup() )

				// Update settings with these values
				settings.instanceUrl = instanceUrl
				settings.credentialsUsername = credentialsUsername
				settings.credentialsPassword = credentialsPassword
				settings.automaticRefresh = automaticRefresh
				settings.automaticRefreshInterval = automaticRefreshInterval
				settings.theme = theme
				settings.notificationAlwaysOngoing = notificationAlwaysOngoing
				settings.notificationWhenIssueArises = notificationWhenIssueArises
				settings.save()

				// Run the custom callback
				successCallback.invoke()

			}, { error, statusCode, errorCode ->
				Log.e( Shared.logTag, "Instance '${instanceUrl}' is NOT running! (Error: '${ error }', Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

				// Hide progress dialog & enable input
				progressDialog.dismiss()
				enableInputs( true, settings.isSetup() )

				when ( error ) {

					// Bad authentication
					is AuthFailureError -> when ( errorCode ) {
						ErrorCode.UnknownUser.code -> showBriefMessage( this, R.string.toastInstanceTestAuthenticationUnknownUser )
						ErrorCode.IncorrectPassword.code -> showBriefMessage( this, R.string.toastInstanceTestAuthenticationIncorrectPassword )
						else -> showBriefMessage( this, R.string.toastInstanceTestAuthenticationFailure )
					}

					// HTTP 4xx
					is ClientError -> when ( statusCode ) {
						404 -> showBriefMessage( this, R.string.toastInstanceTestNotFound )
						else -> showBriefMessage( this, R.string.toastInstanceTestClientFailure )
					}

					// HTTP 5xx
					is ServerError -> when ( statusCode ) {
						502 -> showBriefMessage( this, R.string.toastInstanceTestUnavailable )
						503 -> showBriefMessage( this, R.string.toastInstanceTestUnavailable )
						504 -> showBriefMessage( this, R.string.toastInstanceTestUnavailable )
						530 -> showBriefMessage( this, R.string.toastInstanceTestUnavailable ) // Cloudflare
						else -> showBriefMessage( this, R.string.toastInstanceTestServerFailure )
					}

					// No Internet connection, malformed domain
					is NoConnectionError -> showBriefMessage( this, R.string.toastInstanceTestNoConnection )
					is NetworkError -> showBriefMessage( this, R.string.toastInstanceTestNoConnection )

					// Connection timed out
					is TimeoutError -> showBriefMessage( this, R.string.toastInstanceTestTimeout )

					// Couldn't parse as JSON
					is ParseError -> showBriefMessage( this, R.string.toastInstanceTestParseFailure )

					// ¯\_(ツ)_/¯
					else -> showBriefMessage( this, R.string.toastInstanceTestFailure )

				}
			} )

			// Show the progress dialog
			progressDialog.show()

		// We're not setup yet...
		} else {

			// Enable input
			enableInputs( true, settings.isSetup() )

			// Only update the other settings
			settings.automaticRefresh = automaticRefresh
			settings.automaticRefreshInterval = automaticRefreshInterval
			settings.theme = theme
			settings.notificationAlwaysOngoing = notificationAlwaysOngoing
			settings.notificationWhenIssueArises = notificationWhenIssueArises
			settings.save()

			// Run the custom callback
			successCallback.invoke()

		}

	}

	// Enable/disable user input
	private fun enableInputs( shouldEnable: Boolean, isSetup: Boolean ) {

		// Only change these if we're already setup
		if ( isSetup ) {
			instanceUrlTextInputLayout.isEnabled = shouldEnable
			instanceUrlEditText.isEnabled = shouldEnable

			credentialsUsernameInputTextLayout.isEnabled = shouldEnable
			credentialsUsernameEditText.isEnabled = shouldEnable

			credentialsPasswordInputTextLayout.isEnabled = shouldEnable
			credentialsPasswordEditText.isEnabled = shouldEnable
		}

		automaticRefreshSwitch.isEnabled = shouldEnable
		automaticRefreshIntervalTextInputLayout.isEnabled = shouldEnable && automaticRefreshSwitch.isChecked // Only toggle this if automatic refreshing is enabled
		automaticRefreshIntervalEditText.isEnabled = shouldEnable && automaticRefreshSwitch.isChecked // Only toggle this if automatic refreshing is enabled
		//themeTextInputLayout.isEnabled = false // TODO: Don't enable this until dark theme is implemented
		//themeAutoCompleteTextView.isEnabled = false // TODO: Don't enable this until dark theme is implemented
		notificationsAlwaysOngoingSwitch.isEnabled = shouldEnable
		notificationsWhenIssueArisesSwitch.isEnabled = shouldEnable
		saveButton.isEnabled = shouldEnable

	}

	// Shows a confirmation dialog for leaving settings without saving changes, but only if the settings have been changed
	private fun confirmBack( settings: Settings) {
		if ( hasValuesChanged( settings ) ) {
			Log.d(Shared.logTag, "Settings have changed, showing confirmation dialog..." )

			showConfirmDialog( this, R.string.settingsDialogConfirmBackMessage, {
				Log.d( Shared.logTag, "Back confirmed, returning to previous activity..." )

				finish()
				overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			}, {
				Log.d( Shared.logTag, "Back aborted, not returning to previous activity" )
			} )

		} else {
			Log.d(Shared.logTag, "Settings have not changed, not showing confirmation dialog" )

			finish()
			overridePendingTransition(R.anim.slide_in_from_left, R.anim.slide_out_to_right)
		}
	}

	// Checks if the settings have changed
	private fun hasValuesChanged( settings: Settings): Boolean {

		// Get the values from all the inputs
		val instanceUrl = instanceUrlEditText.text.toString()
		val credentialsUsername = credentialsUsernameEditText.text.toString()
		val credentialsPassword = credentialsPasswordEditText.text.toString()
		val automaticRefresh = automaticRefreshSwitch.isChecked
		val automaticRefreshInterval = automaticRefreshIntervalEditText.text.toString().toInt()
		val theme = themeAutoCompleteTextView.text.toString()
		val notificationAlwaysOngoing = notificationsAlwaysOngoingSwitch.isChecked
		val notificationWhenIssueArises = notificationsWhenIssueArisesSwitch.isChecked

		// Only check these if we're already setup
		if ( settings.isSetup() ) {
			if ( instanceUrl != settings.instanceUrl ) return true
			if ( credentialsUsername != settings.credentialsUsername ) return true
			if ( credentialsPassword != settings.credentialsPassword ) return true
		}

		// True if any of the values have changed
		if ( automaticRefresh != settings.automaticRefresh ) return true
		if ( automaticRefreshInterval != settings.automaticRefreshInterval ) return true
		if ( theme != settings.theme ) return true
		if ( notificationAlwaysOngoing != settings.notificationAlwaysOngoing ) return true
		if ( notificationWhenIssueArises != settings.notificationWhenIssueArises ) return true

		// Otherwise false
		return false

	}

}

