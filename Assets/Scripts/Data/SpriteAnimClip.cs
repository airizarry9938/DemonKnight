using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Multi-frame clip for modular paper-doll parts (Pose / Outfit / Head / Weapon / EnemyBoss).
    /// Standard clip names: "Idle", "Attack", "Hit", "Death".
    /// </summary>
    [Serializable]
    public class SpriteAnimClip
    {
        public const string Idle = "Idle";
        public const string Attack = "Attack";
        public const string Hit = "Hit";
        public const string Death = "Death";

        public string clipName = Idle;
        public float framesPerSecond = 6f;
        public bool loop = true;
        public List<Sprite> frames = new List<Sprite>();

        public int FrameCount => frames != null ? frames.Count : 0;

        public Sprite FrameAt(int index)
        {
            if (frames == null || frames.Count == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Count - 1)];
        }
    }
}
