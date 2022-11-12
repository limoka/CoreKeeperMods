using System;
using System.Linq;
using ChatCommands.Chat.Commands;
using HarmonyLib;
using I2.Loc;

namespace ChatCommands.Chat
{
    [HarmonyPatch]
    public class TitleScreenAnimator_Patch
    {
        private static bool initialized;

        [HarmonyPatch(typeof(TitleScreenAnimator), nameof(TitleScreenAnimator.Start))]
        [HarmonyPostfix]
        public static void OnTitleStart()
        {
            if (!initialized)
            {
                if (LocalizationManager.Sources.Count > 0)
                {
                    foreach (LanguageSourceData source in LocalizationManager.Sources)
                    {
                        TermData[] filteredTerms = source.mTerms._items.Where(data => data != null && data.Term != null && data.Term.StartsWith("Items/")).ToArray();
                        foreach (TermData term in filteredTerms)
                        {
                            try
                            {
                                if (term.Term.Contains("Desc")) continue;
                            
                                string objIdName = term.Term[6..];
                                ObjectID objectID = Enum.Parse<ObjectID>(objIdName);
                                GiveCommandHandler.friendlyNameDict.Add(term.Languages[0].ToLower(), objectID);
                            }
                            catch (Exception)
                            {
                                ChatCommandsPlugin.logger.LogWarning($"Failed to add item freindly name for term {term.Term}");
                            }
                        }
                        ChatCommandsPlugin.logger.LogInfo($"Got {GiveCommandHandler.friendlyNameDict.Keys.Count} friendly name entries  !");
                    }
                }

                initialized = true;
            }
        }
    }
}