using System.Collections.Generic;
using System.Linq;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using UnityEngine;
using Object = Il2CppSystem.Object;


namespace ChatCommands.Chat.Commands;

public class HideUICommand : IChatCommandHandler
{
    public static Dictionary<string, bool> states = new Dictionary<string, bool>();

    public static string[] validNames = { "player", "ui", "inventory" };

    public CommandOutput Execute(string[] parameters)
    {
        if (parameters.Length < 1)
        {
            return new CommandOutput("Not enough arguments. Usage:\nhide <target> [state]", Color.red);
        }

        switch (parameters.Length)
        {
            case 2:
            case 1:
                string target = parameters[0].ToLowerInvariant();
                if (ValidateTarget(target))
                {
                    if (parameters.Length == 3 && bool.TryParse(parameters[1], out bool value))
                    {
                        SetState(target, value);
                    }
                    else
                    {
                        ToggleState(target);
                    }

                    UpdateUIStatus();
                    return "Success";
                }
                else
                {
                    return new CommandOutput($"Target {target} is not valid! Valid targets:\nplayer, ui, inventory", Color.red);
                }

            default:
                return new CommandOutput("Incorrect argument count. Usage:\nhide <target> [state]", Color.red);
        }
    }

    public void UpdateUIStatus()
    {
        foreach (PlayerController player in GameManagers.GetMainManager().allPlayers)
        {
            player.XScaler.gameObject.SetActive(states["player"]);
            player.conditionEffectsHandler.gameObject.SetActive(states["player"]);
        }

        GameObject uiCamera = GameManagers.GetMainManager()._uiManager.UICamera;
        foreach (Object o in uiCamera.transform)
        {
            Transform transform = o.TryCast<Transform>();
            string goName = transform.name.ToLowerInvariant();
            if (goName.Contains("chat") || 
                goName.Contains("menu") || 
                goName.Contains("options") || 
                goName.Contains("customization") || 
                goName.Contains("debug") || 
                goName.Contains("switch") || 
                goName.Contains("hearts") || 
                goName.Contains("console") ||
                goName.Contains("cinematic") || 
                goName.Contains("pop up")) continue;

            if ((goName.Contains("ui") || goName.Contains("window")) && !goName.Contains("conditions"))
            {
                transform.gameObject.SetActive(states["inventory"]);
            }
            else
            {
                transform.gameObject.SetActive(states["ui"]);
            }
        }
    }

    public static void SetState(string name, bool value)
    {
        if (name.Equals("all"))
        {
            foreach (string validName in validNames)
            {
                SetStateDirect(validName, value);
            }
            return;
        }
        SetStateDirect(name, value);
    }
    
    public static bool GetState(string name)
    {
        if (name.Equals("all"))
        {
            if (!states.ContainsKey("player"))
            {
                return true;
            }

            return states["player"];
        }
        return !states.ContainsKey(name) || states[name];
    }

    private static void SetStateDirect(string name, bool value)
    {
        if (!states.ContainsKey(name))
        {
            states.Add(name, value);
        }
        else
        {
            states[name] = value;
        }
    }

    private static void ToggleState(string name)
    {
        SetState(name, !GetState(name));
    }

    private static bool ValidateTarget(string target)
    {
        if (validNames.Contains(target))
        {
            return true;
        }

        if (target.Equals("all"))
        {
            return true;
        }

        return false;
    }

    public string GetDescription()
    {
        return "Use /hide to hide elements of user interface. Usage:\n/hide {target} [state]\nPossible targets: player, ui, inventory";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "hide" };
    }
    
}