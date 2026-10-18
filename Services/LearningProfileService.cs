using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using learn_Assist.Models;

namespace learn_Assist.Services;

public sealed class LearningProfileService
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public LearningProfileService(string? databaseDirectory = null)
    {
        var directory = databaseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "learn-assist");

        Directory.CreateDirectory(directory);
        _databasePath = Path.Combine(directory, "learning.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS learning_profiles (
                user_id TEXT PRIMARY KEY NOT NULL,
                profile_type TEXT NOT NULL,
                learning_goal TEXT NOT NULL DEFAULT '',
                course TEXT NOT NULL DEFAULT '',
                level TEXT NOT NULL DEFAULT '',
                subjects_json TEXT NOT NULL DEFAULT '[]',
                career TEXT NOT NULL DEFAULT '',
                university TEXT NOT NULL DEFAULT '',
                semester TEXT NOT NULL DEFAULT '',
                professional_area TEXT NOT NULL DEFAULT '',
                preferred_language TEXT NOT NULL DEFAULT 'es',
                session_minutes INTEGER NOT NULL DEFAULT 15,
                is_completed INTEGER NOT NULL DEFAULT 0,
                is_learning_session_active INTEGER NOT NULL DEFAULT 0,
                learning_step INTEGER NOT NULL DEFAULT 1,
                is_learning_session_complete INTEGER NOT NULL DEFAULT 0,
                updated_at_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<LearningProfile?> LoadAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT profile_type, learning_goal, course, level, subjects_json,
                   career, university, semester, professional_area,
                   preferred_language, session_minutes, is_completed,
                   is_learning_session_active, learning_step,
                   is_learning_session_complete, updated_at_utc
            FROM learning_profiles
            WHERE user_id = $userId;
            """;
        command.Parameters.AddWithValue("$userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var subjects = JsonSerializer.Deserialize<string[]>(reader.GetString(4)) ?? [];
        var profileType = Enum.TryParse<LearningProfileType>(
            reader.GetString(0), ignoreCase: true, out var parsedType)
            ? parsedType
            : LearningProfileType.Personal;

        return new LearningProfile
        {
            UserId = userId,
            ProfileType = profileType,
            LearningGoal = reader.GetString(1),
            Course = reader.GetString(2),
            Level = reader.GetString(3),
            Subjects = [.. subjects],
            Career = reader.GetString(5),
            University = reader.GetString(6),
            Semester = reader.GetString(7),
            ProfessionalArea = reader.GetString(8),
            PreferredLanguage = reader.GetString(9),
            SessionMinutes = reader.GetInt32(10),
            IsCompleted = reader.GetInt32(11) != 0,
            IsLearningSessionActive = reader.GetInt32(12) != 0,
            LearningStep = reader.GetInt32(13),
            IsLearningSessionComplete = reader.GetInt32(14) != 0,
            UpdatedAtUtc = DateTime.TryParse(reader.GetString(15), out var updatedAt)
                ? updatedAt
                : DateTime.UtcNow,
        };
    }

    public async Task SaveAsync(LearningProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.UserId);

        profile.UpdatedAtUtc = DateTime.UtcNow;
        var subjectsJson = JsonSerializer.Serialize(profile.Subjects);

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO learning_profiles (
                user_id, profile_type, learning_goal, course, level, subjects_json,
                career, university, semester, professional_area, preferred_language,
                session_minutes, is_completed, is_learning_session_active,
                learning_step, is_learning_session_complete, updated_at_utc
            )
            VALUES (
                $userId, $profileType, $learningGoal, $course, $level, $subjectsJson,
                $career, $university, $semester, $professionalArea, $preferredLanguage,
                $sessionMinutes, $isCompleted, $isActive, $learningStep,
                $isComplete, $updatedAtUtc
            )
            ON CONFLICT(user_id) DO UPDATE SET
                profile_type = excluded.profile_type,
                learning_goal = excluded.learning_goal,
                course = excluded.course,
                level = excluded.level,
                subjects_json = excluded.subjects_json,
                career = excluded.career,
                university = excluded.university,
                semester = excluded.semester,
                professional_area = excluded.professional_area,
                preferred_language = excluded.preferred_language,
                session_minutes = excluded.session_minutes,
                is_completed = excluded.is_completed,
                is_learning_session_active = excluded.is_learning_session_active,
                learning_step = excluded.learning_step,
                is_learning_session_complete = excluded.is_learning_session_complete,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$userId", profile.UserId);
        command.Parameters.AddWithValue("$profileType", profile.ProfileType.ToString());
        command.Parameters.AddWithValue("$learningGoal", profile.LearningGoal.Trim());
        command.Parameters.AddWithValue("$course", profile.Course.Trim());
        command.Parameters.AddWithValue("$level", profile.Level.Trim());
        command.Parameters.AddWithValue("$subjectsJson", subjectsJson);
        command.Parameters.AddWithValue("$career", profile.Career.Trim());
        command.Parameters.AddWithValue("$university", profile.University.Trim());
        command.Parameters.AddWithValue("$semester", profile.Semester.Trim());
        command.Parameters.AddWithValue("$professionalArea", profile.ProfessionalArea.Trim());
        command.Parameters.AddWithValue("$preferredLanguage", profile.PreferredLanguage);
        command.Parameters.AddWithValue("$sessionMinutes", profile.SessionMinutes);
        command.Parameters.AddWithValue("$isCompleted", profile.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$isActive", profile.IsLearningSessionActive ? 1 : 0);
        command.Parameters.AddWithValue("$learningStep", profile.LearningStep);
        command.Parameters.AddWithValue("$isComplete", profile.IsLearningSessionComplete ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAtUtc", profile.UpdatedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM learning_profiles WHERE user_id = $userId;";
        command.Parameters.AddWithValue("$userId", userId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
