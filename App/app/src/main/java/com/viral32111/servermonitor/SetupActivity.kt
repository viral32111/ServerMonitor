package com.viral32111.servermonitor

import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.view.Menu
import android.widget.Toolbar
import androidx.appcompat.app.ActionBar

class SetupActivity : AppCompatActivity() {

	override fun onCreate( savedInstanceState: Bundle? ) {
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_setup )

		/* ----------------------------- */

		/*actionBar?.title = "Hello World 1"
		actionBar?.subtitle = "Hello World 2"

		actionBar?.setDisplayShowTitleEnabled( true )
		actionBar?.setDisplayShowHomeEnabled( true )
		actionBar?.setDisplayShowCustomEnabled( true )

		actionBar?.elevation = 12.0f

		actionBar?.show()*/

		/* ----------------------------- */

		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar );


		//supportActionBar?.title = getString( R.string.setup_actionbar_title )
		//supportActionBar?.subtitle = "Hello World 3"

		//supportActionBar?.setDisplayShowTitleEnabled( true )
		//supportActionBar?.setDisplayShowHomeEnabled( true )
		//supportActionBar?.setDisplayShowCustomEnabled( true )

		//supportActionBar?.elevation = 8.0f

	}

	/*override fun onCreateOptionsMenu( menu: Menu? ): Boolean {
		//return super.onCreateOptionsMenu( menu )
		menuInflater.inflate( R.menu.actionbar_menu, menu )
		return true
	}*/

}
