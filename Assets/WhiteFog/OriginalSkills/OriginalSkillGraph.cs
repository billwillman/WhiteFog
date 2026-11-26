using UnityEngine;

namespace SoftKitty
{
    public sealed class OriginalSkillGraph : GraphObject
    {
        [System.Serializable]
        public sealed class EffectCue
        {
            public GameObject prefab;
            public string bone;
            public bool onTarget, followBone, followRotation;
            public float duration, hitDelay;
        }

        public EffectCue[] effects = new EffectCue[0];
        public GameObject previewEnemy;
        public string projectileBone;
        public float impactScale = 1f;
        public int sourceId;
        public string sourceVersion;
        public AnimationClip castMotion;
        public GameObject castEffect, weaponEffect, projectile, tail, impact;
        public string weaponBone;
        public float weaponSeconds, projectileSpeed, projectileAcceleration, tailSeconds;
        public Vector3 launchOffset, impactOffset;
    }
}
