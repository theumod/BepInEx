using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// Delegate used in patching assemblies.
	/// </summary>
	/// <param name="assembly">The assembly that is being patched.</param>
    public delegate void AssemblyPatcherDelegate(ref AssemblyDefinition assembly);

    internal class CecilPatcher
    {
        public IEnumerable<string> TargetDLLs { get; set; } = null;
        public Action Initializer { get; set; } = null;
        public Action Finalizer { get; set; } = null;
        public AssemblyPatcherDelegate Patcher { get; set; } = null;
        public string Name { get; set; } = string.Empty;
    }

	/// <summary>
	/// Worker class which is used for loading and patching entire folders of assemblies, or alternatively patching and loading assemblies one at a time.
	/// </summary>
    internal class AssemblyPatcher
    {
		/// <summary>
		/// Configuration value of whether assembly dumping is enabled or not.
		/// </summary>
        private static bool DumpingEnabled => Utility.SafeParseBool(Config.GetEntry("dump-assemblies", "false", "Preloader"));

        /// <summary>
        ///     The dictionary of currently loaded patchers. The key is the patcher delegate that will be used to patch, and the
        ///     value is a list of filenames of assemblies that the patcher is targeting.
        /// </summary>
        private Dictionary<AssemblyPatcherDelegate, IEnumerable<string>> Patchers { get; } =
            new Dictionary<AssemblyPatcherDelegate, IEnumerable<string>>();

        private List<Action> Initializers { get; } = new List<Action>();

        private List<Action> Finalizers { get; } = new List<Action>();

        public AssemblyPatcher()
        {

        }

        public void AddPatcher(CecilPatcher patcher)
        {
            if (patcher.TargetDLLs != null && patcher.Patcher != null)
                Patchers[patcher.Patcher] = patcher.TargetDLLs;
            if(patcher.Initializer != null)
                Initializers.Add(patcher.Initializer);
            if(patcher.Finalizer != null)
                Finalizers.Add(patcher.Finalizer);
        }

        public static Dictionary<string, AssemblyDefinition> LoadAllAssemblies(string directory)
        {
            Dictionary<string, AssemblyDefinition> assemblies = new Dictionary<string, AssemblyDefinition>();

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
            {
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

                //NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
                //System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
                //It's also generally dangerous to change system.dll since so many things rely on it, 
                // and it's already loaded into the appdomain since this loader references it, so we might as well skip it
                if (assembly.Name.Name == "System"
                    || assembly.Name.Name == "mscorlib") //mscorlib is already loaded into the appdomain so it can't be patched
                {
                    assembly.Dispose();
                    continue;
                }

                assemblies.Add(Path.GetFileName(assemblyPath), assembly);
            }

            return assemblies;
        }

        public static void LoadAssembliesIntoMemory(IDictionary<string, AssemblyDefinition> assemblies, HashSet<string> patchedAssemblies)
        {
            foreach (var kv in assemblies)
            {
                string filename = kv.Key;
                var assembly = kv.Value;

                if (DumpingEnabled && patchedAssemblies.Contains(filename))
                {
                    using (MemoryStream mem = new MemoryStream())
                    {
                        string dirPath = Path.Combine(Paths.PluginPath, "DumpedAssemblies");

                        if (!Directory.Exists(dirPath))
                            Directory.CreateDirectory(dirPath);

                        assembly.Write(mem);
                        File.WriteAllBytes(Path.Combine(dirPath, filename), mem.ToArray());
                    }
                }

                Load(assembly);
                assembly.Dispose();
            }
        }

        public void InitializePatching()
        {
            //run all initializers
            foreach (var init in Initializers)
                init.Invoke();
        }

        /// <summary>
        /// Patches and loads an entire directory of assemblies.
        /// </summary>
        /// <param name="directory">The directory to load assemblies from.</param>
        /// <param name="patcherMethodDictionary">The dictionary of patchers and their targeted assembly filenames which they are patching.</param>
        /// <param name="initializers">List of initializers to run before any patching starts</param>
        /// <param name="finalizers">List of finalizers to run before returning</param>
        public HashSet<string> PatchAll(IDictionary<string, AssemblyDefinition> assemblies)
        {
            HashSet<string> patchedAssemblies = new HashSet<string>();

            //call the patchers on the assemblies
	        foreach (var patcherMethod in Patchers)
	        {
		        foreach (string assemblyFilename in patcherMethod.Value)
		        {
		            if (assemblies.TryGetValue(assemblyFilename, out var assembly))
		            {
		                Patch(ref assembly, patcherMethod.Key);
			            assemblies[assemblyFilename] = assembly;
		                patchedAssemblies.Add(assemblyFilename);
                    }
		        }
	        }

            return patchedAssemblies;
        }

        public void FinalizePatching()
        {
            //run all finalizers
            foreach (var finalizer in Finalizers)
                finalizer.Invoke();
        }

		/// <summary>
		/// Patches an individual assembly, without loading it.
		/// </summary>
		/// <param name="assembly">The assembly definition to apply the patch to.</param>
		/// <param name="patcherMethod">The patcher to use to patch the assembly definition.</param>
        private void Patch(ref AssemblyDefinition assembly, AssemblyPatcherDelegate patcherMethod)
        {
	        patcherMethod.Invoke(ref assembly);
        }

		/// <summary>
		/// Loads an individual assembly defintion into the CLR.
		/// </summary>
		/// <param name="assembly">The assembly to load.</param>
	    public static void Load(AssemblyDefinition assembly)
	    {
		    using (MemoryStream assemblyStream = new MemoryStream())
		    {
			    assembly.Write(assemblyStream);
			    Assembly.Load(assemblyStream.ToArray());
		    }
	    }
    }
}