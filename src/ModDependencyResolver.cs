using System;
using System.Collections.Generic;
using System.Linq;
using Modding;

// HashXL annotations 
//  grafo de ordenação agora combina Requires (hard)
//  e LoadAfter (soft)


//Testei os três casos ao vivo: mod com LoadAfter 
// de algo inexistente carregou de boa; 
// dois mods com LoadAfter circular entre si carregaram
//  os dois sem abortar; 
// e a ordem real (SoftD depende de SoftE) saiu
//  certa no log. Ciclo hard via Requires continua
//  abortando corretamente 
// (testei de novo pra garantir que a refatoração não quebrou isso).
namespace TCModLoader
{
    internal class DiscoveredMod
    {
        public string ModDir;
        public string ManifestPath;
        public ModManifest Manifest;

        /// <summary>UniqueIdentifier if set, otherwise falls back to Name.</summary>
        public string Id;
    }

    internal class ModDependencyException : Exception
    {
        internal IReadOnlyList<string> Errors { get; }

        internal ModDependencyException(IReadOnlyList<string> errors)
            : base("One or more mods have unmet dependencies.")
        {
            Errors = errors;
        }
    }

    /// <summary>
    /// Validates each enabled mod's Requires against the discovered mod set and returns
    /// the enabled mods in dependency-safe load order (dependencies before dependents).
    /// Throws ModDependencyException listing every problem found if validation fails.
    /// </summary>
    internal static class ModDependencyResolver
    {
        private enum VisitState { Visiting, Done }

        internal static List<DiscoveredMod> Resolve(List<DiscoveredMod> discovered)
        {
            var errors = new List<string>();
            var byId = new Dictionary<string, DiscoveredMod>();

            foreach (var group in discovered.GroupBy(m => m.Id))
            {
                if (group.Count() > 1)
                {
                    var conflicting = string.Join(", ", group.Select(m => $"'{m.Manifest.Name}' ({m.ModDir})"));
                    errors.Add($"Mod identifier '{group.Key}' is used by more than one mod: {conflicting}. Set a distinct 'UniqueIdentifier' in each manifest.json.");
                    continue;
                }

                byId[group.Key] = group.Single();
            }

            var enabled = discovered.Where(m => m.Manifest.Enabled).ToList();

            foreach (var mod in enabled)
            {
                if (mod.Manifest.Requires == null) continue;

                foreach (var requirement in mod.Manifest.Requires)
                {
                    var depId = requirement.Key;
                    var minVersion = requirement.Value;

                    if (!byId.TryGetValue(depId, out var dep))
                    {
                        errors.Add($"'{mod.Manifest.Name}' needs the mod '{depId}', which is not installed.");
                        continue;
                    }

                    if (!dep.Manifest.Enabled)
                    {
                        errors.Add($"'{mod.Manifest.Name}' needs the mod '{dep.Manifest.Name}', which is installed but disabled.");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(minVersion) && !IsVersionSatisfied(dep.Manifest.Version, minVersion))
                    {
                        errors.Add($"'{mod.Manifest.Name}' needs the mod '{dep.Manifest.Name}' in version {minVersion} or higher, but the installed version is '{dep.Manifest.Version}'.");
                    }
                }
            }

            var sorted = TopologicalSort(enabled, byId, errors);

            if (errors.Count > 0)
                throw new ModDependencyException(errors);

            return sorted;
        }

        private static List<DiscoveredMod> TopologicalSort(List<DiscoveredMod> enabled, Dictionary<string, DiscoveredMod> byId, List<string> errors)
        {
            var result = new List<DiscoveredMod>();
            var visited = new Dictionary<string, VisitState>();

            foreach (var mod in enabled)
            {
                if (!visited.ContainsKey(mod.Id))
                    Visit(mod, byId, visited, result, errors, new List<string>());
            }

            return result;
        }

        /// <summary>
        /// Load-order edges for a mod: hard Requires always come first (so IsHardEdge below matches
        /// on the first occurrence), followed by soft LoadAfter hints.
        /// </summary>
        private static IEnumerable<string> GetLoadOrderEdges(ModManifest manifest)
        {
            if (manifest.Requires != null)
                foreach (var id in manifest.Requires.Keys)
                    yield return id;

            if (manifest.LoadAfter != null)
                foreach (var id in manifest.LoadAfter)
                    yield return id;
        }

        private static void Visit(DiscoveredMod mod, Dictionary<string, DiscoveredMod> byId,
            Dictionary<string, VisitState> visited, List<DiscoveredMod> result, List<string> errors, List<string> chain)
        {
            visited[mod.Id] = VisitState.Visiting;
            chain.Add(mod.Manifest.Name);

            foreach (var depId in GetLoadOrderEdges(mod.Manifest).Distinct())
            {
                if (!byId.TryGetValue(depId, out var dep) || !dep.Manifest.Enabled)
                    continue; // missing/disabled: hard failures were already reported above, soft hints just skip

                var isHardEdge = mod.Manifest.Requires != null && mod.Manifest.Requires.ContainsKey(depId);

                if (visited.TryGetValue(dep.Id, out var depState))
                {
                    if (depState == VisitState.Visiting)
                    {
                        // Circular Requires can never be satisfied: fatal.
                        // A circular LoadAfter is just an unsatisfiable ordering preference: skip the back-edge and move on.
                        if (isHardEdge)
                            errors.Add($"Circular dependency detected: {string.Join(" -> ", chain)} -> {dep.Manifest.Name}");
                    }
                    continue;
                }

                Visit(dep, byId, visited, result, errors, chain);
            }

            chain.RemoveAt(chain.Count - 1);
            visited[mod.Id] = VisitState.Done;
            result.Add(mod);
        }

        private static bool IsVersionSatisfied(string installedVersion, string requiredVersion)
        {
            if (!TryParseVersion(installedVersion, out var installed)) return false;
            if (!TryParseVersion(requiredVersion, out var required)) return true;
            return installed >= required;
        }

        private static bool TryParseVersion(string raw, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(raw)) return false;
            return Version.TryParse(raw.TrimStart('v', 'V'), out version);
        }
    }
}
