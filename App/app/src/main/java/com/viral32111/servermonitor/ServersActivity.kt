package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.util.Log
import androidx.appcompat.app.ActionBar
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar
import com.google.gson.JsonObject

class ServersActivity : AppCompatActivity() {

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_servers )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Get all UI controls
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		Log.d( Shared.logTag, "Got UI controls" )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.serversActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Open settings when its action bar menu item is clicked
		materialToolbar?.setOnMenuItemClickListener { menuItem ->
			if ( menuItem.title?.equals( getString( R.string.action_bar_menu_settings ) ) == true ) {
				Log.d( Shared.logTag, "Opening Settings activity..." )
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			}

			return@setOnMenuItemClickListener true
		}

		// Get the persistent settings - https://developer.android.com/training/data-storage/shared-preferences
		val sharedPreferences = getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE )
		Log.d( Shared.logTag, "Got shared preferences for '${ Shared.sharedPreferencesName }'" )

		// Get the settings - https://developer.android.com/training/data-storage/shared-preferences#ReadSharedPreference
		val instanceUrl = sharedPreferences.getString( "instanceUrl", null )
		val credentialsUsername = sharedPreferences.getString( "credentialsUsername", null )
		val credentialsPassword = sharedPreferences.getString( "credentialsPassword", null )
		Log.d( Shared.logTag, "Got settings ('${ instanceUrl }', '${ credentialsUsername }', '${ credentialsPassword }')" )

		// Switch to the servers activity if we aren't servers yet
		if ( instanceUrl.isNullOrBlank() || credentialsUsername.isNullOrBlank() || credentialsPassword.isNullOrBlank() ) {
			Log.d( Shared.logTag, "Not servers yet, switching to servers activity..." )
			startActivity( Intent( this, SetupActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			return
		}

		// Create a progress dialog
		val progressDialog = createProgressDialog( this, R.string.serversDialogProgressServersTitle, R.string.serversDialogProgressServersMessage ) {
			API.cancelQueue()
			showBriefMessage( this, R.string.serversToastServersCancel )
		}

		// Fetch the list of servers
		API.getServers( instanceUrl, credentialsUsername, credentialsPassword, { serversData ->

			// Hide the progress dialog
			progressDialog.dismiss()

			// Get the array
			val servers = serversData?.get( "servers" )?.asJsonArray
			Log.d( Shared.logTag, "Got '${ servers?.size() }' servers from API ('${ servers.toString() }')" )

			if ( servers != null ) {
				for ( _server in servers ) {
					val server = _server.asJsonObject

					val identifier = server.get( "identifier" )?.asString
					val jobName = server.get( "jobName" )?.asString
					val instanceAddress = server.get( "instanceAddress" )?.asString
					val lastScrape = server.get( "lastScrape" )?.asBigInteger
					val hostName = server.get( "hostName" )?.asString
					val operatingSystem = server.get( "operatingSystem" )?.asString
					val architecture = server.get( "architecture" )?.asString
					val version = server.get( "version" )?.asString
					val uptimeSeconds = server.get( "uptimeSeconds" )?.asBigInteger

					Log.d( Shared.logTag, "Identifier: '${ identifier }'" )
					Log.d( Shared.logTag, "Job Name: '${ jobName }'" )
					Log.d( Shared.logTag, "Instance Address: '${ instanceAddress }'" )
					Log.d( Shared.logTag, "Last Scrape: '${ lastScrape }'" )
					Log.d( Shared.logTag, "Host Name: '${ hostName }'" )
					Log.d( Shared.logTag, "Operating System: '${ operatingSystem }'" )
					Log.d( Shared.logTag, "Architecture: '${ architecture }'" )
					Log.d( Shared.logTag, "Version: '${ version }'" )
					Log.d( Shared.logTag, "Uptime: '${ uptimeSeconds }' seconds" )
				}
			} else {
				Log.e( Shared.logTag, "Servers array from API is null?!" )
				showBriefMessage( this, R.string.serversToastServersNull )
			}

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Failed to get servers from API due to '${ error }' (Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

			// Hide the progress dialog
			progressDialog.dismiss()

			when ( error ) {

				// Bad authentication
				is AuthFailureError -> when ( errorCode ) {
					ErrorCode.UnknownUser.code -> showBriefMessage( this, R.string.serversToastServersAuthenticationUnknownUser )
					ErrorCode.IncorrectPassword.code -> showBriefMessage( this, R.string.serversToastServersAuthenticationIncorrectPassword )
					else -> showBriefMessage( this, R.string.serversToastServersAuthenticationFailure )
				}

				// HTTP 4xx
				is ClientError -> when ( statusCode ) {
					404 -> showBriefMessage( this, R.string.serversToastServersNotFound )
					else -> showBriefMessage( this, R.string.serversToastServersClientFailure )
				}

				// HTTP 5xx
				is ServerError -> when ( statusCode ) {
					502 -> showBriefMessage( this, R.string.serversToastServersUnavailable )
					503 -> showBriefMessage( this, R.string.serversToastServersUnavailable )
					504 -> showBriefMessage( this, R.string.serversToastServersUnavailable )
					else -> showBriefMessage( this, R.string.serversToastServersServerFailure )
				}

				// No Internet connection, malformed domain
				is NoConnectionError -> showBriefMessage( this, R.string.serversToastServersNoConnection )
				is NetworkError -> showBriefMessage( this, R.string.serversToastServersNoConnection )

				// Connection timed out
				is TimeoutError -> showBriefMessage( this, R.string.serversToastServersTimeout )

				// Couldn't parse as JSON
				is ParseError -> showBriefMessage( this, R.string.serversToastServersParseFailure )

				// ¯\_(ツ)_/¯
				else -> showBriefMessage( this, R.string.serversToastServersFailure )

			}
		} )

		// Show the progress dialog
		progressDialog.show()

	}

	// Cancel pending HTTP requests when the activity is closed
	override fun onStop() {
		super.onStop()
		API.cancelQueue()
	}

}
