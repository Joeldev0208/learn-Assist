using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services;

public sealed class LearningProfileService
{
    private readonly UserDatabase _database;

    public LearningProfileService(UserDatabase? database = null)
    {
        _database = database ?? UserDatabase.Current;
    }

    public Task<LearningProfile?> LoadAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var stored = _database.LoadLearningProfile(userId);
        if (stored is null)
            return Task.FromResult<LearningProfile?>(null);

        return Task.FromResult<LearningProfile?>(new LearningProfile
        {
            UserId = stored.UserId,
            ProfileType = (LearningProfileType)stored.ProfileType,
            LearningGoal = stored.LearningGoal,
            Course = stored.Course,
            Level = stored.Level,
            Subjects = JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(stored.Subjects) ?? [],
            Career = stored.Career,
            University = stored.University,
            Semester = stored.Semester,
            SemesterContent = stored.SemesterContent,
            ProfessionalArea = stored.ProfessionalArea,
            PreferredLanguage = stored.PreferredLanguage,
            SessionMinutes = stored.SessionMinutes,
            IsCompleted = stored.IsCompleted,
            IsLearningSessionActive = stored.IsLearningSessionActive,
            LearningStep = stored.LearningStep,
            IsLearningSessionComplete = stored.IsLearningSessionComplete,
            UpdatedAtUtc = stored.UpdatedAtUtc,
        });
    }

    public Task SaveAsync(LearningProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.UserId);

        profile.UpdatedAtUtc = DateTime.UtcNow;
        _database.SaveLearningProfile(new UserDatabase.LearningProfileRecord(
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

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        _database.DeleteLearningProfile(userId);
        return Task.CompletedTask;
    }
}
