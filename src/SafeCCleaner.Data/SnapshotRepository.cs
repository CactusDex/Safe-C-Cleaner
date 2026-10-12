using Microsoft.Data.Sqlite;
using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Data;

public sealed class SnapshotRepository
{
    private readonly string _connectionString;

    public SnapshotRepository(string? dbPath = null)
    {
        dbPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SafeCCleaner", "history.db");

        var directory = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                captured_at TEXT NOT NULL,
                drive_root TEXT NOT NULL,
                total_bytes INTEGER NOT NULL,
                free_bytes INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS folder_snapshots (
                snapshot_id INTEGER NOT NULL,
                folder_key TEXT NOT NULL,
                display_name TEXT NOT NULL,
                folder_path TEXT NOT NULL,
                bytes INTEGER NOT NULL,
                FOREIGN KEY(snapshot_id) REFERENCES snapshots(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_folder_snapshots_snapshot_id
                ON folder_snapshots(snapshot_id);

            CREATE TABLE IF NOT EXISTS cleanup_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at TEXT NOT NULL,
                completed_at TEXT NOT NULL,
                operation_name TEXT NOT NULL,
                mode TEXT NOT NULL,
                success INTEGER NOT NULL,
                before_free_bytes INTEGER NOT NULL,
                after_free_bytes INTEGER NOT NULL,
                reported_freed_bytes INTEGER NOT NULL,
                skipped_files INTEGER NOT NULL,
                summary TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS cleanup_run_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id INTEGER NOT NULL,
                rule_id TEXT NOT NULL,
                display_name TEXT NOT NULL,
                risk INTEGER NOT NULL,
                success INTEGER NOT NULL,
                freed_bytes INTEGER NOT NULL,
                deleted_files INTEGER NOT NULL,
                skipped_files INTEGER NOT NULL,
                message TEXT NULL,
                FOREIGN KEY(run_id) REFERENCES cleanup_runs(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_cleanup_runs_completed_at
                ON cleanup_runs(completed_at DESC);
            CREATE INDEX IF NOT EXISTS ix_cleanup_run_items_run_id
                ON cleanup_run_items(run_id);
            """;
        await command.ExecuteNonQueryAsync();
        await TrimCleanupHistoryAsync(connection);
    }

    public async Task SaveAsync(DiskSnapshot snapshot)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var tx = connection.BeginTransaction();

        var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO snapshots(captured_at, drive_root, total_bytes, free_bytes)
            VALUES ($captured, $drive, $total, $free);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$captured", snapshot.CapturedAt.ToString("O"));
        insert.Parameters.AddWithValue("$drive", snapshot.DriveRoot);
        insert.Parameters.AddWithValue("$total", snapshot.TotalBytes);
        insert.Parameters.AddWithValue("$free", snapshot.FreeBytes);
        var id = (long)(await insert.ExecuteScalarAsync() ?? 0L);

        foreach (var folder in snapshot.Folders)
        {
            var folderInsert = connection.CreateCommand();
            folderInsert.Transaction = tx;
            folderInsert.CommandText = """
                INSERT INTO folder_snapshots(snapshot_id, folder_key, display_name, folder_path, bytes)
                VALUES ($sid, $key, $name, $path, $bytes);
                """;
            folderInsert.Parameters.AddWithValue("$sid", id);
            folderInsert.Parameters.AddWithValue("$key", folder.Key);
            folderInsert.Parameters.AddWithValue("$name", folder.DisplayName);
            folderInsert.Parameters.AddWithValue("$path", folder.Path);
            folderInsert.Parameters.AddWithValue("$bytes", folder.Bytes);
            await folderInsert.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }

    public async Task<DiskSnapshot?> GetLatestAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, captured_at, drive_root, total_bytes, free_bytes
            FROM snapshots
            ORDER BY captured_at DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var id = reader.GetInt64(0);
        var captured = DateTimeOffset.Parse(reader.GetString(1));
        var drive = reader.GetString(2);
        var total = reader.GetInt64(3);
        var free = reader.GetInt64(4);
        await reader.DisposeAsync();

        var folders = new List<FolderUsage>();
        var folderCommand = connection.CreateCommand();
        folderCommand.CommandText = """
            SELECT folder_key, display_name, folder_path, bytes
            FROM folder_snapshots
            WHERE snapshot_id = $id;
            """;
        folderCommand.Parameters.AddWithValue("$id", id);

        await using var folderReader = await folderCommand.ExecuteReaderAsync();
        while (await folderReader.ReadAsync())
        {
            folders.Add(new FolderUsage(
                folderReader.GetString(0),
                folderReader.GetString(1),
                folderReader.GetString(2),
                folderReader.GetInt64(3)));
        }

        return new DiskSnapshot(captured, drive, total, free, folders);
    }

    public async Task<DiskSnapshot?> GetPreviousAsync(DateTimeOffset before)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, captured_at, drive_root, total_bytes, free_bytes
            FROM snapshots
            WHERE captured_at < $before
            ORDER BY captured_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$before", before.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var id = reader.GetInt64(0);
        var captured = DateTimeOffset.Parse(reader.GetString(1));
        var drive = reader.GetString(2);
        var total = reader.GetInt64(3);
        var free = reader.GetInt64(4);
        await reader.DisposeAsync();

        var folders = new List<FolderUsage>();
        var folderCommand = connection.CreateCommand();
        folderCommand.CommandText = """
            SELECT folder_key, display_name, folder_path, bytes
            FROM folder_snapshots
            WHERE snapshot_id = $id;
            """;
        folderCommand.Parameters.AddWithValue("$id", id);

        await using var folderReader = await folderCommand.ExecuteReaderAsync();
        while (await folderReader.ReadAsync())
        {
            folders.Add(new FolderUsage(
                folderReader.GetString(0),
                folderReader.GetString(1),
                folderReader.GetString(2),
                folderReader.GetInt64(3)));
        }

        return new DiskSnapshot(captured, drive, total, free, folders);
    }
    public async Task<DiskSnapshot?> GetAtOrBeforeAsync(DateTimeOffset atOrBefore)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, captured_at, drive_root, total_bytes, free_bytes
            FROM snapshots
            WHERE captured_at <= $at
            ORDER BY captured_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$at", atOrBefore.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var id = reader.GetInt64(0);
        var captured = DateTimeOffset.Parse(reader.GetString(1));
        var drive = reader.GetString(2);
        var total = reader.GetInt64(3);
        var free = reader.GetInt64(4);
        await reader.DisposeAsync();

        var folders = new List<FolderUsage>();
        var folderCommand = connection.CreateCommand();
        folderCommand.CommandText = """
            SELECT folder_key, display_name, folder_path, bytes
            FROM folder_snapshots
            WHERE snapshot_id = $id;
            """;
        folderCommand.Parameters.AddWithValue("$id", id);

        await using var folderReader = await folderCommand.ExecuteReaderAsync();
        while (await folderReader.ReadAsync())
        {
            folders.Add(new FolderUsage(
                folderReader.GetString(0),
                folderReader.GetString(1),
                folderReader.GetString(2),
                folderReader.GetInt64(3)));
        }

        return new DiskSnapshot(captured, drive, total, free, folders);
    }

    public async Task<DiskSnapshot?> GetNearestAsync(DateTimeOffset target, TimeSpan tolerance)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var min = target - tolerance;
        var max = target + tolerance;
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, captured_at, drive_root, total_bytes, free_bytes
            FROM snapshots
            WHERE captured_at >= $min AND captured_at <= $max
            ORDER BY ABS(julianday(captured_at) - julianday($target)) ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$min", min.ToString("O"));
        command.Parameters.AddWithValue("$max", max.ToString("O"));
        command.Parameters.AddWithValue("$target", target.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var id = reader.GetInt64(0);
        var captured = DateTimeOffset.Parse(reader.GetString(1));
        var drive = reader.GetString(2);
        var total = reader.GetInt64(3);
        var free = reader.GetInt64(4);
        await reader.DisposeAsync();

        var folders = new List<FolderUsage>();
        var folderCommand = connection.CreateCommand();
        folderCommand.CommandText = """
            SELECT folder_key, display_name, folder_path, bytes
            FROM folder_snapshots
            WHERE snapshot_id = $id;
            """;
        folderCommand.Parameters.AddWithValue("$id", id);
        await using var folderReader = await folderCommand.ExecuteReaderAsync();
        while (await folderReader.ReadAsync())
        {
            folders.Add(new FolderUsage(folderReader.GetString(0), folderReader.GetString(1), folderReader.GetString(2), folderReader.GetInt64(3)));
        }

        return new DiskSnapshot(captured, drive, total, free, folders);
    }

    public async Task<long> SaveCleanupRunAsync(CleanupHistoryRecord record)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var tx = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO cleanup_runs(
                started_at, completed_at, operation_name, mode, success,
                before_free_bytes, after_free_bytes, reported_freed_bytes,
                skipped_files, summary)
            VALUES (
                $started, $completed, $name, $mode, $success,
                $before, $after, $reported, $skipped, $summary);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$started", record.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$completed", record.CompletedAt.ToString("O"));
        command.Parameters.AddWithValue("$name", record.OperationName);
        command.Parameters.AddWithValue("$mode", record.Mode);
        command.Parameters.AddWithValue("$success", record.Success ? 1 : 0);
        command.Parameters.AddWithValue("$before", record.BeforeFreeBytes);
        command.Parameters.AddWithValue("$after", record.AfterFreeBytes);
        command.Parameters.AddWithValue("$reported", record.ReportedFreedBytes);
        command.Parameters.AddWithValue("$skipped", record.SkippedFiles);
        command.Parameters.AddWithValue("$summary", (object?)record.Summary ?? DBNull.Value);
        var id = (long)(await command.ExecuteScalarAsync() ?? 0L);

        foreach (var item in record.Items)
        {
            var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = tx;
            itemCommand.CommandText = """
                INSERT INTO cleanup_run_items(
                    run_id, rule_id, display_name, risk, success,
                    freed_bytes, deleted_files, skipped_files, message)
                VALUES (
                    $run, $rule, $name, $risk, $success,
                    $freed, $deleted, $skipped, $message);
                """;
            itemCommand.Parameters.AddWithValue("$run", id);
            itemCommand.Parameters.AddWithValue("$rule", item.RuleId);
            itemCommand.Parameters.AddWithValue("$name", item.DisplayName);
            itemCommand.Parameters.AddWithValue("$risk", (int)item.Risk);
            itemCommand.Parameters.AddWithValue("$success", item.Success ? 1 : 0);
            itemCommand.Parameters.AddWithValue("$freed", item.FreedBytes);
            itemCommand.Parameters.AddWithValue("$deleted", item.DeletedFiles);
            itemCommand.Parameters.AddWithValue("$skipped", item.SkippedFiles);
            itemCommand.Parameters.AddWithValue("$message", (object?)item.Message ?? DBNull.Value);
            await itemCommand.ExecuteNonQueryAsync();
        }

        await TrimCleanupHistoryAsync(connection, tx);
        tx.Commit();
        return id;
    }

    public async Task<IReadOnlyList<CleanupHistoryRecord>> GetCleanupRunsAsync(int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 500);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var runs = new List<(long Id, DateTimeOffset Started, DateTimeOffset Completed, string Name, string Mode, bool Success, long Before, long After, long Reported, long Skipped, string? Summary)>();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at, completed_at, operation_name, mode, success,
                   before_free_bytes, after_free_bytes, reported_freed_bytes,
                   skipped_files, summary
            FROM cleanup_runs
            ORDER BY completed_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                runs.Add((
                    reader.GetInt64(0),
                    DateTimeOffset.Parse(reader.GetString(1)),
                    DateTimeOffset.Parse(reader.GetString(2)),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetInt64(5) != 0,
                    reader.GetInt64(6),
                    reader.GetInt64(7),
                    reader.GetInt64(8),
                    reader.GetInt64(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10)));
            }
        }

        var result = new List<CleanupHistoryRecord>(runs.Count);
        foreach (var run in runs)
        {
            var items = new List<CleanupHistoryItem>();
            var itemCommand = connection.CreateCommand();
            itemCommand.CommandText = """
                SELECT rule_id, display_name, risk, success, freed_bytes,
                       deleted_files, skipped_files, message
                FROM cleanup_run_items
                WHERE run_id = $run
                ORDER BY id;
                """;
            itemCommand.Parameters.AddWithValue("$run", run.Id);
            await using var itemReader = await itemCommand.ExecuteReaderAsync();
            while (await itemReader.ReadAsync())
            {
                items.Add(new CleanupHistoryItem(
                    itemReader.GetString(0),
                    itemReader.GetString(1),
                    (CleanupRisk)itemReader.GetInt32(2),
                    itemReader.GetInt64(3) != 0,
                    itemReader.GetInt64(4),
                    itemReader.GetInt64(5),
                    itemReader.GetInt64(6),
                    itemReader.IsDBNull(7) ? null : itemReader.GetString(7)));
            }

            result.Add(new CleanupHistoryRecord(
                run.Id, run.Started, run.Completed, run.Name, run.Mode, run.Success,
                run.Before, run.After, run.Reported, run.Skipped, run.Summary, items));
        }

        return result;
    }


    private static async Task TrimCleanupHistoryAsync(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        // Keep the database itself bounded, not only the UI list. Remove child rows first
        // because SQLite foreign-key enforcement is not assumed for existing databases.
        var deleteItems = connection.CreateCommand();
        deleteItems.Transaction = transaction;
        deleteItems.CommandText = """
            DELETE FROM cleanup_run_items
            WHERE run_id IN (
                SELECT id
                FROM cleanup_runs
                ORDER BY completed_at DESC, id DESC
                LIMIT -1 OFFSET 10
            );
            """;
        await deleteItems.ExecuteNonQueryAsync();

        var deleteRuns = connection.CreateCommand();
        deleteRuns.Transaction = transaction;
        deleteRuns.CommandText = """
            DELETE FROM cleanup_runs
            WHERE id IN (
                SELECT id
                FROM cleanup_runs
                ORDER BY completed_at DESC, id DESC
                LIMIT -1 OFFSET 10
            );
            """;
        await deleteRuns.ExecuteNonQueryAsync();
    }

}
