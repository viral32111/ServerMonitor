package com.viral32111.servermonitor

import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle

class SetupActivity : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_setup)

        actionBar?.title = "Setup"
    }
}
