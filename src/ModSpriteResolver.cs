using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ANToolkit.ResourceManagement;
using UnityEngine;

namespace Modding
{
    public class ModSpriteResolver
    {
        private readonly string _basePath;
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        private static readonly FieldInfo SpriteRefField =
            typeof(ANResourceSprite).GetField("spriteReference",
                BindingFlags.NonPublic | BindingFlags.Instance);

        public ModSpriteResolver(string basePath)
        {
            _basePath = basePath;
        }

        public Sprite Resolve(string relativePath)
        {
            var key = relativePath.Replace("/", "\\");
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var fullPath = Path.Combine(_basePath, key);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[TCModLoader] Sprite not found: {fullPath}");
                return null;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(tex, bytes))
            {
                Debug.LogWarning($"[TCModLoader] Failed to load image: {fullPath}");
                return null;
            }

            tex.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = Path.GetFileNameWithoutExtension(relativePath);

            _cache[key] = sprite;
            return sprite;
        }

        public ANResourceSprite ResolveAsResource(string relativePath)
        {
            var sprite = Resolve(relativePath);
            if (sprite == null) return null;

            var resource = new ANResourceSprite();
            resource.MOD_ONLY_USE = true;
            resource.fullPath = Path.Combine(_basePath, relativePath.Replace("/", "\\"));
            resource.resourcePath = relativePath;

            if (SpriteRefField != null)
                SpriteRefField.SetValue(resource, sprite);

            return resource;
        }
    }
}
