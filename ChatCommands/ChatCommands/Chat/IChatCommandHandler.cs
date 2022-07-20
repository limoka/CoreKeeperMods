
namespace ChatCommands.Chat.Commands;

public interface IChatCommandHandler
{
    /// <summary>
    /// Execute command & return feedback
    /// </summary>
    CommandOutput Execute(string[] parameters);
    string GetDescription();
    string[] GetTriggerNames();
}