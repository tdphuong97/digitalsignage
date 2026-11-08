using System.Runtime.InteropServices;

namespace SignageVienBa.App;

/// <summary>
/// Finds the old PowerShell sync (sync-to-cloudflare*.ps1) in this PC's Task Scheduler, through the Task Scheduler
/// COM API: no admin rights needed, and it reads the action itself, whatever the Windows display language.
/// Tasks other Windows users created without sharing are not visible from here.
/// </summary>
internal static class OldSyncTasks
{
    private const string Marker = "sync-to-cloudflare";
    private const int IncludeHidden = 1;   // TASK_ENUM_HIDDEN
    private const int ExecAction = 0;      // TASK_ACTION_EXEC

    /// <summary>Task path and whether it is enabled, for every task whose program or arguments mention the old script.</summary>
    public static IReadOnlyList<(string Task, bool Enabled)> Find()
    {
        var found = new List<(string, bool)>();
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new COMException("Task Scheduler is not available");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        Walk(service.GetFolder("\\"), found);
        return found;
    }

    private static void Walk(dynamic folder, List<(string, bool)> found)
    {
        try
        {
            foreach (dynamic task in folder.GetTasks(IncludeHidden))
            {
                try
                {
                    foreach (dynamic action in task.Definition.Actions)
                    {
                        if ((int)action.Type != ExecAction) continue;
                        string command = $"{action.Path} {action.Arguments}";
                        if (command.Contains(Marker, StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(((string)task.Path, (bool)task.Enabled));
                            break;
                        }
                    }
                }
                catch (COMException) { }              // a task we may not read
                catch (UnauthorizedAccessException) { }
            }
            foreach (dynamic sub in folder.GetFolders(0)) Walk(sub, found);
        }
        catch (COMException) { }                      // a folder we may not open (e.g. \Microsoft\Windows\…)
        catch (UnauthorizedAccessException) { }
    }
}
