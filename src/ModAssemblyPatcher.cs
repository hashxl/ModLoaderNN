using System;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;

namespace TCModLoader
{
    internal static class ModAssemblyPatcher
    {
        private const string GameAssembly = "Assembly-CSharp";
        private const string LoaderAssembly = "TCModLoader";

        internal static byte[] PatchModDll(string dllPath, ManualLogSource log)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath));

            var managedDir = Path.Combine(UnityEngine.Application.dataPath, "Managed");
            if (Directory.Exists(managedDir))
                resolver.AddSearchDirectory(managedDir);

            var bepInExCore = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "BepInEx", "core");
            if (Directory.Exists(bepInExCore))
                resolver.AddSearchDirectory(bepInExCore);

            var pluginsDir = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "BepInEx", "plugins");
            if (Directory.Exists(pluginsDir))
                resolver.AddSearchDirectory(pluginsDir);

            using (var module = ModuleDefinition.ReadModule(dllPath,
                new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false }))
            {
                log.LogInfo($"  Assembly refs: {string.Join(", ", module.AssemblyReferences.Select(r => r.Name))}");

                log.LogInfo("  All typerefs from Assembly-CSharp:");
                foreach (var tr in module.GetTypeReferences())
                {
                    if (tr.Scope is AssemblyNameReference ar && ar.Name == GameAssembly)
                        log.LogInfo($"    NS='{tr.Namespace}' Name='{tr.Name}' Full='{tr.FullName}'");
                }

                var loaderRef = new AssemblyNameReference(LoaderAssembly, new Version(1, 0, 0, 0));
                module.AssemblyReferences.Add(loaderRef);

                int patchCount = 0;

                foreach (var typeRef in module.GetTypeReferences())
                {
                    if (!(typeRef.Scope is AssemblyNameReference asmRef)) continue;
                    if (asmRef.Name != GameAssembly) continue;

                    var ns = typeRef.Namespace ?? "";
                    var name = typeRef.Name ?? "";

                    if (ns == "Modding" || name == "ITCMod" || name == "ModManifest" || name == "ModSpriteResolver")
                    {
                        log.LogInfo($"  Patching typeref: {typeRef.FullName} ({asmRef.Name} -> {LoaderAssembly})");
                        typeRef.Scope = loaderRef;
                        patchCount++;
                    }
                }

                foreach (var memberRef in module.GetMemberReferences())
                {
                    if (memberRef.DeclaringType == null) continue;
                    if (!(memberRef.DeclaringType.Scope is AssemblyNameReference asmRef)) continue;
                    if (asmRef.Name != GameAssembly) continue;

                    var ns = memberRef.DeclaringType.Namespace ?? "";
                    var name = memberRef.DeclaringType.Name ?? "";

                    if (ns == "Modding" || name == "ITCMod" || name == "ModManifest" || name == "ModSpriteResolver")
                    {
                        log.LogInfo($"  Patching memberref: {memberRef.DeclaringType.FullName}::{memberRef.Name} ({asmRef.Name} -> {LoaderAssembly})");
                        memberRef.DeclaringType.Scope = loaderRef;
                        patchCount++;
                    }
                }

                log.LogInfo($"  Patched {patchCount} references");

                using (var ms = new MemoryStream())
                {
                    module.Write(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
