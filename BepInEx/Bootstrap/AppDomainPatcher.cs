using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Contract;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
    internal static class AppDomainPatcher
    {
        public static HashSet<string> Run(Dictionary<string, AssemblyDefinition> assemblies)
        {
            var domain = MonoDomain.Create("PatcherDomain");

            var runPatcher = typeof(AppDomainPatcher).GetMethod("RunPatcher", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (HashSet<string>) MonoDomain.InvokeInDomain(MonoDomain.GetMonoDomainId(domain),
                                                                     runPatcher,
                                                                     null,
                                                                     new object[] {Paths.ExecutablePath, Logger.CurrentLogger, assemblies});

            MonoDomain.Unload(domain);
            return result;
        }

        private static HashSet<string> RunPatcher(string path, BaseLogger logger, Dictionary<string, AssemblyDefinition> assemblies)
        {
            Paths.ExecutablePath = path;
            AppDomain.CurrentDomain.AssemblyResolve += Entrypoint.LocalResolve;
            Logger.SetLogger(logger);

            var processor = new PatcherProcessor();
            processor.AddPatchersFromDirectory(Paths.PluginPath, GetInterfacePatchers);

            processor.InitializePatching();
            var result = processor.PatchAll(assemblies);
            processor.FinalizePatching();
            return result;
        }

        private static List<AssemblyPatcher> GetInterfacePatchers(Assembly assembly)
        {
            var result = new List<AssemblyPatcher>();
            foreach (var type in assembly
                                 .GetExportedTypes().Where(t => typeof(IAssemblyPatcher).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
                try
                {
                    var patcherInstance = (IAssemblyPatcher) Activator.CreateInstance(type);
                    var patcher = new AssemblyPatcher
                    {
                            Name = $"{assembly.GetName().Name}.{type.FullName}",
                            Initializer = patcherInstance.Initialize,
                            Finalizer = patcherInstance.Finish,
                            Patcher = (ref AssemblyDefinition ass) => patcherInstance.Patch(ass),
                            TargetDLLs = patcherInstance.TargetDLLs
                    };
                    result.Add(patcher);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, $"Could not load patcher methods from {assembly.GetName().Name}");
                    Logger.Log(LogLevel.Warning, $"{ex}");
                }

            return result;
        }
    }
}

internal sealed class MonoDomain
{
    public delegate object InvokeInDomainDelegate(int domainId, MethodInfo method, object obj, object[] args);

    public static readonly InvokeInDomainDelegate InvokeInDomain;

    static MonoDomain()
    {
        var invokeInDomainById = typeof(AppDomain).GetMethod("InvokeInDomainByID", BindingFlags.Static | BindingFlags.NonPublic);
        InvokeInDomain = (InvokeInDomainDelegate) Delegate.CreateDelegate(typeof(InvokeInDomainDelegate), invokeInDomainById);
    }

    [DllImport("mono.dll", EntryPoint = "mono_domain_get_id")]
    public static extern int GetMonoDomainId(IntPtr domain);

    public static IntPtr Create(string friendlyName)
    {
        return CreateMonoDomain(friendlyName, null);
    }

    public static void Unload(IntPtr nativeDomain)
    {
        UnloadMonoDomain(nativeDomain);
    }

    [DllImport("mono.dll", EntryPoint = "mono_domain_unload")]
    private static extern int UnloadMonoDomain(IntPtr domain);

    [DllImport("mono.dll", EntryPoint = "mono_domain_create_appdomain")]
    private static extern IntPtr CreateMonoDomain([MarshalAs(UnmanagedType.LPStr)] string friendly_name,
                                                  [MarshalAs(UnmanagedType.LPStr)] string configuration_file);
}