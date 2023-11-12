using System;

namespace ModReporter.Scripts
{
    [Flags]
    public enum InstallationKind
    {
        NONE = 0,
        MANUAL = 1,
        MOD_IO = 2,
        BOTH = 3
    }
}