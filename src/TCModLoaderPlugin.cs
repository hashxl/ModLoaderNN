using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Asuna.Dialogues;
using BepInEx;
using BepInEx.Logging;
using Modding;
using Newtonsoft.Json;
using UnityEngine;

namespace TCModLoader
{
    [BepInPlugin("com.tcmodloader.bridge", "TCModLoader", "1.0.0")]
    public class TCModLoaderPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static TCModLoaderPlugin Instance;

        private readonly List<LoadedMod> _loadedMods = new List<LoadedMod>();
        private bool _eventsHooked;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            DontDestroyOnLoad(gameObject);

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                Logger.LogWarning("=== TCModLoader v1.0.0 starting ===");

                var selfAsm = typeof(TCModLoaderPlugin).Assembly;
                var itcType = selfAsm.GetType("ITCMod");
                Logger.LogInfo($"Self-check: ITCMod = {itcType?.FullName ?? "NOT FOUND"} in {selfAsm.GetName().Name} v{selfAsm.GetName().Version}");

                LoadAllMods();
                Logger.LogWarning($"=== TCModLoader ready: {_loadedMods.Count} mod(s) loaded ===");
            }
            catch (Exception ex)
            {
                Logger.LogError($"TCModLoader FATAL: {ex}");
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (name.Name == "TCModLoader")
                return typeof(TCModLoaderPlugin).Assembly;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == name.Name)
                    return asm;
            }

            var managedDir = Path.Combine(UnityEngine.Application.dataPath, "Managed");
            var dllPath = Path.Combine(managedDir, name.Name + ".dll");
            if (File.Exists(dllPath))
            {
                Log.LogInfo($"  AssemblyResolve: loading {name.Name} from disk");
                return Assembly.LoadFrom(dllPath);
            }

            Log.LogWarning($"  AssemblyResolve FAILED: {name.Name}");
            return null;
        }

        private void Update()
        {
            if (_loadedMods.Count == 0) return;

            if (!_eventsHooked)
            {
                try
                {
                    HookDialogueEvents();
                    _eventsHooked = true;
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed to hook events: {ex}");
                    _eventsHooked = true;
                }
            }

            foreach (var mod in _loadedMods)
            {
                try { mod.Instance.OnFrame(); }
                catch (Exception ex) { Log.LogError($"[{mod.Manifest.Name}] OnFrame: {ex}"); }
            }
        }

        private void HookDialogueEvents()
        {
            DialogueManager.OnDialogueStarted.AddListener(dialogue =>
            {
                foreach (var mod in _loadedMods)
                {
                    try { mod.Instance.OnDialogueStarted(dialogue); }
                    catch (Exception ex) { Log.LogError($"[{mod.Manifest.Name}] OnDialogueStarted: {ex}"); }
                }
            });

            DialogueManager.OnLineOpened.AddListener(line =>
            {
                foreach (var mod in _loadedMods)
                {
                    try { mod.Instance.OnLineStarted(line); }
                    catch (Exception ex) { Log.LogError($"[{mod.Manifest.Name}] OnLineStarted: {ex}"); }
                }
            });

            Log.LogInfo("Hooked into DialogueManager events");
        }

        private void OnApplicationQuit()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            foreach (var mod in _loadedMods)
            {
                try { mod.Instance.OnModUnLoaded(); }
                catch (Exception ex) { Log.LogError($"Unload error [{mod.Manifest.Name}]: {ex}"); }
            }
            _loadedMods.Clear();
        }

        private void LoadAllMods()
        {
            var gameDir = Path.GetDirectoryName(Application.dataPath);
            Log.LogInfo($"Scanning: {gameDir}");

            foreach (var dir in Directory.GetDirectories(gameDir))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                try { LoadMod(dir, manifestPath); }
                catch (Exception ex) { Log.LogError($"Mod load failed [{Path.GetFileName(dir)}]: {ex}"); }
            }
        }

        private void LoadMod(string modDir, string manifestPath)
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

            if (manifest == null || string.IsNullOrEmpty(manifest.PathToDLL))
            {
                Log.LogWarning($"Invalid manifest: {Path.GetFileName(modDir)}");
                return;
            }

            manifest.ModPath = modDir;
            manifest.SpriteResolver = new ModSpriteResolver(modDir);

            var dllPath = Path.Combine(modDir, manifest.PathToDLL);
            if (!File.Exists(dllPath))
            {
                Log.LogError($"DLL not found: {dllPath}");
                return;
            }

            Log.LogInfo($"Patching: {manifest.Name}...");
            var patchedBytes = ModAssemblyPatcher.PatchModDll(dllPath, Log);

            Log.LogInfo($"Loading: {manifest.Name}...");
            var assembly = Assembly.Load(patchedBytes);

            Log.LogInfo($"Searching for ITCMod in {assembly.GetName().Name}...");

            Type modType = null;
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(ITCMod).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        modType = type;
                        break;
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Log.LogError($"GetTypes failed. Loader exceptions:");
                foreach (var le in ex.LoaderExceptions)
                    Log.LogError($"  {le.Message}");

                foreach (var type in ex.Types.Where(t => t != null))
                {
                    try
                    {
                        if (typeof(ITCMod).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            modType = type;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (modType == null)
            {
                Log.LogError($"No ITCMod found in {manifest.PathToDLL}");
                return;
            }

            Log.LogInfo($"Creating: {modType.FullName}...");
            Log.LogInfo($"  Interfaces: {string.Join(", ", modType.GetInterfaces().Select(i => i.FullName + " in " + i.Assembly.GetName().Name))}");
            Log.LogInfo($"  BaseType: {modType.BaseType?.FullName}");
            var modInstance = (ITCMod)Activator.CreateInstance(modType);

            Log.LogInfo($"OnModLoaded: {manifest.Name}...");
            modInstance.OnModLoaded(manifest);

            _loadedMods.Add(new LoadedMod { Manifest = manifest, Instance = modInstance });
            Log.LogWarning($"=== Loaded: {manifest.Name} v{manifest.Version} by {manifest.Author} ===");
        }
    }

    internal class LoadedMod
    {
        public ModManifest Manifest;
        public ITCMod Instance;
    }
}
