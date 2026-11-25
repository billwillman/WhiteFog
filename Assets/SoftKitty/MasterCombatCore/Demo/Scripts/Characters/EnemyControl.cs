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
            if(Random.Range(-5,50)< EntityInstance.GetAttributeInt("lvl")+GameManager.GetPlayer().GetAttributeInt("luck"))DemoManager.instance.DropRandomLoot(transform.position);
        }
        public override void Update()
        {
            base.Update();
        }

        
    }
}
