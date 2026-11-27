using UnityEngine;

namespace SoftKitty
{
    [CreateAssetMenu(fileName = "投射物 Snap", menuName = "Soft Kitty/投射物 Snap")]
    public sealed class SkillProjectileSnap : ScriptableObject
    {
        public SkillProjectile settings = new SkillProjectile();
    }
}
