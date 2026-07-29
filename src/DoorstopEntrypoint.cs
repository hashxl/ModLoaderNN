using System;
using System.IO;
using System.Reflection;
using TCModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Doorstop
{
    public static class Entrypoint
    {
        private static LoaderLog _log;

        public static void Start()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveLoaderDependency;

            _log = new LoaderLog(StandalonePaths.LogFile);
            TCModLoaderPlugin.Log = _log;

            try
            {
                _log.LogWarning("=== TCModLoader standalone bootstrap starting ===");
                SceneManager.sceneLoaded += StartLoaderOnFirstScene;
                _log.LogInfo("Waiting for Unity's first scene before creating the loader host.");
            }
            catch (Exception ex)
            {
                _log.LogError($"Standalone bootstrap failed: {ex}");
                ShowBootstrapError(ex);
            }
        }

        private static void StartLoaderOnFirstScene(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= StartLoaderOnFirstScene;

            try
            {
                _log.LogInfo($"Unity ready on scene '{scene.name}'. Creating loader host.");
                var host = new GameObject("TCModLoader");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<TCModLoaderPlugin>();
            }
            catch (Exception ex)
            {
                _log.LogError($"Loader host creation failed: {ex}");
                ShowBootstrapError(ex);
            }
        }

        private static Assembly ResolveLoaderDependency(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            var candidates = new[]
            {
                Path.Combine(StandalonePaths.RuntimeDirectory, name),
                Path.Combine(StandalonePaths.ManagedDirectory, name)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);
            }

            return null;
        }

        private static void ShowBootstrapError(Exception ex)
        {
            try
            {
                System.Windows.Forms.MessageBox.Show(
                    "TCModLoader could not start.\n\n" + ex.Message +
                    "\n\nSee TCModLoader\\Logs\\TCModLoader.log for details.",
                    "TCModLoader - Bootstrap Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
            catch
            {
                // The file log already contains the full failure.
            }
        }
    }
}
