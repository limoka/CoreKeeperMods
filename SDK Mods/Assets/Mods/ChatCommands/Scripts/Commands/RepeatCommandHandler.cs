using System;
using System.Linq;
using System.Text.RegularExpressions;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using Unity.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChatCommands.Chat.Commands
{
    public class RepeatCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            if (parameters.Length < 2)
                return new CommandOutput("Not enough arguments, check usage: /help repeat", CommandStatus.Error);

            if (!int.TryParse(parameters[0], out int times))
                return new CommandOutput($"'{parameters[0]}' is not a valid number!", CommandStatus.Error);

            if (!CommandsModule.GetCommandHandler(parameters[1], out CommandPair command))
                return new CommandOutput($"Command named '{parameters[1]}' does not exist!", CommandStatus.Error);

            if (!command.isServer)
                return new CommandOutput("Non Server sided commands are not supported by repeat!", CommandStatus.Error);
            
            int count = 0;

            try
            {
                var newParameters = parameters.Skip(2).Select(ParseParameters).ToArray();

                for (int i = 0; i < times; i++)
                {
                    int currentIndex = i;
                    string[] outParams = newParameters.Select(data => data.prefix + data.Execute(currentIndex, times) + data.postfix).ToArray();
                    CommandOutput output = command.serverHandler.Execute(outParams, sender);
                    if (output.status is CommandStatus.Error or CommandStatus.Warning) return output.AppendAtStart("Error while repeating: ");

                    count++;
                }
            }
            catch (Exception e)
            {
                return new CommandOutput(e.Message, CommandStatus.Error);
            }

            return $"Executed {parameters[1]} {count} times successfully!";
        }

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
            return Random.Range(min, max).ToString();
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
    }
}