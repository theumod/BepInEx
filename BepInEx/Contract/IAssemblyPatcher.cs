using System.Collections.Generic;
using Mono.Cecil;

namespace BepInEx.Contract
{
    public interface IAssemblyPatcher
    {
        IEnumerable<string> TargetDLLs { get; }
        void Initialize();
        void Patch(AssemblyDefinition assembly);
        void Finish();
    }
}
