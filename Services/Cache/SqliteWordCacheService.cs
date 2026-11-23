using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VisualNovelDetect.Models;

namespace VisualNovelDetect.Services.Cache;

public class SqliteWordCacheService : IWordCacheService
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, (DictionaryResult?, TranslationResult?)> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _initialized;

    public SqliteWordCacheService()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualNovelDetect");

        Directory.CreateDirectory(appData);
        string dbPath = Path.Combine(appData, "cache.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _dbLock.WaitAsync();
        try
        {
            if (_initialized) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string createSql = @"
                CREATE TABLE IF NOT EXISTS WordCache (
                    Word TEXT NOT NULL,
                    TargetLanguage TEXT NOT NULL,
                    DictionaryJson TEXT,
                    TranslationJson TEXT,
                    LastAccessedUtc TEXT NOT NULL,
                    PRIMARY KEY (Word, TargetLanguage)
                );
                CREATE INDEX IF NOT EXISTS IX_WordCache_Word ON WordCache (Word);";

            using var cmd = new SqliteCommand(createSql, connection);
            await cmd.ExecuteNonQueryAsync();

            _initialized = true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<(DictionaryResult? Dict, TranslationResult? Trans)> GetCachedAsync(string word, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(word)) return (null, null);

        string cacheKey = $"{word.ToLowerInvariant()}_{targetLanguage.ToLowerInvariant()}";

        // Check in-memory cache first (0ms latency)
        if (_memoryCache.TryGetValue(cacheKey, out var memVal))
        {
            return memVal;
        }

        await EnsureInitializedAsync();

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string querySql = @"
                SELECT DictionaryJson, TranslationJson FROM WordCache
                WHERE Word = $word AND TargetLanguage = $targetLang LIMIT 1;";

            using var cmd = new SqliteCommand(querySql, connection);
            cmd.Parameters.AddWithValue("$word", word.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$targetLang", targetLanguage.ToLowerInvariant());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string? dictJson = reader.IsDBNull(0) ? null : reader.GetString(0);
                string? transJson = reader.IsDBNull(1) ? null : reader.GetString(1);

                DictionaryResult? dict = null;
                TranslationResult? trans = null;

                if (!string.IsNullOrEmpty(dictJson))
                {
                    try { dict = JsonSerializer.Deserialize<DictionaryResult>(dictJson); } catch { }
                }

                if (!string.IsNullOrEmpty(transJson))
                {
                    try { trans = JsonSerializer.Deserialize<TranslationResult>(transJson); } catch { }
                }

                var pair = (dict, trans);
                _memoryCache[cacheKey] = pair;
                return pair;
            }
        }
        catch
        {
            // Ignore DB errors
        }
        finally
        {
            _dbLock.Release();
        }

        return (null, null);
    }

    public async Task SaveAsync(string word, string targetLanguage, DictionaryResult? dict, TranslationResult? trans)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        string cacheKey = $"{word.ToLowerInvariant()}_{targetLanguage.ToLowerInvariant()}";
        _memoryCache[cacheKey] = (dict, trans);

        await EnsureInitializedAsync();

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string insertSql = @"
                INSERT INTO WordCache (Word, TargetLanguage, DictionaryJson, TranslationJson, LastAccessedUtc)
                VALUES ($word, $targetLang, $dictJson, $transJson, $now)
                ON CONFLICT(Word, TargetLanguage) DO UPDATE SET
                    DictionaryJson = excluded.DictionaryJson,
                    TranslationJson = excluded.TranslationJson,
                    LastAccessedUtc = excluded.LastAccessedUtc;";

            using var cmd = new SqliteCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("$word", word.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$targetLang", targetLanguage.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$dictJson", dict != null ? JsonSerializer.Serialize(dict) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$transJson", trans != null ? JsonSerializer.Serialize(trans) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore DB errors
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task ClearCacheAsync()
    {
        _memoryCache.Clear();
        await EnsureInitializedAsync();

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = new SqliteCommand("DELETE FROM WordCache;", connection);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore DB errors
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> GetCachedCountAsync()
    {
        await EnsureInitializedAsync();

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM WordCache;", connection);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch
        {
            return 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }
}
