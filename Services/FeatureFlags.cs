using learn_Assist.Models;

namespace learn_Assist.Services;

/// <summary>
/// Developer-facing feature switches, bound from <see cref="AppSettings"/>.
/// Kept separate from the user-facing AI configuration.
/// </summary>
internal static class FeatureFlags
{
    /// <summary>
    /// When enabled (default), the learner's <see cref="LearningProfile"/> is
    /// summarized into a bounded system message and injected into the AI prompt.
    /// Disable by setting <c>LEARN_ASSIST_PROFILE_CONTEXT=0</c>.
    /// </summary>
    public static bool ProfileContextEnabled => !IsOff(AppSettings.Current.ProfileContext);

    private static bool IsOff(string? value)
    {
        var v = value?.Trim();
        return v is "0" or "false" or "no" or "off";
    }
}
