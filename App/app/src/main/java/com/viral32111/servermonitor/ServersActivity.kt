package com.viral32111.servermonitor

import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import androidx.appcompat.app.ActionBar
import com.google.android.material.appbar.MaterialToolbar

class ServersActivity : AppCompatActivity() {

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_servers )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )

		// Get all UI controls
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.serversActionBarTitle )
		materialToolbar?.isTitleCentered = true

		// Open settings when its action bar menu item is clicked
		materialToolbar?.setOnMenuItemClickListener { menuItem ->
			if ( menuItem.title?.equals( getString( R.string.action_bar_menu_settings ) ) == true ) {
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			}

			return@setOnMenuItemClickListener true
		}

	}

}
