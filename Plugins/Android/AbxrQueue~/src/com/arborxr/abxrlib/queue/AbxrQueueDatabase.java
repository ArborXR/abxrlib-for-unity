package com.arborxr.abxrlib.queue;

import android.content.Context;
import android.database.Cursor;
import android.database.DatabaseUtils;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import android.database.sqlite.SQLiteStatement;

import java.util.ArrayList;

/**
 * The offline record queue's store on Android headsets (offline RFC D13). It uses the OS's built-in SQLite, so the SDK
 * adds no library the host app might also ship. One database in app-private storage: a summary row per session and a
 * records table keyed by session and stream. Record ids only ever grow (AUTOINCREMENT), so a session's acknowledged
 * position is the last id the backend answered for. Called from C# (SqliteQueueStore) on Unity's main thread.
 */
public final class AbxrQueueDatabase extends SQLiteOpenHelper {
    static final int SCHEMA_VERSION = 1;
    private static final int STREAMS = 3;

    private static AbxrQueueDatabase shared;
    private static String sharedName;

    private final SQLiteDatabase db;

    private AbxrQueueDatabase(Context context, String name) {
        super(context.getApplicationContext(), name, null, SCHEMA_VERSION);
        // Write-ahead logging: a commit survives the app being killed, and the default sync mode keeps a commit cheap.
        setWriteAheadLoggingEnabled(true);
        // A newer SDK's schema throws here (SQLiteOpenHelper won't downgrade), and its database is left as it is.
        db = getWritableDatabase();
    }

    /**
     * Opens (or creates) the queue database, once per process: a transport created later in the same run shares it.
     * Throws when it can't be used, and the SDK falls back to its file queue.
     */
    public static synchronized AbxrQueueDatabase open(Context context, String name) {
        if (shared == null || !name.equals(sharedName)) {
            shared = new AbxrQueueDatabase(context, name);
            sharedName = name;
        }
        return shared;
    }

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE sessions (file_key TEXT PRIMARY KEY, summary TEXT NOT NULL)");
        db.execSQL("CREATE TABLE records (id INTEGER PRIMARY KEY AUTOINCREMENT, file_key TEXT NOT NULL, "
                + "stream INTEGER NOT NULL, line TEXT NOT NULL)");
        db.execSQL("CREATE INDEX records_by_stream ON records (file_key, stream, id)");
    }

    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        // Version 1 is the first schema. A later version migrates older ones here.
    }

    /** Every session's key and summary, as key, summary, key, summary, ... */
    public String[] readSummaries() {
        ArrayList<String> out = new ArrayList<>();
        try (Cursor c = db.rawQuery("SELECT file_key, summary FROM sessions", null)) {
            while (c.moveToNext()) {
                out.add(c.getString(0));
                out.add(c.getString(1));
            }
        }
        return out.toArray(new String[0]);
    }

    /** Deletes records that belong to no session. */
    public void deleteOrphans() {
        db.execSQL("DELETE FROM records WHERE file_key NOT IN (SELECT file_key FROM sessions)");
    }

    public int countAfter(String key, int stream, long after) {
        return (int) DatabaseUtils.longForQuery(db,
                "SELECT COUNT(*) FROM records WHERE file_key = ? AND stream = ? AND id > ?",
                new String[] { key, Integer.toString(stream), Long.toString(after) });
    }

    /**
     * Appends records and, when summary isn't empty, writes the session's summary, in one transaction: a crash or a
     * failure keeps all of it or none. Returns the characters added per stream.
     */
    public long[] append(String key, int[] streams, String[] lines, String summary) {
        long[] added = new long[STREAMS];
        db.beginTransaction();
        try {
            SQLiteStatement insert = db.compileStatement("INSERT INTO records (file_key, stream, line) VALUES (?, ?, ?)");
            try {
                for (int i = 0; i < lines.length; i++) {
                    insert.bindString(1, key);
                    insert.bindLong(2, streams[i]);
                    insert.bindString(3, lines[i]);
                    insert.executeInsert();
                    insert.clearBindings();
                    added[streams[i]] += lines[i].length();
                }
            } finally {
                insert.close();
            }
            if (summary != null && !summary.isEmpty()) putSummary(key, summary);
            db.setTransactionSuccessful();
        } finally {
            db.endTransaction();
        }
        return added;
    }

    /**
     * Up to maxRows records after a position, oldest first, as id, line, id, line, ... It stops early once maxChars
     * are read (always returning at least one record), so one query result stays well inside the CursorWindow.
     */
    public String[] read(String key, int stream, long after, int maxRows, int maxChars) {
        ArrayList<String> out = new ArrayList<>();
        long chars = 0;
        try (Cursor c = db.rawQuery(
                "SELECT id, line FROM records WHERE file_key = ? AND stream = ? AND id > ? ORDER BY id LIMIT ?",
                new String[] { key, Integer.toString(stream), Long.toString(after), Integer.toString(maxRows) })) {
            while (c.moveToNext()) {
                String line = c.getString(1);
                if (!out.isEmpty() && chars + line.length() > maxChars) break;
                out.add(Long.toString(c.getLong(0)));
                out.add(line);
                chars += line.length();
            }
        }
        return out.toArray(new String[0]);
    }

    /** Writes a session's summary. durable checkpoints the log, so it survives a power cut too. Returns the change in characters. */
    public long writeSummary(String key, String summary, boolean durable) {
        long before = summarySize(key);
        putSummary(key, summary);
        if (durable) checkpoint();
        return summary.length() - before;
    }

    /** Deletes a stream's records up to a position, once the backend has answered for them. Returns the characters freed. */
    public long acknowledged(String key, int stream, long through) {
        String[] args = { key, Integer.toString(stream), Long.toString(through) };
        long freed = DatabaseUtils.longForQuery(db,
                "SELECT COALESCE(SUM(LENGTH(line)), 0) FROM records WHERE file_key = ? AND stream = ? AND id <= ?", args);
        db.execSQL("DELETE FROM records WHERE file_key = ? AND stream = ? AND id <= ?", args);
        return freed;
    }

    public long deleteStream(String key, int stream) {
        long freed = streamSize(key, stream);
        db.execSQL("DELETE FROM records WHERE file_key = ? AND stream = ?", new Object[] { key, stream });
        return freed;
    }

    public long deleteSession(String key) {
        long freed = summarySize(key);
        for (int s = 0; s < STREAMS; s++) freed += streamSize(key, s);
        db.beginTransaction();
        try {
            db.execSQL("DELETE FROM records WHERE file_key = ?", new Object[] { key });
            db.execSQL("DELETE FROM sessions WHERE file_key = ?", new Object[] { key });
            db.setTransactionSuccessful();
        } finally {
            db.endTransaction();
        }
        return freed;
    }

    public long streamSize(String key, int stream) {
        return DatabaseUtils.longForQuery(db,
                "SELECT COALESCE(SUM(LENGTH(line)), 0) FROM records WHERE file_key = ? AND stream = ?",
                new String[] { key, Integer.toString(stream) });
    }

    public long summarySize(String key) {
        return DatabaseUtils.longForQuery(db,
                "SELECT COALESCE(SUM(LENGTH(summary)), 0) FROM sessions WHERE file_key = ?", new String[] { key });
    }

    /** For tests: the schema version stored in the database file. */
    public int schemaVersion() {
        return db.getVersion();
    }

    private void putSummary(String key, String summary) {
        db.execSQL("INSERT OR REPLACE INTO sessions (file_key, summary) VALUES (?, ?)", new Object[] { key, summary });
    }

    private void checkpoint() {
        try (Cursor c = db.rawQuery("PRAGMA wal_checkpoint(FULL)", null)) {
            c.moveToFirst();
        }
    }
}
