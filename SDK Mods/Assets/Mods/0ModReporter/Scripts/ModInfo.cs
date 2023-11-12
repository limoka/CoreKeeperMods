using System.Collections.Generic;
using PugMod;
using UnityEngine;

namespace ModReporter.Scripts
{
    public class LogEntry
    {
        public LogType logType;
        public string message;
        public string stacktrace;
    }
    
    public class ModInfo
    {
        public string modName;
        public long modId;

        public ModStatus status = ModStatus.OK;
        public InstallationKind installation = InstallationKind.NONE;
        
        public List<ModMetadata.Dependency> dependencies = new List<ModMetadata.Dependency>();
        public bool dependenciesUnknown;
        public List<LogEntry> logEntries = new List<LogEntry>();
    }
}