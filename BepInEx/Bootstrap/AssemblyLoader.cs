using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
    internal static class AssemblyLoader
    {
        /// <summary>
        ///     Configuration value of whether assembly dumping is enabled or not.
        /// </summary>
        private static bool DumpingEnabled => Utility.SafeParseBool(Config.GetEntry("dump-assemblies", "false", "Preloader"));

        public static Dictionary<string, AssemblyDefinition> LoadIntoCecil(string directory)
        {
            var assemblies = new Dictionary<string, AssemblyDefinition>();

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
            {
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

                //NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
                //System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
                //It's also generally dangerous to change system.dll since so many things rely on it, 
                // and it's already loaded into the appdomain since this loader references it, so we might as well skip it
                if (assembly.Name.Name == "System" || assembly.Name.Name == "mscorlib"
                ) //mscorlib is already loaded into the appdomain so it can't be patched
                {
                    assembly.Dispose();
                    continue;
                }

                assemblies.Add(Path.GetFileName(assemblyPath), assembly);
            }

            return assemblies;
        }

        public static void LoadIntoCLR(IDictionary<string, AssemblyDefinition> assemblies, HashSet<string> patchedAssemblies)
        {
            foreach (var kv in assemblies)
            {
                string filename = kv.Key;
                var assembly = kv.Value;

                if (DumpingEnabled && patchedAssemblies.Contains(filename))
                    using (var mem = new MemoryStream())
                    {
                        string dirPath = Path.Combine(Paths.PluginPath, "DumpedAssemblies");

                        if (!Directory.Exists(dirPath))
                            Directory.CreateDirectory(dirPath);

                        assembly.Write(mem);
                        File.WriteAllBytes(Path.Combine(dirPath, filename), mem.ToArray());
                    }

                Load(assembly);
                assembly.Dispose();
            }
        }

        /// <summary>
        ///     Loads an individual assembly defintion into the CLR.
        /// </summary>
        /// <param name="assembly">The assembly to load.</param>
        private static void Load(AssemblyDefinition assembly)
        {
            using (var assemblyStream = new MemoryStream())
            {
                assembly.Write(assemblyStream);
                Assembly.Load(assemblyStream.ToArray());
            }
        }
    }
}