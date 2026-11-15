using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.Backdrop
{
    /// <summary>
    /// 共享背景抓屏：多个 UIBackdropBlur 命中同一 source camera / 分辨率 / cullingMask 时只创建
    /// 一台隐藏相机和一张 capture RT；每个面板仍各自做 ping-pong 模糊。
    /// </summary>
    public static class UIBackdropSharedCapture
    {
        private sealed class Entry
        {
            public Camera Camera;
            public RenderTexture Texture;
            public int References;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        public static int EntryCount => Entries.Count;

        public static Camera Acquire(Camera source, int downsample, int layerMask, int width, int height,
            out RenderTexture texture)
        {
            texture = null;
            if (source == null) return null;

            string key = source.GetInstanceID() + ":" + downsample + ":" + layerMask + ":" + width + "x" + height;
            if (Entries.TryGetValue(key, out Entry existing) && existing.Camera != null)
            {
                existing.References++;
                texture = existing.Texture;
                return existing.Camera;
            }

            var go = new GameObject("~UIBackdropSharedCapture")
            {
                hideFlags = HideFlags.HideAndDontSave,
                transform = { parent = null },
            };
            Camera camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.cullingMask = layerMask;
            camera.depth = source.depth - 0.5f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.enabled = false;

            RenderTexture rt = new RenderTexture(width, height, 16, RenderTextureFormat.Default)
            {
                name = "UI Backdrop Shared Capture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            camera.targetTexture = rt;

            Entries[key] = new Entry { Camera = camera, Texture = rt, References = 1 };
            texture = rt;
            return camera;
        }

        public static void Release(Camera camera)
        {
            if (camera == null) return;
            string foundKey = null;
            foreach (var pair in Entries)
            {
                if (pair.Value.Camera != camera) continue;
                foundKey = pair.Key;
                pair.Value.References--;
                if (pair.Value.References > 0) return;
                break;
            }

            if (foundKey == null) return;
            Entry entry = Entries[foundKey];
            Entries.Remove(foundKey);
            if (entry.Texture != null)
            {
                entry.Texture.Release();
                DestroyObject(entry.Texture);
            }

            if (entry.Camera != null) DestroyObject(entry.Camera.gameObject);
        }

        public static void RequestFrame(Camera camera, bool shouldRender)
        {
            if (camera != null) camera.enabled = shouldRender;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (var pair in Entries)
            {
                if (pair.Value.Camera != null) DestroyObject(pair.Value.Camera.gameObject);
                if (pair.Value.Texture != null) DestroyObject(pair.Value.Texture);
            }
            Entries.Clear();
        }

        private static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
