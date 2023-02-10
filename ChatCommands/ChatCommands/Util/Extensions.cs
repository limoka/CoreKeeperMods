using CoreLib.Submodules.ChatCommands;

namespace ChatCommands.Util
{
    public static class Extensions
    {
        public static CommandOutput AppendAtStart(this CommandOutput commandOutput, string prefix)
        {
            commandOutput.feedback = $"{prefix}: {commandOutput.feedback}";
            return commandOutput;
        }
        
    }
}