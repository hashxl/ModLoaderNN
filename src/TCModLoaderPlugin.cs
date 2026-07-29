using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Modding;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TCModLoader
{
    public class TCModLoaderPlugin : MonoBehaviour
    {
        internal static LoaderLog Log;
        internal static TCModLoaderPlugin Instance;

        private readonly List<LoadedMod> _loadedMods = new List<LoadedMod>();
        private bool _framePumpCreated;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                Log.LogWarning("=== TCModLoader v1.0.0 starting ===");

                var selfAsm = typeof(TCModLoaderPlugin).Assembly;
                var itcType = selfAsm.GetType("ITCMod");
                Log.LogInfo($"Self-check: ITCMod = {itcType?.FullName ?? "NOT FOUND"} in {selfAsm.GetName().Name} v{selfAsm.GetName().Version}");

                LoadAllMods();
                Log.LogWarning($"=== TCModLoader ready: {_loadedMods.Count} mod(s) loaded ===");
            }
            catch (Exception ex)
            {
                Log.LogError($"TCModLoader FATAL: {ex}");
            }

            // This plugin's own GameObject is created during the game's initial
            // bootstrap scene, and the bootstrap-to-MainMenu transition destroys
            // every DontDestroyOnLoad object created before it (confirmed via a
            // test mod: Awake/OnEnable fired, then OnDisable/OnDestroy fired again
            // a few log lines later, right as Rewired/PhysX/Steam init and the
            // scene changed to MainMenu) — which is almost certainly why Update()
            // below never actually ran and ITCMod.OnFrame() never fired for any
            // mod. SceneManager.sceneLoaded is a static event, unaffected by that
            // wipe, so we use it to (re)create a separate frame-pump GameObject
            // once the wipe has already happened.
            SceneManager.sceneLoaded += CreateFramePumpOnce;
        }

        private void CreateFramePumpOnce(Scene scene, LoadSceneMode mode)
        {
            if (_framePumpCreated) return;
            _framePumpCreated = true;

            var pumpGo = new GameObject("TCModLoader_FramePump");
            DontDestroyOnLoad(pumpGo);
            pumpGo.AddComponent<FramePump>();
            Log.LogInfo("TCModLoader: frame pump created (post-bootstrap).");
        }

        // Reads the static Instance instead of holding its own reference: Instance
        // is set at the very top of Awake(), before SceneManager.sceneLoaded is
        // even subscribed, so it's guaranteed non-null by the time any scene load
        // could trigger CreateFramePumpOnce. A previous version used a plain
        // instance field (Owner) assigned right after AddComponent, which should
        // have been just as safe but produced a NullReferenceException on Owner
        // in practice — using the static field removes that whole class of bug.
        private class FramePump : MonoBehaviour
        {
            private void Update()
            {
                try { Instance?.RunModFrames(); }
                catch (Exception ex) { Log?.LogError($"FramePump.Update: {ex}"); }
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

            var managedDir = StandalonePaths.ManagedDirectory;
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
            RunModFrames();
        }

        private int _lastFrameRun = -1;

        // Guarded by frame number in case both this plugin's own Update() and
        // FramePump.Update() end up alive at the same time (e.g. if the
        // bootstrap-wipe theory above turns out not to apply in some scenario) —
        // without this, mods' OnFrame() would fire twice per frame.
        internal void RunModFrames()
        {
            if (Time.frameCount == _lastFrameRun) return;
            _lastFrameRun = Time.frameCount;

            if (_loadedMods.Count == 0) return;

            foreach (var mod in _loadedMods)
            {
                try { mod.Instance.OnFrame(); }
                catch (Exception ex) { Log.LogError($"[{mod.Manifest.Name}] OnFrame: {ex}"); }
            }
        }

        private void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= CreateFramePumpOnce;
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
            var modsDir = StandalonePaths.ModsDirectory;

            if (!Directory.Exists(modsDir))
            {
                Log.LogInfo($"Creating mods folder: {modsDir}");
                Directory.CreateDirectory(modsDir);
            }

            Log.LogInfo($"Scanning: {modsDir}");

            var discovered = new List<DiscoveredMod>();
            foreach (var dir in Directory.GetDirectories(modsDir))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                var mod = TryDiscoverMod(dir, manifestPath);
                if (mod != null) discovered.Add(mod);
            }

            List<DiscoveredMod> loadOrder;
            try
            {
                loadOrder = ModDependencyResolver.Resolve(discovered);
            }
            catch (ModDependencyException ex)
            {
                ShowFatalDependencyErrorAndQuit(ex.Errors);
                return; // unreachable: Environment.Exit terminates the process
            }

            foreach (var mod in loadOrder)
            {
                try { InstantiateMod(mod); }
                catch (Exception ex) { Log.LogError($"Mod load failed [{mod.Manifest.Name}]: {ex}"); }
            }

            var skipped = discovered.Count - loadOrder.Count;
            if (skipped > 0)
                Log.LogInfo($"{skipped} mod(s) disabled via manifest, not loaded.");
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONERROR = 0x00000010;

        private static void ShowFatalDependencyErrorAndQuit(IReadOnlyList<string> errors)
        {
            var message = "The game could not be started because one or more mods have unmet dependencies:\n\n"
                + string.Join("\n", errors.Select(e => "- " + e));

            Log?.LogError(message);
            MessageBox(IntPtr.Zero, message, "TCModLoader - Dependency Error", MB_OK | MB_ICONERROR);
            Environment.Exit(1);
        }

        private DiscoveredMod TryDiscoverMod(string modDir, string manifestPath)
        {
            ModManifest manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = JsonConvert.DeserializeObject<ModManifest>(json);
            }
            catch (Exception ex)
            {
                Log.LogError($"Invalid manifest JSON [{Path.GetFileName(modDir)}]: {ex.Message}");
                return null;
            }

            if (manifest == null || string.IsNullOrEmpty(manifest.PathToDLL))
            {
                Log.LogWarning($"Invalid manifest: {Path.GetFileName(modDir)}");
                return null;
            }

            manifest.ModPath = modDir;
            manifest.SpriteResolver = new ModSpriteResolver(modDir);

            var dllPath = Path.Combine(modDir, manifest.PathToDLL);
            if (!File.Exists(dllPath))
            {
                Log.LogError($"DLL not found: {dllPath}");
                return null;
            }

            var id = string.IsNullOrEmpty(manifest.UniqueIdentifier) ? manifest.Name : manifest.UniqueIdentifier;
            return new DiscoveredMod { ModDir = modDir, ManifestPath = manifestPath, Manifest = manifest, Id = id };
        }

        private void InstantiateMod(DiscoveredMod mod)
        {
            var manifest = mod.Manifest;
            var dllPath = Path.Combine(mod.ModDir, manifest.PathToDLL);

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
