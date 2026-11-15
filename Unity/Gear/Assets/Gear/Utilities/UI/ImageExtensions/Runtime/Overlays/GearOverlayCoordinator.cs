using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>
    /// 覆盖层协调器：owner 入栈，Esc 只交给栈顶；同一 owner 再打开会先关闭旧项。
    /// 可选打开/关闭音效；不依赖场景单例，纯静态栈可在 EditMode 测试。
    /// </summary>
    public static class GearOverlayCoordinator
    {
        private sealed class Entry
        {
            public object Owner;
            public Action Close;
            public string Id;
        }

        private static readonly List<Entry> stack = new List<Entry>();
        private static AudioSource audioSource;
        private static AudioClip openClip;
        private static AudioClip closeClip;

        public static int Count => stack.Count;
        public static object TopOwner => stack.Count > 0 ? stack[stack.Count - 1].Owner : null;

        public static void ConfigureAudio(AudioSource source, AudioClip open = null, AudioClip close = null)
        {
            audioSource = source;
            openClip = open;
            closeClip = close;
        }

        /// <summary>打开一个覆盖层；同一 owner 的旧项会先关闭。</summary>
        public static void Open(object owner, Action close, string id = null)
        {
            if (owner == null) return;

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(stack[i].Owner, owner)) continue;
                Action oldClose = stack[i].Close;
                stack.RemoveAt(i);
                oldClose?.Invoke();
            }

            stack.Add(new Entry { Owner = owner, Close = close, Id = id });
            Play(openClip);
        }

        /// <summary>释放 owner；不调用关闭回调（用于对象已销毁时清理）。</summary>
        public static void Release(object owner)
        {
            if (owner == null) return;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(stack[i].Owner, owner)) stack.RemoveAt(i);
            }
        }

        /// <summary>
        /// Esc 仲裁：栈为空返回 true（旧覆盖层继续自行处理）；
        /// 栈顶 owner 匹配时关闭栈顶并消费；其他 owner 返回 false。
        /// </summary>
        public static bool TryHandleEscape(object owner)
        {
            if (stack.Count == 0) return true;
            Entry top = stack[stack.Count - 1];
            if (!ReferenceEquals(top.Owner, owner)) return false;

            stack.RemoveAt(stack.Count - 1);
            top.Close?.Invoke();
            Play(closeClip);
            return true;
        }

        public static bool CloseTop()
        {
            if (stack.Count == 0) return false;
            Entry top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            top.Close?.Invoke();
            Play(closeClip);
            return true;
        }

        public static void CloseAll()
        {
            while (stack.Count > 0) CloseTop();
        }

        public static bool IsOpen(object owner)
        {
            for (int i = 0; i < stack.Count; i++)
                if (ReferenceEquals(stack[i].Owner, owner)) return true;
            return false;
        }

        public static void Reset()
        {
            stack.Clear();
            audioSource = null;
            openClip = null;
            closeClip = null;
        }

        private static void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
