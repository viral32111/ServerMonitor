package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.ProgressBar
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar

class SetupActivity : AppCompatActivity() {

	// UI
	private lateinit var instanceUrlEditText: EditText
	private lateinit var credentialsUsernameEditText: EditText
	private lateinit var credentialsPasswordEditText: EditText
	private lateinit var continueButton: Button
	private lateinit var progressBar: ProgressBar

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Display the layout
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_setup )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Set the title on the toolbar
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		materialToolbar?.title = getString( R.string.setupActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Get all UI controls
		instanceUrlEditText = findViewById( R.id.settingsInstanceUrlEditText )
		credentialsUsernameEditText = findViewById( R.id.setupCredentialsUsernameEditText )
		credentialsPasswordEditText = findViewById( R.id.setupCredentialsPasswordEditText )
		continueButton = findViewById( R.id.settingsSaveButton )
		progressBar = findViewById( R.id.setupProgressBar )
		Log.d( Shared.logTag, "Got UI controls" )

		// Get the persistent settings - https://developer.android.com/training/data-storage/shared-preferences
		val sharedPreferences = getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE )
		Log.d( Shared.logTag, "Got shared preferences for '${ Shared.sharedPreferencesName }'" )

		// Initialise our RESTful API class
		API.initializeQueue( applicationContext )

		// Open settings when its action bar menu item is clicked
		materialToolbar?.setOnMenuItemClickListener { menuItem ->
			if ( menuItem.title?.equals( getString( R.string.action_bar_menu_settings ) ) == true ) {
				Log.d( Shared.logTag, "Opening Settings activity..." )
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			}

			return@setOnMenuItemClickListener true
		}

		// When the the continue button is pressed...
		continueButton.setOnClickListener {

			// Disable UI & show loading spinner
			setLoading( true )

			// Get the values in the text inputs
			val instanceUrl = instanceUrlEditText.text.toString()
			val credentialsUsername = credentialsUsernameEditText.text.toString()
			val credentialsPassword = credentialsPasswordEditText.text.toString()
			Log.d( Shared.logTag, "Continue button pressed ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )

			// Do not continue if an instance URL wasn't provided
			if ( instanceUrl.isBlank() ) {
				setLoading( false )
				showBriefMessage( this, R.string.setupToastInstanceUrlEmpty )
				Log.w( Shared.logTag, "Instance URL is empty" )
				return@setOnClickListener
			}

			// Do not continue if the URL isn't valid
			if ( !validateInstanceUrl( instanceUrl ) ) {
				setLoading( false )
				showBriefMessage( this, R.string.setupToastInstanceUrlInvalid )
				Log.w( Shared.logTag, "Instance URL is invalid" )
				return@setOnClickListener
			}

			// Do not continue if a username wasn't provided
			if ( credentialsUsername.isBlank() ) {
				setLoading( false )
				showBriefMessage( this, R.string.setupToastCredentialsUsernameEmpty )
				Log.w( Shared.logTag, "Username is empty" )
				return@setOnClickListener
			}

			// Do not continue if the username isn't valid
			if ( !validateCredentialsUsername( credentialsUsername ) ) {
				setLoading( false )
				showBriefMessage( this, R.string.setupToastCredentialsUsernameInvalid )
				Log.w( Shared.logTag, "Username is invalid" )
				return@setOnClickListener
			}

			// Do not continue if a password wasn't provided
			if ( credentialsPassword.isBlank() ) {
				setLoading( false )
				showBriefMessage( this, R.string.setupToastCredentialsPasswordEmpty )
				Log.w( Shared.logTag, "Password is empty" )
				return@setOnClickListener
			}

			// Do not continue if the password isn't valid
			if ( !validateCredentialsPassword( credentialsPassword ) ) {
				setLoading( false )
				showBriefMessage( this, R.string.setupToastCredentialsPasswordInvalid )
				Log.w( Shared.logTag, "Password is invalid" )
				return@setOnClickListener
			}

			// Test if a connector instance is running on this URL
			API.getHello( instanceUrl, credentialsUsername, credentialsPassword, { helloData ->
				Log.d( Shared.logTag, "Instance '${ instanceUrl }' is running! (Message: '${ helloData?.get( "message" )?.asString }')" )

				// Enable UI & hide loading spinner
				setLoading( false )

				// Save the values to the shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
				with ( sharedPreferences.edit() ) {
					putString( "instanceUrl", instanceUrl )
					putString( "credentialsUsername", credentialsUsername )
					putString( "credentialsPassword", credentialsPassword )
					apply()
				}
				Log.d( Shared.logTag, "Saved values to shared preferences (URL: '${ instanceUrl }', Username: '${ credentialsUsername }', Password: '${ credentialsPassword }')" )

				// Change to the appropriate activity
				switchActivity( instanceUrl, credentialsUsername, credentialsPassword )

			// Show message if the test fails
			}, { error, statusCode, errorCode ->
				Log.e( Shared.logTag, "Instance '${ instanceUrl }' is NOT running! (Error: '${ error }', Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

				// Enable UI & hide loading spinner
				setLoading( false )

				when ( error ) {

					// Bad authentication
					is AuthFailureError -> when ( errorCode ) {
						ErrorCode.UnknownUser.code -> showBriefMessage( this, R.string.setupToastInstanceTestAuthenticationUnknownUser )
						ErrorCode.IncorrectPassword.code -> showBriefMessage( this, R.string.setupToastInstanceTestAuthenticationIncorrectPassword )
						else -> showBriefMessage( this, R.string.setupToastInstanceTestAuthenticationFailure )
					}

					// HTTP 4xx
					is ClientError -> when ( statusCode ) {
						404 -> showBriefMessage( this, R.string.setupToastInstanceTestNotFound )
						else -> showBriefMessage( this, R.string.setupToastInstanceTestClientFailure )
					}

					// HTTP 5xx
					is ServerError -> showBriefMessage( this, R.string.setupToastInstanceTestServerFailure )

					// No Internet connection, malformed domain
					is NoConnectionError -> showBriefMessage( this, R.string.setupToastInstanceTestNoConnection )
					is NetworkError -> showBriefMessage( this, R.string.setupToastInstanceTestNoConnection )

					// Connection timed out
					is TimeoutError -> showBriefMessage( this, R.string.setupToastInstanceTestTimeout )

					// Couldn't parse as JSON
					is ParseError -> showBriefMessage( this, R.string.setupToastInstanceTestParseFailure )

					// ¯\_(ツ)_/¯
					else -> showBriefMessage( this, R.string.setupToastInstanceTestFailure )

				}
			} )

		}

		// Get the current values from shared preferences - they may be null if we're not setup yet
		val instanceUrl = sharedPreferences.getString( "instanceUrl", null )
		val credentialsUsername = sharedPreferences.getString( "credentialsUsername", null )
		val credentialsPassword = sharedPreferences.getString( "credentialsPassword", null )

		// Change to the appropriate activity if we are already setup
		if ( !instanceUrl.isNullOrBlank() && !credentialsUsername.isNullOrBlank() && !credentialsPassword.isNullOrBlank() ) {
			Log.d( Shared.logTag, "We're already setup! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
			switchActivity( instanceUrl, credentialsUsername, credentialsPassword )
		} else {
			Log.d( Shared.logTag, "We're not setup yet! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
		}

	}

	// Cancels pending HTTP requests when the app is closed
	override fun onStop() {
		super.onStop()
		API.cancelQueue()
	}

	// Switches to the next activity
	private fun switchActivity( instanceUrl: String, credentialsUsername: String, credentialsPassword: String ) {
		API.getServers( instanceUrl, credentialsUsername, credentialsPassword, { serversData ->
			val servers = serversData?.get( "servers" )?.asJsonArray
			Log.d( Shared.logTag, "Got '${ serversData?.size() }' servers from API: '${ servers.toString() }'" )

			// Fallback to 2 servers, as that will show the Servers activity which can display only 1
			val serverCount = serversData?.size() ?: 2

			// Switch to the Servers activity if there's more than 1 server, otherwise switch to the Server activity
			startActivity( Intent( this, if ( serverCount > 1 ) ServersActivity::class.java else ServerActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			Log.d( Shared.logTag, "Switching to next activity..." )

			// Remove this activity from the back button history
			finish()

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Failed to get servers from API due to '${ error }' (Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

			when ( error ) {

				// HTTP 4xx, HTTP 5xx
				is ClientError -> showBriefMessage( this, R.string.setupToastInstanceTestClientFailure )
				is ServerError -> showBriefMessage( this, R.string.setupToastInstanceTestServerFailure )

				// No Internet connection, malformed domain
				is NoConnectionError -> showBriefMessage( this, R.string.setupToastInstanceTestNoConnection )
				is NetworkError -> showBriefMessage( this, R.string.setupToastInstanceTestNoConnection )

				// Connection timed out
				is TimeoutError -> showBriefMessage( this, R.string.setupToastInstanceTestTimeout )

				// Couldn't parse as JSON
				is ParseError -> showBriefMessage( this, R.string.setupToastInstanceTestParseFailure )

				// ¯\_(ツ)_/¯
				else -> showBriefMessage( this, R.string.setupToastInstanceTestFailure )

			}
		} )
	}

	private fun setLoading( isLoading: Boolean ) {

		// Enable/disable user input
		instanceUrlEditText.isEnabled = !isLoading
		credentialsUsernameEditText.isEnabled = !isLoading
		credentialsPasswordEditText.isEnabled = !isLoading
		continueButton.isEnabled = !isLoading

		// Show/hide progress spinner
		progressBar.visibility = if ( isLoading ) View.VISIBLE else View.GONE

	}

}
