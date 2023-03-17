package com.viral32111.servermonitor

import android.content.Context
import android.content.SharedPreferences
import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import android.widget.Spinner
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.content.res.AppCompatResources
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.google.android.material.materialswitch.MaterialSwitch

class SettingsActivity : AppCompatActivity() {

	// UI
	private lateinit var instanceUrlEditText: EditText
	private lateinit var credentialsUsernameEditText: EditText
	private lateinit var credentialsPasswordEditText: EditText
	private lateinit var automaticRefreshSwitch: MaterialSwitch
	private lateinit var automaticRefreshIntervalEditText: EditText
	private lateinit var themeSpinner: Spinner
	private lateinit var notificationsAlwaysOngoingSwitch: MaterialSwitch
	private lateinit var notificationsWhenIssueArisesSwitch: MaterialSwitch
	private lateinit var saveButton: Button

	// Values from when activity loaded, for checking if anything has been changed when back is pressed
	private var instanceUrl: String? = null
	private var credentialsUsername: String? = null
	private var credentialsPassword: String? = null
	private var automaticRefresh: Boolean? = null
	private var automaticRefreshInterval: Int? = null
	private var theme: Int? = null
	private var notificationAlwaysOngoing: Boolean? = null
	private var notificationWhenIssueArises: Boolean? = null

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_settings )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
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
		instanceUrlEditText = findViewById( R.id.settingsInstanceUrlEditText )
		credentialsUsernameEditText = findViewById( R.id.settingsCredentialsUsernameEditText )
		credentialsPasswordEditText = findViewById( R.id.settingsCredentialsPasswordEditText )
		automaticRefreshSwitch = findViewById( R.id.settingsAutomaticRefreshSwitch )
		automaticRefreshIntervalEditText = findViewById( R.id.settingsAutomaticRefreshIntervalEditText )
		themeSpinner = findViewById( R.id.settingsThemeSpinner )
		notificationsAlwaysOngoingSwitch = findViewById( R.id.settingsNotificationsAlwaysOngoingSwitch )
		notificationsWhenIssueArisesSwitch = findViewById( R.id.settingsNotificationsWhenIssueArisesSwitch )
		saveButton = findViewById( R.id.settingsSaveButton )
		Log.d( Shared.logTag, "Got UI controls" )

		// Force theme selection by disabling interaction - This will be removed once dark theme is properly implemented
		themeSpinner.setSelection( 2 ) // Light
		themeSpinner.isEnabled = false
		Log.d( Shared.logTag, "Forced theme selection" )

		// Get the persistent settings - https://developer.android.com/training/data-storage/shared-preferences
		val sharedPreferences = getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE )
		Log.d( Shared.logTag, "Got shared preferences for '${ Shared.sharedPreferencesName }'" )

		// Update values with persistent settings & save in case it used defaults
		readSettings( sharedPreferences )
		saveSettings( sharedPreferences ) {
			Log.d( Shared.logTag, "Successfully re-saved settings" )
		}

		// Validate & save values when the save button is pressed, then return to the previous activity
		saveButton.setOnClickListener {
			saveSettings( sharedPreferences ) {
				finish()
				overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			}
		}

		// Disables interval input when automatic refresh is switched off
		automaticRefreshSwitch.setOnCheckedChangeListener { _, isChecked ->
			automaticRefreshIntervalEditText.isEnabled = isChecked
		}

		// Enable the back button on the toolbar
		materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this, R.drawable.ic_baseline_arrow_back_24 )
		materialToolbar?.setNavigationOnClickListener {
			Log.d( Shared.logTag, "Navigation back button pressed" )
			confirmBack()
		}

		// Register the back button pressed callback - https://medium.com/tech-takeaways/how-to-migrate-the-deprecated-onbackpressed-function-e66bb29fa2fd
		onBackPressedDispatcher.addCallback( this, onBackPressed )

	}

	// When the activity is closed...
	override fun onStop() {
		super.onStop()

		// Cancel all pending HTTP requests
		API.cancelQueue()

		// Enable input
		enableInputs( true )

	}

	// Show a confirmation when the back button is pressed - https://medium.com/tech-takeaways/how-to-migrate-the-deprecated-onbackpressed-function-e66bb29fa2fd
	private val onBackPressed: OnBackPressedCallback = object : OnBackPressedCallback( true ) {
		override fun handleOnBackPressed() {
			Log.d( Shared.logTag, "System back button pressed" )
			confirmBack()
		}
	}

	// Updates the UI with the settings from the persistent settings
	private fun readSettings( sharedPreferences: SharedPreferences ) {
		Log.d( Shared.logTag, "Populating UI with settings from shared preferences..." )

		// Get stored settings or default to current values - https://developer.android.com/training/data-storage/shared-preferences#ReadSharedPreference
		instanceUrl = sharedPreferences.getString( "instanceUrl", instanceUrlEditText.text.toString() )
		credentialsUsername = sharedPreferences.getString( "credentialsUsername", credentialsUsernameEditText.text.toString() )
		credentialsPassword = sharedPreferences.getString( "credentialsPassword", credentialsPasswordEditText.text.toString() )
		automaticRefresh = sharedPreferences.getBoolean( "automaticRefresh", automaticRefreshSwitch.isChecked )
		automaticRefreshInterval = sharedPreferences.getInt( "automaticRefreshInterval", 15 ) // Default to 15 seconds
		theme = sharedPreferences.getInt( "theme", themeSpinner.selectedItemPosition ) // Not ideal to use position but we'll never have more than 3 themes anyway (system, light & dark)
		notificationAlwaysOngoing = sharedPreferences.getBoolean( "notificationAlwaysOngoing", notificationsAlwaysOngoingSwitch.isChecked )
		notificationWhenIssueArises = sharedPreferences.getBoolean( "notificationWhenIssueArises", notificationsWhenIssueArisesSwitch.isChecked )

		// Update the values
		automaticRefreshSwitch.isChecked = automaticRefresh as Boolean
		automaticRefreshIntervalEditText.setText( automaticRefreshInterval.toString() )
		themeSpinner.setSelection( theme as Int )
		notificationsAlwaysOngoingSwitch.isChecked = notificationAlwaysOngoing as Boolean
		notificationsWhenIssueArisesSwitch.isChecked = notificationWhenIssueArises as Boolean

		// Enable instance URL & credentials if setup is finished
		if ( !instanceUrl.isNullOrBlank() ) {
			instanceUrlEditText.setText( instanceUrl )
			instanceUrlEditText.hint = getString( R.string.settingsEditTextInstanceUrlHint )
			instanceUrlEditText.isEnabled = true
		}
		if ( !credentialsUsername.isNullOrBlank() ) {
			credentialsUsernameEditText.setText( credentialsUsername )
			credentialsUsernameEditText.hint = getString( R.string.settingsEditTextCredentialsUsernameHint )
			credentialsUsernameEditText.isEnabled = true
		}
		if ( !credentialsPassword.isNullOrBlank() ) {
			credentialsPasswordEditText.setText( credentialsPassword )
			credentialsPasswordEditText.hint = getString( R.string.settingsEditTextCredentialsPasswordHint )
			credentialsPasswordEditText.isEnabled = true
		}

		// Disable interval input if automatic refresh is switched off
		automaticRefreshIntervalEditText.isEnabled = automaticRefreshSwitch.isChecked

	}

	// Saves the values to the persistent settings
	private fun saveSettings( sharedPreferences: SharedPreferences, successCallback: () -> Unit ) {
		Log.d( Shared.logTag, "Saving settings to shared preferences..." )

		// Disable input
		enableInputs( false )

		// Get the values from all the UI inputs
		val instanceUrl = instanceUrlEditText.text.toString()
		val credentialsUsername = credentialsUsernameEditText.text.toString()
		val credentialsPassword = credentialsPasswordEditText.text.toString()
		val automaticRefresh = automaticRefreshSwitch.isChecked
		val automaticRefreshInterval = automaticRefreshIntervalEditText.text.toString().toInt()
		val theme = themeSpinner.selectedItemPosition
		val notificationAlwaysOngoing = notificationsAlwaysOngoingSwitch.isChecked
		val notificationWhenIssueArises = notificationsWhenIssueArisesSwitch.isChecked

		// Do not continue if an instance URL wasn't provided
		if ( instanceUrl.isBlank() ) {
			enableInputs( true )
			showBriefMessage( this, R.string.settingsToastInstanceUrlEmpty )
			Log.w( Shared.logTag, "Instance URL is empty" )
			return
		}

		// Do not continue if the URL isn't valid
		if ( !validateInstanceUrl( instanceUrl ) ) {
			enableInputs( true )
			showBriefMessage( this, R.string.settingsToastInstanceUrlInvalid )
			Log.w( Shared.logTag, "Instance URL '${ instanceUrl }' is invalid" )
			return
		}

		// Do not continue if a username wasn't provided
		if ( credentialsUsername.isBlank() ) {
			enableInputs( true )
			showBriefMessage( this, R.string.settingsToastCredentialsUsernameEmpty )
			Log.w( Shared.logTag, "Username is empty" )
			return
		}

		// Do not continue if the username isn't valid
		if ( !validateCredentialsUsername( credentialsUsername ) ) {
			enableInputs( true )
			showBriefMessage( this, R.string.settingsToastCredentialsUsernameInvalid )
			Log.w( Shared.logTag, "Username '${ credentialsUsername }' is invalid" )
			return
		}

		// Do not continue if a password wasn't provided
		if ( credentialsPassword.isBlank() ) {
			enableInputs( true )
			showBriefMessage( this, R.string.settingsToastCredentialsPasswordEmpty )
			Log.w( Shared.logTag, "Password is empty" )
			return
		}

		// Do not continue if the password isn't valid
		if ( !validateCredentialsPassword( credentialsPassword ) ) {
			enableInputs( true )
			showBriefMessage( this, R.string.settingsToastCredentialsPasswordInvalid )
			Log.w( Shared.logTag, "Password '${ credentialsPassword }' is invalid" )
			return
		}

		// Create a progress dialog
		val progressDialog = createProgressDialog( this, R.string.dialogProgressInstanceTestTitle, R.string.dialogProgressInstanceTestMessage ) {
			API.cancelQueue() // Cancel all pending HTTP requests
			enableInputs( true ) // Enable input
			showBriefMessage( this, R.string.toastInstanceTestCancel )
		}

		// Test if a connector instance is running on this URL
		API.getHello( instanceUrl, credentialsUsername, credentialsPassword, { helloData ->
			Log.d( Shared.logTag, "Instance '${ instanceUrl }' is running! (Message: '${ helloData?.get( "message" )?.asString }')" )

			// Hide progress dialog & enable input
			progressDialog.dismiss()
			enableInputs( true )

			// Save the values to shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
			with ( sharedPreferences.edit() ) {
				putString( "instanceUrl", instanceUrl )
				putString( "credentialsUsername", credentialsUsername )
				putString( "credentialsPassword", credentialsPassword )
				putBoolean( "automaticRefresh", automaticRefresh )
				putInt( "automaticRefreshInterval", automaticRefreshInterval )
				putInt( "theme", theme )
				putBoolean( "notificationAlwaysOngoing", notificationAlwaysOngoing )
				putBoolean( "notificationWhenIssueArises", notificationWhenIssueArises )
				apply()
			}
			Log.d( Shared.logTag, "Saved settings to shared preferences" )

			// Run the custom callback
			successCallback.invoke()

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Instance '${ instanceUrl }' is NOT running! (Error: '${ error }', Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

			// Hide progress dialog & enable input
			progressDialog.dismiss()
			enableInputs( true )

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

	}

	// Enable/disable user input
	private fun enableInputs( shouldEnable: Boolean ) {
		instanceUrlEditText.isEnabled = shouldEnable
		credentialsUsernameEditText.isEnabled = shouldEnable
		credentialsPasswordEditText.isEnabled = shouldEnable
		automaticRefreshSwitch.isEnabled = shouldEnable
		automaticRefreshIntervalEditText.isEnabled = shouldEnable
		themeSpinner.isEnabled = shouldEnable
		notificationsAlwaysOngoingSwitch.isEnabled = shouldEnable
		notificationsWhenIssueArisesSwitch.isEnabled = shouldEnable
		saveButton.isEnabled = shouldEnable
	}

	// Shows a confirmation dialog for leaving settings without saving changes, but only if the settings have been changed
	private fun confirmBack() {
		if ( hasValuesChanged() ) {
			Log.d( Shared.logTag, "Settings have changed, showing confirmation dialog..." )

			MaterialAlertDialogBuilder( this )
				.setTitle( R.string.settingsDialogConfirmBackTitle )
				.setMessage( R.string.settingsDialogConfirmBackMessage )
				.setPositiveButton( R.string.settingsDialogConfirmBackPositiveButton ) { _, _ ->
					Log.d( Shared.logTag, "Back confirmed, returning to previous activity..." )

					finish()
					overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
				}
				.setNegativeButton( R.string.settingsDialogConfirmBackNegativeButton ) { _, _ ->
					Log.d( Shared.logTag, "Back aborted, not returning to previous activity" )
				}
				.show()

		} else {
			Log.d( Shared.logTag, "Settings have not changed, not showing confirmation dialog" )

			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
		}
	}

	// Checks if the values have changed since the activity loaded
	private fun hasValuesChanged(): Boolean {

		// Get the values from all the inputs
		val instanceUrl = instanceUrlEditText.text.toString()
		val credentialsUsername = credentialsUsernameEditText.text.toString()
		val credentialsPassword = credentialsPasswordEditText.text.toString()
		val automaticRefresh = automaticRefreshSwitch.isChecked
		val automaticRefreshInterval = automaticRefreshIntervalEditText.text.toString().toInt()
		val theme = themeSpinner.selectedItemPosition
		val notificationAlwaysOngoing = notificationsAlwaysOngoingSwitch.isChecked
		val notificationWhenIssueArises = notificationsWhenIssueArisesSwitch.isChecked

		// True if any of the values have changed
		if ( instanceUrl != this.instanceUrl ) return true
		if ( credentialsUsername != this.credentialsUsername ) return true
		if ( credentialsPassword != this.credentialsPassword ) return true
		if ( automaticRefresh != this.automaticRefresh ) return true
		if ( automaticRefreshInterval != this.automaticRefreshInterval ) return true
		if ( theme != this.theme ) return true
		if ( notificationAlwaysOngoing != this.notificationAlwaysOngoing ) return true
		if ( notificationWhenIssueArises != this.notificationWhenIssueArises ) return true

		// Otherwise false
		return false

	}

}

