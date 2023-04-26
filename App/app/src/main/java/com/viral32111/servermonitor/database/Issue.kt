package com.viral32111.servermonitor.database

import androidx.room.ColumnInfo
import androidx.room.Dao
import androidx.room.Delete
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Update

// https://developer.android.com/training/data-storage/room

// https://developer.android.com/training/data-storage/room/defining-data
@Entity( tableName = "issueHistory" )
data class Issue(
	@PrimaryKey( autoGenerate = true ) val identifier: Long = 0,
	@ColumnInfo( name = "startedAt", defaultValue = "CURRENT_TIMESTAMP" ) val startedAt: Long = System.currentTimeMillis(),
	@ColumnInfo( name = "finishedAt", defaultValue = "NULL" ) val finishedAt: Long? = null,
	@ColumnInfo( name = "totalCount", defaultValue = "1" ) val totalCount: Long = 1
)

// https://developer.android.com/training/data-storage/room/accessing-data
@Dao
interface IssueDAO {
	@Query( "SELECT * from issueHistory" )
	suspend fun fetchAll(): List<Issue>

	@Query( "SELECT * FROM issueHistory WHERE identifier = :identifier" )
	suspend fun fetchByIdentifier( identifier: Long ): Issue

	@Query( "SELECT * FROM issueHistory ORDER BY startedAt DESC LIMIT 1" )
	suspend fun fetchLatest(): Issue

	@Query( "SELECT * FROM issueHistory WHERE finishedAt IS NULL ORDER BY startedAt DESC LIMIT 1" )
	suspend fun fetchOngoing(): Issue?

	@Insert( onConflict = OnConflictStrategy.REPLACE )
	suspend fun create( issue: Issue = Issue() ): Long

	@Query( "UPDATE issueHistory SET finishedAt = :finishedAt WHERE identifier = :identifier" )
	suspend fun updateFinishedAtByIdentifier( identifier: Long, finishedAt: Long? = System.currentTimeMillis() )

	@Query( "UPDATE issueHistory SET totalCount = totalCount + :incrementBy WHERE identifier = :identifier" )
	suspend fun incrementTotalCountByIdentifier( identifier: Long, incrementBy: Long = 1 )

	@Update
	suspend fun update( issue: Issue )

	@Delete
	suspend fun remove( issue: Issue )

	@Query( "DELETE FROM issueHistory" )
	suspend fun removeAll()
}
