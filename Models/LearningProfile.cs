using System;
using System.Collections.Generic;

namespace learn_Assist.Models;

public enum LearningProfileType
{
    Student,
    University,
    Professional,
    Personal,
}

public sealed class LearningProfile
{
    public string UserId { get; set; } = string.Empty;
    public LearningProfileType ProfileType { get; set; } = LearningProfileType.Personal;
    public string LearningGoal { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public List<string> Subjects { get; set; } = [];
    public string Career { get; set; } = string.Empty;
    public string University { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string SemesterContent { get; set; } = string.Empty;
    public string ProfessionalArea { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "es";
    public int SessionMinutes { get; set; } = 15;
    public bool IsCompleted { get; set; }

    public bool IsLearningSessionActive { get; set; }
    public int LearningStep { get; set; } = 1;
    public bool IsLearningSessionComplete { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
