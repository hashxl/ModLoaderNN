using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace TCModLoader
{
    internal static class ModAssemblyPatcher
    {
        private const string GameAssembly = "Assembly-CSharp";
        private const string LoaderAssembly = "TCModLoader";

        /// <summary>Bump this whenever the patching rules in PatchAssembly change, to invalidate stale cache entries.</summary>
        private const string PatcherVersion = "1";

        internal static byte[] PatchModDll(string dllPath, LoaderLog log)
        {
            var sourceBytes = File.ReadAllBytes(dllPath);
            var cacheKey = ComputeCacheKey(sourceBytes);

            var cacheDir = StandalonePaths.CacheDirectory;
            var cachePath = Path.Combine(cacheDir, cacheKey + ".dll");

            if (File.Exists(cachePath))
            {
                log.LogInfo($"  Using cached patch for {Path.GetFileName(dllPath)} ({cacheKey.Substring(0, 8)})");
                return File.ReadAllBytes(cachePath);
            }

            var patchedBytes = PatchAssembly(dllPath, log);

            try
            {
                Directory.CreateDirectory(cacheDir);
                File.WriteAllBytes(cachePath, patchedBytes);
            }
            catch (Exception ex)
            {
                log.LogWarning($"  Failed to write patch cache for {Path.GetFileName(dllPath)}: {ex.Message}");
            }

            return patchedBytes;
        }

        private static string ComputeCacheKey(byte[] sourceBytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var versionBytes = Encoding.UTF8.GetBytes(PatcherVersion);
                var combined = new byte[sourceBytes.Length + versionBytes.Length];
                Buffer.BlockCopy(sourceBytes, 0, combined, 0, sourceBytes.Length);
                Buffer.BlockCopy(versionBytes, 0, combined, sourceBytes.Length, versionBytes.Length);

                var hash = sha256.ComputeHash(combined);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static byte[] PatchAssembly(string dllPath, LoaderLog log)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath));

            var managedDir = StandalonePaths.ManagedDirectory;
            if (Directory.Exists(managedDir))
                resolver.AddSearchDirectory(managedDir);

            resolver.AddSearchDirectory(StandalonePaths.RuntimeDirectory);

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
