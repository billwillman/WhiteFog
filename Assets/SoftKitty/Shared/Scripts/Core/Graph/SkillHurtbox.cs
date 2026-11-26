using UnityEngine;
using SoftKitty.MasterCombatCore;

namespace SoftKitty
{
    [DisallowMultipleComponent]
    [AddComponentMenu("战斗/受击体")]
    public sealed class SkillHurtbox : DamageReciever
    {
        private void Reset()
        {
            if (!GetComponent<Collider>()) gameObject.AddComponent<BoxCollider>().isTrigger = true;
        }
    }
}
