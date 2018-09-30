using System.Collections.Generic;
using Mono.Cecil;

namespace BepInEx.Contract
{
    interface ICecilPatch
    {
        IEnumerable<string> TargetDLLs { get; }
        void Initialize();
        void Patch(AssemblyDefinition assembly);
        void Finalize();
    }
}
