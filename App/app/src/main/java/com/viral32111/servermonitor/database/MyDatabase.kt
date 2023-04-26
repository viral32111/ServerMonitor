package com.viral32111.servermonitor.database

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import com.viral32111.servermonitor.Shared

// https://developer.android.com/training/data-storage/room#database

@Database( entities = [ Issue::class ], version = 3, exportSchema = false )
abstract class MyDatabase : RoomDatabase() {
	abstract fun issueHistory(): IssueDAO
}

// Helper to get or initialise (if not yet created) the database
fun initialiseDatabase( applicationContext: Context) = Room.databaseBuilder( applicationContext, MyDatabase::class.java, Shared.roomDatabaseName )
	.fallbackToDestructiveMigration() // Allow nuking the previous database when the schema version changes
	.build()
