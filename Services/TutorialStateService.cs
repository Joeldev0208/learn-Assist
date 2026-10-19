using System;
namespace learn_Assist.Services;

public static class TutorialStateService
{
    public static bool HasBeenSeen(string userKey)
    {
        try
        {
            return UserDatabase.Current.LoadTutorialState(userKey) == "1";
        }
        catch
        {
            return false;
        }
    }

    public static void MarkSeen(string userKey)
    {
        try
        {
            UserDatabase.Current.MarkTutorialSeen(userKey);
        }
        catch
        {
            // The tutorial remains usable for the current session.
        }
    }
}
