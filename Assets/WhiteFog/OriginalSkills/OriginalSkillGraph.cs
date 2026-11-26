using UnityEngine;

namespace SoftKitty
{
    public sealed class OriginalSkillGraph : GraphObject
    {
        public int sourceId;
        public string sourceVersion;
        public AnimationClip castMotion;
        public GameObject castEffect, weaponEffect, projectile, tail, impact;
        public string weaponBone;
        public float weaponSeconds, projectileSpeed, projectileAcceleration, tailSeconds;
        public Vector3 launchOffset, impactOffset;
    }
}
