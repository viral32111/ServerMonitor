package com.viral32111.servermonitor.database

import androidx.room.ColumnInfo
import androidx.room.Dao
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query

// https://developer.android.com/training/data-storage/room

// https://developer.android.com/training/data-storage/room/defining-data
@Entity( tableName = "issueHistory" )
data class Issue(
	@PrimaryKey( autoGenerate = true ) var identifier: Long = 0,
	@ColumnInfo( name = "startedAt", defaultValue = "CURRENT_TIMESTAMP" ) var startedAt: Long = System.currentTimeMillis(),
	@ColumnInfo( name = "finishedAt", defaultValue = "NULL" ) var finishedAt: Long? = null,
	@ColumnInfo( name = "totalCount", defaultValue = "1" ) var totalCount: Long = 1
)

// https://developer.android.com/training/data-storage/room/accessing-data
@Dao
interface IssueDAO {

	// Fetches all issues
	@Query( "SELECT * FROM issueHistory" )
	suspend fun fetchAll(): List<Issue>

	// Fetches a specific issue, if it exists
	@Query( "SELECT * FROM issueHistory WHERE identifier = :identifier" )
	suspend fun fetchByIdentifier( identifier: Long ): Issue?

	// Fetches all issues after a given timestamp, defaults to start of the current day - https://stackoverflow.com/a/68294693
	@Query( "SELECT * FROM issueHistory WHERE startedAt >= :timestamp" )
	suspend fun fetchAfterStartedAtDate( timestamp: Long ): List<Issue>

	// Fetches the latest issue, if any
	@Query( "SELECT * FROM issueHistory ORDER BY startedAt DESC LIMIT 1" )
	suspend fun fetchLatest(): Issue?

	// Fetches the current on-going issue, if any
	@Query( "SELECT * FROM issueHistory WHERE finishedAt IS NULL ORDER BY startedAt DESC LIMIT 1" )
	suspend fun fetchOngoing(): Issue?

	@Insert( onConflict = OnConflictStrategy.REPLACE )
	suspend fun create( issue: Issue ): Long

	// Finishes any on-going issues
	@Query( "UPDATE issueHistory SET finishedAt = :finishedAt WHERE finishedAt IS NULL" )
	suspend fun updateFinishedAt( finishedAt: Long )

	// Finishes an on-going issue
	@Query( "UPDATE issueHistory SET finishedAt = :finishedAt WHERE identifier = :identifier" )
	suspend fun updateFinishedAtByIdentifier( identifier: Long, finishedAt: Long? )

	// Increments the counter on an issue
	@Query( "UPDATE issueHistory SET totalCount = totalCount + :incrementBy WHERE identifier = :identifier" )
	suspend fun incrementTotalCountByIdentifier( identifier: Long, incrementBy: Long )

	// Removes all issues
	@Query( "DELETE FROM issueHistory" )
	suspend fun removeAll()

}
