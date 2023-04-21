package com.viral32111.servermonitor.helper

import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Locale

// https://stackoverflow.com/a/51394768
fun getCurrentDateTime( format: String ): String = SimpleDateFormat( format, Locale.getDefault() ).format( Calendar.getInstance().time )
