using UnityEngine;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class EnemyControl : NpcControl
    {
        public float Exp = 100F;

        public override void OnDeath(Entity _attacker)
        {
            GameManager.PlaySound3D(DeathSound, 1F, transform.position + Vector3.up);
            if (_attacker != null)
            {
                _attacker.AddAttributeValue("xp", Exp);
            }
            var loot = CombatRandom.Event("deathLoot", EntityInstance.mData.CombatRandomId, null, _attacker?.CombatRandomId);
            if (loot.With("chance").Index(55) - 5 < EntityInstance.GetAttributeInt("lvl") + GameManager.GetPlayer().GetAttributeInt("luck"))
                DemoManager.instance.DropRandomLoot(transform.position, loot);
        }
        public override void Update()
        {
            base.Update();
        }

        
    }
}
