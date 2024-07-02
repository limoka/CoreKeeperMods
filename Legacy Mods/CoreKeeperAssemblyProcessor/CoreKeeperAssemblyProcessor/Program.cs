using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Options;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace AssemblyProcessor // Note: actual namespace depends on the project name.
{
    internal static class Program
    {
        private static bool help;

        private static string[] authorMethods =
        {
            "EnsureHasComponent",
            "EnsureHasComponent",
            "AddComponentData",
            "AddComponentData",
            "AddSharedComponentData",
            "AddSharedComponentData",
            "EnsureHasBuffer",
            "EnsureHasBuffer",
            "AddToBuffer",
            "AddToBuffer",
        };

        private static void Main(string[] args)
        {
            ParseArgs(args, out string input, out string output, out string managed);
            //GetDirectoryInfo(output, out string directoryName, out string fileName);

            var files = Directory.EnumerateFiles(input);
            foreach (string file in files)
            {
                if (!file.EndsWith(".dll")) continue;
                var fileName = Path.GetFileName(file);
                fileName = Path.ChangeExtension(fileName, null);

                Console.WriteLine($"## {fileName}\n");

                AssemblyDefinition assemblyDefinition = LoadAssembly(file, managed);

                var allTypes = GetAllTypes(assemblyDefinition.MainModule)
                    .Where(type => type != null &&
                                   type.Name.Contains("Converter") &&
                                   !type.IsValueType &&
                                   type.BaseType != null &&
                                   type.BaseType.Name.Contains("Converter"))
                    .ToList();

                allTypes.Sort((type1, type2) => string.Compare(type1.Name, type2.Name, StringComparison.Ordinal));

                foreach (var type in allTypes)
                {
                    try
                    {
                        PrintConverterInfo(type);
                    }
                    catch (Exception e)
                    {
                        // ignored
                    }
                }
            }
            /*int count = 0;
            foreach (TypeDefinition type in allTypes)
            {
                if (type == null) return;
                
                RemoveAtrribute(type);
                PublicizeType(type);
                ProcessColorReplacer(type);
                count++;
            }
            
            Console.WriteLine($"Publicized {count} types!");
            Console.WriteLine("Saving the new assembly ...");*/

            //WriteAssembly(fileName, input, directoryName, assemblyDefinition);
        }

        private static void PrintConverterInfo(TypeDefinition type)
        {
            if (type.Name.Contains("Converter") &&
                !type.IsValueType &&
                type.BaseType.Name.Contains("Converter"))
            {
                var convertMethod = type.Methods.FirstOrDefault(definition => definition.Name.Equals("Convert"));
                if (convertMethod == null) return;

                var authoringComponent = convertMethod.Parameters.FirstOrDefault();
                if (authoringComponent == null ||
                    !authoringComponent.ParameterType.Name.Contains("Authoring")) return;

                Console.WriteLine($"- `{authoringComponent.ParameterType.FullName}`");

                var components = new List<TypeReference>();

                foreach (Instruction instruction in convertMethod.Body.Instructions)
                {
                    if (instruction.OpCode != OpCodes.Call &&
                        instruction.OpCode != OpCodes.Calli &&
                        instruction.OpCode != OpCodes.Callvirt) continue;

                    var method = (MethodReference)instruction.Operand;
                    if (!authorMethods.Contains(method.Name)) continue;

                    var genMethod = method as GenericInstanceMethod;
                    foreach (TypeReference argument in genMethod.GenericArguments)
                    {
                        if (components.All(component => !component.FullName.Equals(argument.FullName)))
                        {
                            components.Add(argument);
                        }
                    }
                }

                foreach (TypeReference component in components)
                {
                    var isAux = IsAux(component);

                    Console.WriteLine($"    - `{component.FullName}{(isAux ? " (AUX)" : "")}`");
                }
            }
        }

        private static bool IsAux(TypeReference component)
        {
            try
            {
                var typeDef = component.Resolve();
                return typeDef.CustomAttributes.Any(attribute => attribute.AttributeType.FullName.Contains("InventoryAuxDataComponent"));
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private static void WriteAssembly(string fileName, string input, string directoryName, AssemblyDefinition assemblyDefinition)
        {
            if (fileName == "")
            {
                fileName = $"{Path.GetFileNameWithoutExtension(input)}{Path.GetExtension(input)}";
                Console.WriteLine("Info: Use default output name: \"{0}\"", fileName);
            }

            if (directoryName == "")
            {
                directoryName = "output";
                Console.WriteLine("Info: Use default output dir: \"{0}\"", directoryName);
            }

            string finalPath = Path.Combine(directoryName, fileName);
            try
            {
                if (directoryName != "" && !Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                assemblyDefinition.Write(finalPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("ERROR! Cannot create/overwrite the new assembly. ");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Please check the path and its permissions and in case of overwriting an existing file ensure that it isn't currently used.");
                Exit(50);
            }

            Console.WriteLine("Completed.");
            Exit(0);
        }

        private static void ProcessColorReplacer(TypeDefinition type)
        {
            if (!type.FullName.Equals("ColorReplacer")) return;

            MethodDefinition validateMethod = type.Methods.FirstOrDefault(method => method.Name.Equals("OnValidate"));
            if (!validateMethod.HasBody) return;

            var voidTypeReference = type.Module.TypeSystem.Void;
            MethodBody body = new MethodBody(validateMethod);
            ILProcessor il = body.GetILProcessor();

            il.Clear();
            if (validateMethod.ReturnType.IsPrimitive)
            {
                il.Emit(OpCodes.Ldc_I4_0);
            }
            else if (validateMethod.ReturnType != voidTypeReference)
            {
                il.Emit(OpCodes.Ldnull);
            }

            il.Emit(OpCodes.Ret);

            validateMethod.Body = body;

            // Probably not necessary but just in case
            validateMethod.AggressiveInlining = false;
            validateMethod.NoInlining = true;
            Console.WriteLine("Stripped OnValidate() code in ColorReplacer");
        }

        private static void PublicizeType(TypeDefinition type)
        {
            IEnumerable<MethodDefinition> getters = type.Properties.Select(p => p.GetMethod);
            IEnumerable<MethodDefinition> setters = type.Properties.Select(p => p.SetMethod);

            if (!type.IsPublic)
            {
                if (type.IsNested)
                {
                    type.IsNestedPublic = true;
                }
                else
                {
                    type.IsPublic = true;
                }
            }

            foreach (MethodDefinition methods in type.Methods)
            {
                if (methods != null && !methods.IsPublic)
                {
                    methods.IsPublic = true;
                }
            }

            foreach (FieldDefinition field in type.Fields)
            {
                if (field != null && field.DeclaringType.Events.All(ev => ev.Name != field.Name) && !field.IsPublic)
                {
                    field.IsPublic = true;
                }
            }

            foreach (MethodDefinition getter in getters)
            {
                if (getter != null && !getter.IsPublic)
                {
                    getter.IsPublic = true;
                }
            }

            foreach (MethodDefinition setter in setters)
            {
                if (setter != null && !setter.IsPublic)
                {
                    setter.IsPublic = true;
                }
            }
        }

        private static void RemoveAtrribute(TypeDefinition type)
        {
            for (int i = 0; i < type.CustomAttributes.Count; i++)
            {
                var attribute = type.CustomAttributes[i];
                if (attribute.AttributeType.FullName.Equals("UnityEngine.ExecuteInEditMode"))
                {
                    Console.WriteLine($"Removing ExecuteInEditMode from type {type.FullName}");
                    type.CustomAttributes.RemoveAt(i);
                }
                else if (attribute.AttributeType.FullName.Contains("BurstCompile"))
                {
                    Console.WriteLine($"Removing BurstCompile from type {type.FullName}");
                    type.CustomAttributes.RemoveAt(i);
                }
            }
        }

        private static AssemblyDefinition LoadAssembly(string input, string managedPath)
        {
            if (!File.Exists(input))
            {
                Console.WriteLine();
                Console.WriteLine("ERROR! File doesn't exist or you don't have sufficient permissions.");
                Exit(30);
            }

            try
            {
                DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
                string directoryName = Path.GetDirectoryName(input);
                resolver.AddSearchDirectory(directoryName);
                resolver.AddSearchDirectory(managedPath);

                return AssemblyDefinition.ReadAssembly(input, new ReaderParameters()
                {
                    AssemblyResolver = resolver
                });
            }
            catch (Exception)
            {
                Console.WriteLine();
                Console.WriteLine("ERROR! Cannot read the assembly. Please check your permissions.");
                Exit(40);
                throw new Exception();
            }
        }

        private static void GetDirectoryInfo(string output, out string directoryName, out string fileName)
        {
            directoryName = "";
            fileName = "";

            if (output != "")
            {
                try
                {
                    directoryName = Path.GetDirectoryName(output);
                    fileName = Path.GetFileName(output);
                }
                catch (Exception)
                {
                    Console.WriteLine("ERROR! Invalid output argument.");
                    Exit(20);
                }
            }
        }

        private static void ParseArgs(string[] args, out string inputOut, out string outputOut, out string managedOut)
        {
            string input = "";
            string output = "";
            string managed = "";

            OptionSet optionSet = new OptionSet();
            optionSet.Add<string>("i|input=", "path (relative or absolute) to the input assembly", delegate(string i) { input = i; });
            optionSet.Add<string>("o|output=", "path/dir/filename for the output assembly", delegate(string o) { output = o; });
            optionSet.Add<string>("h|help", "show this message and exit", delegate(string h) { help = h != null; });
            optionSet.Add<string>("p|path=", "Path to managed folder", delegate(string p) { managed = p; });
            Console.WriteLine();
            try
            {
                List<string> list = optionSet.Parse(args);
                if (help)
                {
                    ShowHelp(optionSet);
                }

                if (input == "" && list.Count >= 1)
                {
                    input = list[0];
                }

                if (input == "")
                {
                    throw new OptionException();
                }

                if (output == "" && list.Count >= 2)
                {
                    output = list[1];
                }
            }
            catch (OptionException)
            {
                Console.WriteLine("ERROR! Incorrect arguments. You need to provide the path to the assembly to process.");
                Console.WriteLine("On Windows you can even drag and drop the assembly on the .exe.");
                Console.WriteLine("Try `--help' for more information.");
                Exit(10);
            }

            inputOut = input;
            outputOut = output;
            managedOut = managed;
        }

        public static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition moduleDefinition)
        {
            return GetAllNestedTypes(moduleDefinition.Types);
        }

        private static IEnumerable<TypeDefinition> GetAllNestedTypes(IEnumerable<TypeDefinition> typeDefinitions)
        {
            if (typeDefinitions != null && !typeDefinitions.Any())
            {
                return new List<TypeDefinition>();
            }

            return typeDefinitions.Concat(GetAllNestedTypes(typeDefinitions.SelectMany(t => t.NestedTypes)));
        }

        public static void Exit(int exitCode = 0)
        {
            Environment.Exit(exitCode);
        }

        private static void ShowHelp(OptionSet optionSet)
        {
            Console.WriteLine();
            Console.WriteLine("Usage: CoreKeeperAssemblyProcessor.exe [Options]+");
            Console.WriteLine("Processes the assembly for it to be Unity project friendly");
            Console.WriteLine("An input path must be provided, the other options are optional.");
            Console.WriteLine("You can use it without the option identifiers;");
            Console.WriteLine("If so, the first argument is for input and the optional second one for output.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(Console.Out);
            Exit(0);
        }
    }
}