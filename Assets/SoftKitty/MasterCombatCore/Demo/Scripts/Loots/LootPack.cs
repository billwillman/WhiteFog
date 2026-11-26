using UnityEngine;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class LootPack : MonoBehaviour
    {
        public enum LootTypes
        {
            Attributes,
            TempAttributes,
            CustomData
        }
        public LootTypes LootType;
        public string Uid;
        public int Value;
        public Color VfxColor;
        public string Information;
        public string LootedText;
        public ParticleSystem [] Particles;
        private float life = 30F;
        private bool looted = false;
        internal CombatRoll? DropKey;

        void Awake()
        {
            life = 30F;
            foreach (var obj in Particles)
            {
                obj.startColor = VfxColor;
            }
        }

        public void Loot()
        {
            looted = true;
            foreach (var obj in Particles) obj.Stop();
            Destroy(gameObject, 2F);
            DemoUi.instance.ShowLootedMsg(LootedText);
            switch (LootType) {
                case LootTypes.Attributes:
                    GameManager.GetPlayer().GetAttributeData(Uid).AddValue(Value);
                    break;
                case LootTypes.TempAttributes:
                    var key = DropKey ?? CombatRandom.Event("lootPickup", GameManager.GetPlayer().CombatRandomId, Uid, null);
                    GameManager.GetPlayer().AddTempAttributes(Uid, Value, 20F, BasicMathFunction.Add, "LootPack" + key.Key.ToString("X16"));
                    GameManager.GetPlayerInstance().GetComponent<PlayerControl>().AddBuff(VfxColor);
                    break;
                case LootTypes.CustomData:
                    GameManager.GetPlayer().SetCustomInt(Uid, Value);
                    
                    break;
            }
        }

        void Update()
        {
            
            if (GameManager.GetPlayer() == null || looted) return;
            if (Vector3.Distance(GameManager.GetPlayer().Position, transform.position) < 2F)
            {
                DemoUi.instance.ShowLootInfo(Information);
                if (InputProxy.GetKeyDown(KeyCode.E))
                {
                    Loot();
                }
            }
            else
            {
                life = Mathf.MoveTowards(life,0F,Time.deltaTime);
                if (life <= 0F)
                {
                    foreach(var obj in Particles) obj.Stop();
                    Destroy(gameObject, 2F);
                }
            }
        }
    }
}
