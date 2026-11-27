using UnityEngine;

namespace SoftKitty
{
    [CreateAssetMenu(fileName = "投射物配置", menuName = "Soft Kitty/投射物配置")]
    public sealed class SkillProjectileSnap : ScriptableObject
    {
        public SkillProjectile settings = new SkillProjectile();
    }
}
