using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using learn_Assist.Models;
using Microsoft.Data.Sqlite;

namespace learn_Assist.Services;

public sealed class UserDatabase
{
    private readonly string _databasePath;
    private readonly object _lock = new();

    public UserDatabase(string? databasePath = null)
    {
        SQLitePCL.Batteries_V2.Init();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "learn-assist");
        Directory.CreateDirectory(directory);
        _databasePath = databasePath ?? Path.Combine(directory, "learn_assist.db");
        Initialize();
    }

    public static UserDatabase Current { get; } = new();

    public UserRecord? FindUserByEmail(string email)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, email, name, password_hash, has_password, created_at
            FROM users WHERE email = $email COLLATE NOCASE LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", email);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public UserRecord? FindUserById(string userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, email, name, password_hash, has_password, created_at
            FROM users WHERE id = $id LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public void AddUser(UserRecord user)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (id, email, name, password_hash, has_password, created_at)
            VALUES ($id, $email, $name, $password_hash, $has_password, $created_at);
            """;
        AddUserParameters(command, user);
        command.ExecuteNonQuery();
    }

    public RememberedSession? LoadRememberedSession()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT user_id, email, session_id FROM sessions WHERE id = 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new RememberedSession(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    public void SaveRememberedSession(RememberedSession session)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions (id, user_id, email, session_id)
            VALUES (1, $user_id, $email, $session_id)
            ON CONFLICT(id) DO UPDATE SET
                user_id = excluded.user_id,
                email = excluded.email,
                session_id = excluded.session_id;
            """;
        command.Parameters.AddWithValue("$user_id", session.UserId);
        command.Parameters.AddWithValue("$email", session.Email);
        command.Parameters.AddWithValue("$session_id", session.SessionId);
        command.ExecuteNonQuery();
    }

    public void DeleteRememberedSession()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = 1;";
        command.ExecuteNonQuery();
    }

    public string? LoadTutorialState(string userKey)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT seen FROM tutorial_state WHERE user_key = $user_key;";
        command.Parameters.AddWithValue("$user_key", userKey);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt32(value).ToString();
    }

    public void MarkTutorialSeen(string userKey)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tutorial_state (user_key, seen)
            VALUES ($user_key, 1)
            ON CONFLICT(user_key) DO UPDATE SET seen = 1;
            """;
        command.Parameters.AddWithValue("$user_key", userKey);
        command.ExecuteNonQuery();
    }

    public void SaveLearningProfile(LearningProfileRecord profile)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO learning_profiles (
                user_id, profile_type, learning_goal, course, level, subjects,
                career, university, semester, semester_content, professional_area, preferred_language,
                session_minutes, is_completed, is_learning_session_active,
                learning_step, is_learning_session_complete, updated_at_utc
            ) VALUES (
                $user_id, $profile_type, $learning_goal, $course, $level, $subjects,
                $career, $university, $semester, $semester_content, $professional_area, $preferred_language,
                $session_minutes, $is_completed, $is_learning_session_active,
                $learning_step, $is_learning_session_complete, $updated_at_utc
            )
            ON CONFLICT(user_id) DO UPDATE SET
                profile_type = excluded.profile_type,
                learning_goal = excluded.learning_goal,
                course = excluded.course,
                level = excluded.level,
                subjects = excluded.subjects,
                career = excluded.career,
                university = excluded.university,
                semester = excluded.semester,
                semester_content = excluded.semester_content,
                professional_area = excluded.professional_area,
                preferred_language = excluded.preferred_language,
                session_minutes = excluded.session_minutes,
                is_completed = excluded.is_completed,
                is_learning_session_active = excluded.is_learning_session_active,
                learning_step = excluded.learning_step,
                is_learning_session_complete = excluded.is_learning_session_complete,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$user_id", profile.UserId);
        command.Parameters.AddWithValue("$profile_type", profile.ProfileType);
        command.Parameters.AddWithValue("$learning_goal", profile.LearningGoal);
        command.Parameters.AddWithValue("$course", profile.Course);
        command.Parameters.AddWithValue("$level", profile.Level);
        command.Parameters.AddWithValue("$subjects", profile.Subjects);
        command.Parameters.AddWithValue("$career", profile.Career);
        command.Parameters.AddWithValue("$university", profile.University);
        command.Parameters.AddWithValue("$semester", profile.Semester);
        command.Parameters.AddWithValue("$semester_content", profile.SemesterContent);
        command.Parameters.AddWithValue("$professional_area", profile.ProfessionalArea);
        command.Parameters.AddWithValue("$preferred_language", profile.PreferredLanguage);
        command.Parameters.AddWithValue("$session_minutes", profile.SessionMinutes);
        command.Parameters.AddWithValue("$is_completed", profile.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$is_learning_session_active", profile.IsLearningSessionActive ? 1 : 0);
        command.Parameters.AddWithValue("$learning_step", profile.LearningStep);
        command.Parameters.AddWithValue("$is_learning_session_complete", profile.IsLearningSessionComplete ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at_utc", profile.UpdatedAtUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    public LearningProfileRecord? LoadLearningProfile(string userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM learning_profiles WHERE user_id = $user_id;";
        command.Parameters.AddWithValue("$user_id", userId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new LearningProfileRecord(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetInt32(12),
            reader.GetInt32(13) != 0,
            reader.GetInt32(14) != 0,
            reader.GetInt32(15),
            reader.GetInt32(16) != 0,
            DateTime.Parse(reader.GetString(17)));
    }

    public void DeleteLearningProfile(string userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM learning_profiles WHERE user_id = $user_id;";
        command.Parameters.AddWithValue("$user_id", userId);
        command.ExecuteNonQuery();
    }

    private void Initialize()
    {
        lock (_lock)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    email TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    name TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    has_password INTEGER NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS sessions (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    user_id TEXT NOT NULL,
                    email TEXT NOT NULL,
                    session_id TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS learning_profiles (
                    user_id TEXT PRIMARY KEY,
                    profile_type INTEGER NOT NULL,
                    learning_goal TEXT NOT NULL,
                    course TEXT NOT NULL,
                    level TEXT NOT NULL,
                    subjects TEXT NOT NULL,
                    career TEXT NOT NULL,
                    university TEXT NOT NULL,
                    semester TEXT NOT NULL,
                    semester_content TEXT NOT NULL DEFAULT '',
                    professional_area TEXT NOT NULL,
                    preferred_language TEXT NOT NULL,
                    session_minutes INTEGER NOT NULL,
                    is_completed INTEGER NOT NULL,
                    is_learning_session_active INTEGER NOT NULL,
                    learning_step INTEGER NOT NULL,
                    is_learning_session_complete INTEGER NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS tutorial_state (
                    user_key TEXT PRIMARY KEY,
                    seen INTEGER NOT NULL
                );
                """;
            command.ExecuteNonQuery();
            MigrateLegacyUsers(connection);
            MigrateLegacySession(connection);
            MigrateLegacyProfiles();
            MigrateSemesterContentColumn(connection);
        }
    }

    private void MigrateSemesterContentColumn(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            ALTER TABLE learning_profiles ADD COLUMN semester_content TEXT NOT NULL DEFAULT '';
            """;
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
        {
            // La columna ya existe, ignorar.
        }
    }

    private void MigrateLegacyProfiles()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "learn-assist", "learning-profiles");
        if (!Directory.Exists(directory))
            return;

        foreach (var path in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<LearningProfile>(File.ReadAllText(path));
                if (profile is null || string.IsNullOrWhiteSpace(profile.UserId))
                    continue;

                SaveLearningProfile(new LearningProfileRecord(
                    profile.UserId,
                    (int)profile.ProfileType,
                    profile.LearningGoal,
                    profile.Course,
                    profile.Level,
                    JsonSerializer.Serialize(profile.Subjects),
                    profile.Career,
                    profile.University,
                    profile.Semester,
                    profile.SemesterContent,
                    profile.ProfessionalArea,
                    profile.PreferredLanguage,
                    profile.SessionMinutes,
                    profile.IsCompleted,
                    profile.IsLearningSessionActive,
                    profile.LearningStep,
                    profile.IsLearningSessionComplete,
                    profile.UpdatedAtUtc));
                File.Move(path, path + ".migrated", overwrite: true);
            }
            catch (JsonException)
            {
            }
        }
    }

    private void MigrateLegacyUsers(SqliteConnection connection)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "learn-assist", "users.json");
        if (!File.Exists(path))
            return;

        try
        {
            var users = JsonSerializer.Deserialize<List<UserRecord>>(File.ReadAllText(path)) ?? [];
            foreach (var user in users)
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT OR IGNORE INTO users
                    (id, email, name, password_hash, has_password, created_at)
                    VALUES ($id, $email, $name, $password_hash, $has_password, $created_at);
                    """;
                AddUserParameters(command, user);
                command.ExecuteNonQuery();
            }
            File.Move(path, path + ".migrated", overwrite: true);
        }
        catch (JsonException)
        {
        }
    }

    private void MigrateLegacySession(SqliteConnection connection)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "learn-assist", "session.json");
        if (!File.Exists(path))
            return;

        try
        {
            var session = JsonSerializer.Deserialize<RememberedSession>(File.ReadAllText(path));
            if (session is not null)
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT OR REPLACE INTO sessions (id, user_id, email, session_id)
                    VALUES (1, $user_id, $email, $session_id);
                    """;
                command.Parameters.AddWithValue("$user_id", session.UserId);
                command.Parameters.AddWithValue("$email", session.Email);
                command.Parameters.AddWithValue("$session_id", session.SessionId);
                command.ExecuteNonQuery();
            }
            File.Move(path, path + ".migrated", overwrite: true);
        }
        catch (JsonException)
        {
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private static UserRecord ReadUser(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4) != 0,
            DateTime.Parse(reader.GetString(5)));

    private static void AddUserParameters(SqliteCommand command, UserRecord user)
    {
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$name", user.Name);
        command.Parameters.AddWithValue("$password_hash", user.PasswordHash);
        command.Parameters.AddWithValue("$has_password", user.HasPassword ? 1 : 0);
        command.Parameters.AddWithValue("$created_at", user.CreatedAt.ToString("O"));
    }

    public sealed record UserRecord(
        string Id,
        string Email,
        string Name,
        string PasswordHash,
        bool HasPassword,
        DateTime CreatedAt);

    public sealed record RememberedSession(string UserId, string Email, string SessionId);

    public sealed record LearningProfileRecord(
        string UserId,
        int ProfileType,
        string LearningGoal,
        string Course,
        string Level,
        string Subjects,
        string Career,
        string University,
        string Semester,
        string SemesterContent,
        string ProfessionalArea,
        string PreferredLanguage,
        int SessionMinutes,
        bool IsCompleted,
        bool IsLearningSessionActive,
        int LearningStep,
        bool IsLearningSessionComplete,
        DateTime UpdatedAtUtc);
}
