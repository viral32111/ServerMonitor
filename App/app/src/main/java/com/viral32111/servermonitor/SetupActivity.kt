package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
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

		// Hide the logout button as it is pointless during setup - https://www.tutorialspoint.com/how-do-i-hide-and-show-a-menu-item-in-the-android-actionbar
		materialToolbar?.menu?.findItem( R.id.actionBarLogout )?.isVisible = false

		// Get all the UI
		instanceUrlEditText = findViewById( R.id.setupInstanceUrlTextInputEditText )
		credentialsUsernameEditText = findViewById( R.id.setupCredentialsUsernameTextInputEditText )
		credentialsPasswordEditText = findViewById( R.id.setupCredentialsPasswordTextInputEditText )
		continueButton = findViewById( R.id.settingsSaveButton )
		Log.d( Shared.logTag, "Got UI controls" )

		// Get the settings - https://developer.android.com/training/data-storage/shared-preferences
		val settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE ) )
		Log.d( Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Initialise our RESTful API class
		API.initializeQueue( applicationContext )

		// When an item on the action bar menu is pressed...
		materialToolbar?.setOnMenuItemClickListener { menuItem ->

			// Settings
			if ( menuItem.title?.equals( getString( R.string.actionBarMenuSettings ) ) == true ) {
				Log.d( Shared.logTag, "Opening Settings activity..." )
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )

			// Logout
			} else if ( menuItem.title?.equals( getString( R.string.actionBarMenuLogout ) ) == true ) {
				Log.w( Shared.logTag, "Logout from the setup activity?!" )

			// About
			} else if ( menuItem.title?.equals( getString( R.string.actionBarMenuAbout ) ) == true ) {
				Log.d( Shared.logTag, "Showing information about app dialog..." )
				showInformationDialog( this, R.string.dialogInformationAboutTitle, R.string.dialogInformationAboutMessage )
			}

			return@setOnMenuItemClickListener true
		}

		// When the the continue button is pressed...
		continueButton.setOnClickListener {

			// Disable input
			enableInputs( false )

			// Get the values in the text inputs
			val instanceUrl = instanceUrlEditText.text.toString()
			val credentialsUsername = credentialsUsernameEditText.text.toString()
			val credentialsPassword = credentialsPasswordEditText.text.toString()
			Log.d( Shared.logTag, "Continue button pressed ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )

			// Do not continue if an instance URL wasn't provided
			if ( instanceUrl.isBlank() ) {
				enableInputs( true )
				showBriefMessage( this, R.string.setupToastInstanceUrlEmpty )
				Log.w( Shared.logTag, "Instance URL is empty" )
				return@setOnClickListener
			}

			// Do not continue if the URL isn't valid
			if ( !validateInstanceUrl( instanceUrl ) ) {
				enableInputs( true )
				showBriefMessage( this, R.string.setupToastInstanceUrlInvalid )
				Log.w( Shared.logTag, "Instance URL is invalid" )
				return@setOnClickListener
			}

			// Do not continue if a username wasn't provided
			if ( credentialsUsername.isBlank() ) {
				enableInputs( true )
				showBriefMessage( this, R.string.setupToastCredentialsUsernameEmpty )
				Log.w( Shared.logTag, "Username is empty" )
				return@setOnClickListener
			}

			// Do not continue if the username isn't valid
			if ( !validateCredentialsUsername( credentialsUsername ) ) {
				enableInputs( true )
				showBriefMessage( this, R.string.setupToastCredentialsUsernameInvalid )
				Log.w( Shared.logTag, "Username is invalid" )
				return@setOnClickListener
			}

			// Do not continue if a password wasn't provided
			if ( credentialsPassword.isBlank() ) {
				enableInputs( true )
				showBriefMessage( this, R.string.setupToastCredentialsPasswordEmpty )
				Log.w( Shared.logTag, "Password is empty" )
				return@setOnClickListener
			}

			// Do not continue if the password isn't valid
			if ( !validateCredentialsPassword( credentialsPassword ) ) {
				enableInputs( true )
				showBriefMessage( this, R.string.setupToastCredentialsPasswordInvalid )
				Log.w( Shared.logTag, "Password is invalid" )
				return@setOnClickListener
			}

			// Create a progress dialog for the connector test
			val progressDialog = createProgressDialog( this, R.string.dialogProgressInstanceTestTitle, R.string.dialogProgressInstanceTestMessage ) {
				API.cancelQueue() // Cancel all pending HTTP requests
				enableInputs( true ) // Enable input
				showBriefMessage( this, R.string.toastInstanceTestCancel )
			}

			// Test if a connector instance is running on this URL...
			testInstance( instanceUrl, credentialsUsername, credentialsPassword, {

				// Hide the progress dialog & enable UI
				progressDialog.dismiss()
				enableInputs( true )

				// Update the settings with these values
				settings.instanceUrl = instanceUrl
				settings.credentialsUsername = credentialsUsername
				settings.credentialsPassword = credentialsPassword
				settings.save()

				// Change to the appropriate activity
				switchActivity( instanceUrl, credentialsUsername, credentialsPassword )

			}, {
				progressDialog.dismiss() // Hide the progress dialog
				enableInputs( true ) // Enable input
			} )

			// Show the progress dialog
			progressDialog.show()

		}

		// Are we already setup?
		if ( settings.isSetup() ) {
			Log.d( Shared.logTag, "We're already setup! ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

			// Populate the UI so the user doesn't have to retype everything if the instance test fails
			instanceUrlEditText.setText( settings.instanceUrl )
			credentialsUsernameEditText.setText( settings.credentialsUsername )
			credentialsPasswordEditText.setText( settings.credentialsPassword )

			// Disable input
			enableInputs( false )

			// Create a progress dialog
			val progressDialog = createProgressDialog( this, R.string.dialogProgressInstanceTestTitle, R.string.dialogProgressInstanceTestMessage ) {
				API.cancelQueue() // Cancel all pending HTTP requests
				enableInputs( true ) // Enable input
				showBriefMessage( this, R.string.toastInstanceTestCancel )
			}

			// Test if this instance is running...
			testInstance( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, {
				progressDialog.dismiss() // Hide the progress dialog
				enableInputs( true ) // Enable input
				switchActivity( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! ) // Change to the next appropriate activity
			}, {
				Log.e( Shared.logTag, "We may already be setup, but the instance is offline?" )
				progressDialog.dismiss() // Hide the progress dialog
				enableInputs( true ) // Enable input
			} )

			// Show the progress dialog
			progressDialog.show()

		} else {
			Log.d( Shared.logTag, "We're not setup yet! ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )
		}

		/*
		// Event handlers to show input validation errors on the inputs
		instanceUrlEditText.setOnFocusChangeListener { _, hasFocus ->
			if ( !hasFocus ) instanceUrlEditText.error = if ( !validateInstanceUrl( instanceUrlEditText.text.toString() ) ) getString( R.string.setupToastInstanceUrlInvalid ) else null
		}
		credentialsUsernameEditText.setOnFocusChangeListener { _, hasFocus ->
			if ( !hasFocus ) credentialsUsernameEditText.error = if ( !validateCredentialsUsername( credentialsUsernameEditText.text.toString() ) ) getString( R.string.setupToastCredentialsUsernameInvalid ) else null
		}
		credentialsPasswordEditText.setOnFocusChangeListener { _, hasFocus ->
			if ( !hasFocus ) credentialsPasswordEditText.error = if ( !validateCredentialsPassword( credentialsPasswordEditText.text.toString() ) ) getString( R.string.setupToastCredentialsPasswordInvalid ) else null
		}
		*/

	}

	// When the activity is closed...
	override fun onStop() {
		super.onStop()
		Log.d( Shared.logTag, "Stopped setup activity" )

		// Cancel all pending HTTP requests
		//API.cancelQueue()

		// Enable input
		enableInputs( true )

	}

	// Tests if a connector instance is running on a given URL
	private fun testInstance( instanceUrl: String, credentialsUsername: String, credentialsPassword: String, successCallback: () -> Unit, errorCallback: () -> Unit ) {
		API.getHello( instanceUrl, credentialsUsername, credentialsPassword, { helloData ->
			val message = helloData?.get( "message" )?.asString
			val user = helloData?.get( "user" )?.asString
			val version = helloData?.get( "version" )?.asString

			val contact = helloData?.get( "contact" )?.asJsonObject
			val contactName = contact?.get( "name" )?.asString
			val contactMethods = contact?.get( "methods" )?.asJsonArray

			Log.d( Shared.logTag, "Instance '${ instanceUrl }' is running! (Version: '${ version }', User: '${ user }', Message: '${ message }', Contact: '${ contactName }')" )
			successCallback.invoke() // Run the custom callback

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Instance '${ instanceUrl }' is NOT running! (Error: '${ error }', Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )
			errorCallback.invoke() // Run the custom callback

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
	}

	// Switches to the next activity
	private fun switchActivity( instanceUrl: String, credentialsUsername: String, credentialsPassword: String ) {

		// Disable input
		enableInputs( false )

		// Create a progress dialog
		val progressDialog = createProgressDialog( this, R.string.setupDialogProgressServerCountTitle, R.string.setupDialogProgressServerCountMessage ) {
			API.cancelQueue() // Cancel all pending HTTP requests
			enableInputs( true ) // Enable input
			showBriefMessage( this, R.string.setupToastServerCountCancel )
		}

		// Fetch the list of servers
		API.getServers( instanceUrl, credentialsUsername, credentialsPassword, { serversData ->

			// Hide the progress dialog & enable input
			progressDialog.dismiss()
			enableInputs( true )

			// Get the array
			val servers = serversData?.get( "servers" )?.asJsonArray
			Log.d( Shared.logTag, "Got '${ servers?.size() }' servers from API ('${ servers.toString() }')" )

			// Switch to the Servers activity if there's more than 1 server, otherwise switch to the Server activity
			// NOTE: This will fallback to 2 servers, as that will show the Servers activity which is capable of displaying only 1 server
			Log.d( Shared.logTag, "Switching to next activity..." )
			startActivity( Intent( this, if ( ( servers?.size() ?: 2 ) > 1) ServersActivity::class.java else ServerActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )

			// Remove this activity from the back button history
			finish()

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Failed to get servers from API due to '${ error }' (Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

			// Hide the progress dialog & enable input
			progressDialog.dismiss()
			enableInputs( true )

			when ( error ) {

				// Bad authentication
				is AuthFailureError -> when ( errorCode ) {
					ErrorCode.UnknownUser.code -> showBriefMessage( this, R.string.setupToastServerCountAuthenticationUnknownUser )
					ErrorCode.IncorrectPassword.code -> showBriefMessage( this, R.string.setupToastServerCountAuthenticationIncorrectPassword )
					else -> showBriefMessage( this, R.string.setupToastServerCountAuthenticationFailure )
				}

				// HTTP 4xx
				is ClientError -> when ( statusCode ) {
					404 -> showBriefMessage( this, R.string.setupToastServerCountNotFound )
					else -> showBriefMessage( this, R.string.setupToastServerCountClientFailure )
				}

				// HTTP 5xx
				is ServerError -> when ( statusCode ) {
					502 -> showBriefMessage( this, R.string.setupToastServerCountUnavailable )
					503 -> showBriefMessage( this, R.string.setupToastServerCountUnavailable )
					504 -> showBriefMessage( this, R.string.setupToastServerCountUnavailable )
					else -> showBriefMessage( this, R.string.setupToastServerCountServerFailure )
				}

				// No Internet connection, malformed domain
				is NoConnectionError -> showBriefMessage( this, R.string.setupToastServerCountNoConnection )
				is NetworkError -> showBriefMessage( this, R.string.setupToastServerCountNoConnection )

				// Connection timed out
				is TimeoutError -> showBriefMessage( this, R.string.setupToastServerCountTimeout )

				// Couldn't parse as JSON
				is ParseError -> showBriefMessage( this, R.string.setupToastServerCountParseFailure )

				// ¯\_(ツ)_/¯
				else -> showBriefMessage( this, R.string.setupToastServerCountFailure )

			}
		} )

		// Show the progress dialog
		progressDialog.show()

	}

	// Enables/disables user input
	private fun enableInputs( shouldEnable: Boolean ) {
		instanceUrlEditText.isEnabled = shouldEnable
		credentialsUsernameEditText.isEnabled = shouldEnable
		credentialsPasswordEditText.isEnabled = shouldEnable
		continueButton.isEnabled = shouldEnable
	}

}
