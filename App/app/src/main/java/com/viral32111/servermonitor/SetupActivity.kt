package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import com.android.volley.Request
import com.android.volley.RequestQueue
import com.android.volley.toolbox.JsonObjectRequest
import com.android.volley.toolbox.Volley
import com.google.android.material.appbar.MaterialToolbar
import org.json.JSONObject
import java.util.*

class SetupActivity : AppCompatActivity() {

	// HTTP request queue
	private lateinit var requestQueue: RequestQueue
	private val requestQueueTag = Shared.logTag

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

		// Get all UI controls
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		val instanceUrlEditText = findViewById<EditText>( R.id.settingsInstanceUrlEditText )
		val credentialsUsernameEditText = findViewById<EditText>( R.id.setupCredentialsUsernameEditText )
		val credentialsPasswordEditText = findViewById<EditText>( R.id.setupCredentialsPasswordEditText )
		val continueButton = findViewById<Button>( R.id.settingsSaveButton )
		Log.d( Shared.logTag, "Got UI controls" )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.setupActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered } )" )

		// Get the persistent settings - https://developer.android.com/training/data-storage/shared-preferences
		val sharedPreferences = getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE )
		Log.d( Shared.logTag, "Got shared preferences for '${ Shared.sharedPreferencesName }'" )

		// Initialise the HTTP request queue - https://google.github.io/volley/simple.html#use-newrequestqueue
		requestQueue = Volley.newRequestQueue( applicationContext )

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

			// Get the values in the text inputs
			val instanceUrl = instanceUrlEditText.text.toString()
			val credentialsUsername = credentialsUsernameEditText.text.toString()
			val credentialsPassword = credentialsPasswordEditText.text.toString()
			Log.d( Shared.logTag, "Continue button pressed ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )

			// Do not continue if an instance URL wasn't provided
			if ( instanceUrl.isBlank() ) {
				showBriefMessage( this, R.string.setupToastInstanceUrlEmpty )
				Log.w( Shared.logTag, "Instance URL is empty" )
				return@setOnClickListener
			}

			// Do not continue if the URL isn't valid
			if ( !validateInstanceUrl( instanceUrl ) ) {
				showBriefMessage( this, R.string.setupToastInstanceUrlInvalid )
				Log.w( Shared.logTag, "Instance URL is invalid" )
				return@setOnClickListener
			}

			// Do not continue if a username wasn't provided
			if ( credentialsUsername.isBlank() ) {
				showBriefMessage( this, R.string.setupToastCredentialsUsernameEmpty )
				Log.w( Shared.logTag, "Username is empty" )
				return@setOnClickListener
			}

			// Do not continue if the username isn't valid
			if ( !validateCredentialsUsername( credentialsUsername ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsUsernameInvalid )
				Log.w( Shared.logTag, "Username is invalid" )
				return@setOnClickListener
			}

			// Do not continue if a password wasn't provided
			if ( credentialsPassword.isBlank() ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordEmpty )
				Log.w( Shared.logTag, "Password is empty" )
				return@setOnClickListener
			}

			// Do not continue if the password isn't valid
			if ( !validateCredentialsPassword( credentialsPassword ) ) {
				showBriefMessage( this, R.string.setupToastCredentialsPasswordInvalid )
				Log.w( Shared.logTag, "Password is invalid" )
				return@setOnClickListener
			}

			// Test if a Server Monitor instance is running on this URL
			testInstance( instanceUrl, credentialsUsername, credentialsPassword, { payload ->
				Log.d( Shared.logTag, "Instance test was successful! (${ payload })" )

				// Save the values to the shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
				with ( sharedPreferences.edit() ) {
					putString( "instanceUrl", instanceUrl )
					putString( "credentialsUsername", credentialsUsername )
					putString( "credentialsPassword", credentialsPassword )
					apply()
				}
				Log.d( Shared.logTag, "Saved values to shared preferences ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )

				// Change to the Servers activity
				switchActivity( 0 ) // TODO: Get number of servers

			// Show message if the test errors
			}, { reason ->
				Log.e( Shared.logTag, "Failed to test instance '${ instanceUrl }' (${ reason })" )
				showBriefMessage( this, R.string.setupToastInstanceUnavailable )
			} )

		}

		// Get the current values from shared preferences - they may be null if we're not setup yet
		val instanceUrl = sharedPreferences.getString( "instanceUrl", null )
		val credentialsUsername = sharedPreferences.getString( "credentialsUsername", null )
		val credentialsPassword = sharedPreferences.getString( "credentialsPassword", null )

		// Change to the next activity if we are already setup
		if ( !instanceUrl.isNullOrEmpty() && !credentialsUsername.isNullOrEmpty() && !credentialsPassword.isNullOrEmpty() ) {
			Log.d( Shared.logTag, "We're already setup! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
			switchActivity( 0 ) // TODO: Get number of servers
		} else {
			Log.d( Shared.logTag, "We're not setup yet! ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )
		}

	}

	// Cancel all HTTP requests when the app is closed - https://google.github.io/volley/simple.html#cancel-a-request
	override fun onStop() {
		super.onStop()

		Log.d( Shared.logTag, "Cancelling all HTTP requests in the queue..." )
		requestQueue.cancelAll( requestQueueTag )
	}

	// Sends a HTTP request to a URL to validate if the connector service is running on it
	private fun testInstance(url: String, username: String, password: String, successCallback: (payload: JSONObject ) -> Unit, errorCallback: (reason: String ) -> Unit ) {

		// Create the request to the given URL
		val httpRequest = object: JsonObjectRequest( Request.Method.GET, "$url/hello", null, { responsePayload ->
			successCallback.invoke( responsePayload )
		}, { error ->
			errorCallback.invoke( error.message ?: error.toString() )
		} ) {
			// Override the request headers - https://stackoverflow.com/a/53141982
			override fun getHeaders(): MutableMap<String, String> {
				val headers = HashMap<String, String>()

				// Expect a JSON response
				headers[ "Accept" ] = "application/json, */*"

				// Add the authentication - https://developer.mozilla.org/en-US/docs/Web/HTTP/Authentication, https://developer.android.com/reference/kotlin/java/util/Base64
				headers[ "Authorization" ] = "Basic ${ Base64.getEncoder().encodeToString( "${ username }:${ password }".toByteArray() ) }"
				Log.d( Shared.logTag, "Added authentication '${ headers[ "Authorization" ] }' to HTTP request" )

				return headers
			}
		}
		Log.d( Shared.logTag, "Created HTTP request to '${ url }'" )

		// Send the request
		httpRequest.tag = requestQueueTag
		requestQueue.add( httpRequest )
		Log.d( Shared.logTag, "Sending HTTP request..." )

	}

	// Switches to the next activity
	private fun switchActivity( serverCount: Int ) {

		// Switch to the Servers activity if there's more than 1 server, otherwise switch to the Server activity
		startActivity( Intent( this, if ( serverCount > 1 ) ServersActivity::class.java else ServerActivity::class.java ) )
		overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
		Log.d( Shared.logTag, "Switching to next activity..." )

		// Remove this activity from the back button history
		finish()

	}

}
