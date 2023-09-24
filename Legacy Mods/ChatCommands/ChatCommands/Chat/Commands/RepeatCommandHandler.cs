using System;
using System.Linq;
using System.Text.RegularExpressions;
using ChatCommands.Util;
using CoreLib.Submodules.ChatCommands;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChatCommands.Chat.Commands
{
    public class RepeatCommandHandler : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            if (parameters.Length < 2)
                return new CommandOutput("Not enough arguments, check usage: /help repeat", Color.red);

            if (!int.TryParse(parameters[0], out int times))
                return new CommandOutput($"'{parameters[0]}' is not a valid number!", Color.red);

            if (!CommandsModule.GetCommandHandler(parameters[1], out IChatCommandHandler commandHandler))
                return new CommandOutput($"Command named '{parameters[1]}' does not exist!", Color.red);

            int count = 0;
            
            try
            {
                ParameterData[] newParameters = parameters.Skip(2).Select(ParseParameters).ToArray();

                for (int i = 0; i < times; i++)
                {
                    int currentIndex = i;
                    string[] outParams = newParameters.Select(data => data.prefix + data.Execute(currentIndex, times) + data.postfix).ToArray();
                    CommandOutput output = commandHandler.Execute(outParams);
                    if (output.color == Color.green) count++;
                    else if (output.color == Color.red)
                    {
                        return output.AppendAtStart("Error while repeating: ");
                    }
                }
            }
            catch (Exception e)
            {
                return new CommandOutput(e.Message, Color.red);
            }

            return $"Executed {parameters[1]} {count} times successfully!";
        }

        private ParameterData ParseParameters(string parameter)
        {
            Match match = Regex.Match(parameter, @"([a-z]{3})\((.*)\)");
            if (!match.Success) return new ParameterData(parameter);


            string functionName = match.Groups[1].Value;
            string argString = match.Groups[2].Value;
            string[] args = argString.Split(',');
            if (args.Length != 2) throw new ArgumentException("Generator function must always have 2 parameters!");
            int[] ints = args.Select(int.Parse).ToArray();


            string prefix = parameter.Substring(0, match.Index);
            string postfix = parameter.Substring(match.Index + match.Length, parameter.Length - prefix.Length - match.Length);

            Function function = null;
            switch (functionName)
            {
                case "rng":
                    function = rng;
                    break;
                case "lin":
                    function = lin;
                    break;
            }

            return new ParameterData(prefix, postfix, ints, function);
        }

        public static string rng(int min, int max, int i, int count)
        {
            return Random.RandomRangeInt(min, max).ToString();
        }

        public static string lin(int min, int max, int i, int count)
        {
            return Mathf.RoundToInt(Mathf.Lerp(min, max, i / (float)count)).ToString();
        }

        internal struct ParameterData
        {
            public string prefix;
            public string postfix;

            public int[] args;
            public Function function;

            public ParameterData(string prefix) : this()
            {
                this.prefix = prefix;
                postfix = "";
            }

            public ParameterData(string prefix, string postfix, int[] args, Function function)
            {
                this.prefix = prefix;
                this.postfix = postfix;
                this.args = args;
                this.function = function;
            }

            public string Execute(int i, int count)
            {
                if (function == null) return "";
                return function(args[0], args[1], i, count);
            }
        }

        internal delegate string Function(int a, int b, int i, int count);

        public string GetDescription()
        {
            return "Use /repeat to repeat any valid command\n" +
                   "Syntax: /repeat {times} {command name} [parameters to the command]\n" +
                   "Example /repeat 10 spawn SlimeBlob ~rng(-10,10) ~rng(-10,10)\n" +
                   "\n" +
                   "Use value generators to procedurally enter values:\n" +
                   "Generator syntax is: name(a,b) WITHOUT any spaces\n" +
                   "Generator syntax will be replaced with it's output on each execution\n" +
                   "\n" +
                   "There are two generators:\n" +
                   "* lin: lerp output from a to b according to current execution index\n" +
                   "* rng: output random values from a to b on each execution";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "repeat" };
        }
    }
}