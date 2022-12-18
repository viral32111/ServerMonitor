package com.viral32111.servermonitor

import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import androidx.appcompat.app.ActionBar
import androidx.appcompat.content.res.AppCompatResources
import com.google.android.material.appbar.MaterialToolbar

class SettingsActivity : AppCompatActivity() {

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
		val instanceUrlEditText = findViewById<EditText>( R.id.settingsInstanceUrlEditText )
		val credentialsUsernameEditText = findViewById<EditText>( R.id.settingsCredentialsUsernameEditText )
		val credentialsPasswordEditText = findViewById<EditText>( R.id.settingsCredentialsPasswordEditText )
		val saveButton = findViewById<Button>( R.id.settingsSaveButton )

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

		// When the the save button is pressed...
		saveButton.setOnClickListener {

			// TODO: Get all values from UI inputs
			// TODO: Validate all those values
			// TODO: Save those values to shared preferences

			// Return to last activity
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )

		}

	}

}
