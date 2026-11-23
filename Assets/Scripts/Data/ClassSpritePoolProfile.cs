using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    // tip: fix-gdt-profile-always-compile — named MonoScript for Unity SO binding
    /// <summary>
    /// Architect class sprite pools (Peasant / Warrior / Knight). Resources path:
    /// ClassSpritePoolProfile. StageLayoutProfile pew slots stay position/scale guides —
    /// church/combat visuals pick from these pools by unit class.
    /// </summary>
    [CreateAssetMenu(fileName = "ClassSpritePoolProfile", menuName = "DemonKnight/Class Sprite Pool Profile")]
    public class ClassSpritePoolProfile : ScriptableObject
    {
        public const string ResourcesPath = "ClassSpritePoolProfile";

        public List<Sprite> peasantSprites = new List<Sprite>();
        public List<Sprite> warriorSprites = new List<Sprite>();
        public List<Sprite> knightSprites = new List<Sprite>();

        /// <summary>Editor path backups so list entries survive reimports.</summary>
        public List<string> peasantAssetPaths = new List<string>();
        public List<string> warriorAssetPaths = new List<string>();
        public List<string> knightAssetPaths = new List<string>();

        /// <summary>True when this is a CreateInstance stand-in (not a Resources asset).</summary>
        [System.NonSerialized] public bool isRuntimeStandIn;

        public static ClassSpritePoolProfile Load()
        {
            ClassSpritePoolProfile p = null;
            try { p = Resources.Load<ClassSpritePoolProfile>(ResourcesPath); }
            catch { /* missing MonoScript GUID/fileID after embed — fall through */ }
            if (p != null)
            {
                p.EnsureLists();
                p.isRuntimeStandIn = false;
                // Paths filled + Sprite Missing after Sync → rematerialize before Applier picks.
                p.RematerializeSpritesFromPaths();
                return p;
            }
            try
            {
                var all = Resources.LoadAll<ClassSpritePoolProfile>("");
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] == null) continue;
                        all[i].EnsureLists();
                        all[i].isRuntimeStandIn = false;
                        all[i].RematerializeSpritesFromPaths();
                        return all[i];
                    }
                }
            }
            catch { /* broken Resources asset / missing script — soft stand-in */ }
            p = CreateInstance<ClassSpritePoolProfile>();
            p.name = "ClassSpritePoolProfile_Runtime";
            p.isRuntimeStandIn = true;
            p.EnsureLists();
            return p;
        }

        public void EnsureLists()
        {
            peasantSprites ??= new List<Sprite>();
            warriorSprites ??= new List<Sprite>();
            knightSprites ??= new List<Sprite>();
            peasantAssetPaths ??= new List<string>();
            warriorAssetPaths ??= new List<string>();
            knightAssetPaths ??= new List<string>();
        }

        /// <summary>
        /// Restore Missing sprite refs from path backups (editor AssetDatabase).
        /// Never wipe path backups when sprites are temporarily null — Persist syncs
        /// paths only when a live sprite is present.
        /// </summary>
        public int RematerializeSpritesFromPaths()
        {
            EnsureLists();
            int n = 0;
            n += RematerializeList(peasantSprites, peasantAssetPaths);
            n += RematerializeList(warriorSprites, warriorAssetPaths);
            n += RematerializeList(knightSprites, knightAssetPaths);
            return n;
        }

        public static int RematerializeList(List<Sprite> sprites, List<string> paths)
        {
            if (sprites == null || paths == null) return 0;
            int restored = 0;
            int count = Mathf.Max(sprites.Count, paths.Count);
            while (sprites.Count < count) sprites.Add(null);
            while (paths.Count < count) paths.Add("");
            for (int i = 0; i < count; i++)
            {
                if (sprites[i] != null) continue;
                if (string.IsNullOrEmpty(paths[i])) continue;
                Sprite sp = TryLoadSpriteAtPath(paths[i]);
                if (sp == null) continue;
                sprites[i] = sp;
                restored++;
            }
            return restored;
        }

        public static Sprite TryLoadSpriteAtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string norm = path.Replace("\\", "/");
#if UNITY_EDITOR
            try
            {
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(norm);
                if (sp != null) return sp;
                // Texture with multiple sprites — take first sub-asset Sprite.
                var objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(norm);
                if (objs != null)
                {
                    for (int i = 0; i < objs.Length; i++)
                        if (objs[i] is Sprite s) return s;
                }
            }
            catch { /* ignore */ }
#endif
            // Player builds: Resources.Load (profiles + promoted ArchitectArt copies).
            try
            {
                int idx = norm.IndexOf("Resources/", System.StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string res = norm.Substring(idx + "Resources/".Length);
                    int dot = res.LastIndexOf('.');
                    if (dot > 0) res = res.Substring(0, dot);
                    var fromRes = Resources.Load<Sprite>(res);
                    if (fromRes != null) return fromRes;
                    var all = Resources.LoadAll<Sprite>(res);
                    if (all != null && all.Length > 0 && all[0] != null) return all[0];
                }
                // Bare Resources key (no Assets/ prefix) — ArchitectArt/Body/foo
                if (!norm.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                    && norm.IndexOf(':') < 0)
                {
                    string key = norm;
                    int dot = key.LastIndexOf('.');
                    if (dot > 0) key = key.Substring(0, dot);
                    var bare = Resources.Load<Sprite>(key);
                    if (bare != null) return bare;
                }
            }
            catch { /* ignore */ }

            // StreamingAssets fallback (png/jpg copied by build prepare). Soft-fail — no wipe.
            try
            {
                string fileName = System.IO.Path.GetFileName(norm);
                if (!string.IsNullOrEmpty(fileName))
                {
                    string sa = System.IO.Path.Combine(Application.streamingAssetsPath, "DemonKnightArchitect", fileName);
                    if (System.IO.File.Exists(sa))
                    {
                        byte[] bytes = System.IO.File.ReadAllBytes(sa);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(bytes))
                        {
                            tex.name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                            return Sprite.Create(
                                tex,
                                new Rect(0, 0, tex.width, tex.height),
                                new Vector2(0.5f, 0.5f),
                                100f);
                        }
                    }
                }
            }
            catch { /* ignore */ }
            return null;
        }

        public List<Sprite> GetPool(UnitType type)
        {
            EnsureLists();
            return type switch
            {
                UnitType.Peasant => peasantSprites,
                UnitType.Warrior => warriorSprites,
                UnitType.Knight => knightSprites,
                _ => null
            };
        }

        public int CountUsable(UnitType type)
        {
            var pool = GetPool(type);
            if (pool == null) return 0;
            EnsureLists();
            List<string> paths = type switch
            {
                UnitType.Peasant => peasantAssetPaths,
                UnitType.Warrior => warriorAssetPaths,
                UnitType.Knight => knightAssetPaths,
                _ => null
            };
            int n = 0;
            int count = pool.Count;
            if (paths != null && paths.Count > count) count = paths.Count;
            for (int i = 0; i < count; i++)
            {
                if (i < pool.Count && pool[i] != null) { n++; continue; }
                // Path backup counts as usable — Rematerialize restores the Missing Sprite ref.
                if (paths != null && i < paths.Count && !string.IsNullOrEmpty(paths[i])) n++;
            }
            return n;
        }
    }
}
