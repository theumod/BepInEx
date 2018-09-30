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

    internal class AssemblyPatcher
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
    internal class PatcherProcessor
    {
        /// <summary>
        ///     The dictionary of currently loaded patchers. The key is the patcher delegate that will be used to patch, and the
        ///     value is a list of filenames of assemblies that the patcher is targeting.
        /// </summary>
        private Dictionary<AssemblyPatcherDelegate, IEnumerable<string>> Patchers { get; } =
            new Dictionary<AssemblyPatcherDelegate, IEnumerable<string>>();

        private List<Action> Initializers { get; } = new List<Action>();

        private List<Action> Finalizers { get; } = new List<Action>();

        public PatcherProcessor()
        {

        }

        public void AddPatcher(AssemblyPatcher patcher)
        {
            if (patcher.TargetDLLs != null && patcher.Patcher != null)
                Patchers[patcher.Patcher] = patcher.TargetDLLs;
            if(patcher.Initializer != null)
                Initializers.Add(patcher.Initializer);
            if(patcher.Finalizer != null)
                Finalizers.Add(patcher.Finalizer);
        }

        public void AddPatchersFromDirectory(string directory, Func<Assembly, List<AssemblyPatcher>> patcherLocator)
        {
            if (!Directory.Exists(directory))
                return;

            var sortedPatchers = new SortedDictionary<string, AssemblyPatcher>();

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);

                    foreach (var patcher in patcherLocator(assembly))
                        sortedPatchers.Add(patcher.Name, patcher);
                }
                catch (BadImageFormatException) { } //unmanaged DLL
                catch (ReflectionTypeLoadException) { } //invalid references

            foreach (var patcher in sortedPatchers)
                AddPatcher(patcher.Value);
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
    }
}