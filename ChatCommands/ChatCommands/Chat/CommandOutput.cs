using UnityEngine;

namespace ChatCommands.Chat;

public struct CommandOutput
{
    public string feedback;
    public Color color;

    public CommandOutput(string feedback)
    {
        this.feedback = feedback;
        color = Color.green;
    }

    public CommandOutput(string feedback, Color color)
    {
        this.feedback = feedback;
        this.color = color;
    }
        
    public static implicit operator CommandOutput(string d) => new CommandOutput(d);
}