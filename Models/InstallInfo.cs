using System;

namespace learn_Assist.Models;

public enum InstallScope
{
    User,
    System,
}

public class InstallInfo
{
    public InstallScope Scope { get; set; }
    public string BinaryPath { get; set; } = string.Empty;
    public DateTime InstallDate { get; set; }
}