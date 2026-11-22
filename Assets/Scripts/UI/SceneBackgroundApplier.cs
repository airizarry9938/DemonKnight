using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Applies Architect-assigned SceneBackgroundProfile entries (jpg/png or mp4)
    /// onto UI Images / SpriteRenderers / optional VideoPlayer hosts. Assign-only:
    /// empty slots leave the screen's existing default art alone.
    /// </summary>
    public static class SceneBackgroundApplier
    {
        static SceneBackgroundProfile _cache;

        public static void InvalidateCache() => _cache = null;

        public static SceneBackgroundProfile Profile
        {
            get
            {
                if (_cache == null)
                {
                    try { _cache = SceneBackgroundProfile.Load(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DemonKnight] SceneBackgroundProfile.Load soft-fail: " + ex.Message);
                        return null;
                    }
                }
                return _cache;
            }
        }

        public static SceneBackgroundEntry Get(SceneBackgroundSlot slot)
        {
            try { return Profile?.GetEntry(slot); }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] SceneBackgroundApplier.Get soft-fail: " + ex.Message);
                return null;
            }
        }

        /// <summary>Apply still sprite to a UI Image when the slot has an image assignment.</summary>
        public static bool ApplyToImage(SceneBackgroundSlot slot, Image target, bool preserveColor = true)
        {
            if (target == null) return false;
            var e = Get(slot);
            Sprite sp = ResolveSprite(e);
            if (sp == null) return false;
            target.sprite = sp;
            target.type = Image.Type.Simple;
            target.preserveAspect = true;
            if (!preserveColor) target.color = Color.white;
            else if (target.color.a < 0.05f) target.color = Color.white;
            return true;
        }

        /// <summary>Apply still sprite to a world SpriteRenderer.</summary>
        public static bool ApplyToSpriteRenderer(SceneBackgroundSlot slot, SpriteRenderer target)
        {
            if (target == null) return false;
            var e = Get(slot);
            Sprite sp = ResolveSprite(e);
            if (sp == null) return false;
            target.sprite = sp;
            target.color = Color.white;
            return true;
        }

        /// <summary>
        /// Prefer video when assigned; otherwise still image. Returns true if anything applied.
        /// Video plays into <paramref name="videoHost"/> (adds VideoPlayer + RawImage child if needed).
        /// </summary>
        public static bool ApplyToUiHost(
            SceneBackgroundSlot slot,
            Image imageTarget,
            RectTransform videoHostParent = null)
        {
            var e = Get(slot);
            if (e == null) return false;

            VideoClip clip = ResolveVideo(e);
            if (clip != null && videoHostParent != null)
            {
                EnsureUiVideo(videoHostParent, clip);
                if (imageTarget != null)
                    imageTarget.color = new Color(0.1f, 0.1f, 0.1f, 0.35f);
                return true;
            }

            return ApplyToImage(slot, imageTarget, preserveColor: false);
        }

        public static Sprite ResolveSprite(SceneBackgroundEntry e)
        {
            if (e == null) return null;
            if (e.imageSprite != null) return e.imageSprite;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(e.imageAssetPath))
            {
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(e.imageAssetPath);
                if (sp != null) return sp;
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(e.imageAssetPath);
                if (tex != null)
                {
                    // Fall back to first sprite sub-asset.
                    var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(e.imageAssetPath);
                    if (assets != null)
                    {
                        for (int i = 0; i < assets.Length; i++)
                            if (assets[i] is Sprite s) return s;
                    }
                }
            }
#endif
            return null;
        }

        public static VideoClip ResolveVideo(SceneBackgroundEntry e)
        {
            if (e == null) return null;
            if (e.videoClip != null) return e.videoClip;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(e.videoAssetPath))
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(e.videoAssetPath);
                if (clip != null) return clip;
            }
#endif
            // Resources fallback: path without extension under Resources.
            if (!string.IsNullOrEmpty(e.videoAssetPath))
            {
                string res = e.videoAssetPath.Replace("\\", "/");
                int ri = res.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                if (ri >= 0)
                {
                    res = res.Substring(ri + "/Resources/".Length);
                    if (res.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                        res = res.Substring(0, res.Length - 4);
                    var clip = Resources.Load<VideoClip>(res);
                    if (clip != null) return clip;
                }
            }
            return null;
        }

        static void EnsureUiVideo(RectTransform parent, VideoClip clip)
        {
            if (parent == null || clip == null) return;
            Transform existing = parent.Find("ArchitectVideoBg");
            GameObject go;
            RawImage raw;
            if (existing != null)
            {
                go = existing.gameObject;
                raw = go.GetComponent<RawImage>();
            }
            else
            {
                go = new GameObject("ArchitectVideoBg", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(parent, false);
                go.transform.SetAsFirstSibling();
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                raw = go.GetComponent<RawImage>();
            }

            go.SetActive(true);
            var vp = go.GetComponent<VideoPlayer>();
            if (vp == null) vp = go.AddComponent<VideoPlayer>();

            const int w = 1280, h = 720;
            var rtTex = raw.texture as RenderTexture;
            if (rtTex == null || rtTex.width != w || rtTex.height != h)
            {
                rtTex = new RenderTexture(w, h, 0)
                {
                    name = "ArchitectUiVideoRT_" + clip.name
                };
                raw.texture = rtTex;
            }

            vp.playOnAwake = false;
            vp.isLooping = true;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rtTex;
            vp.source = VideoSource.VideoClip;
            vp.clip = clip;
            vp.skipOnDrop = true;
            if (!vp.isPlaying) vp.Play();
        }
    }
}
