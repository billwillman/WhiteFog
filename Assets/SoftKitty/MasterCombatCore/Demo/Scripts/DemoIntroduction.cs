using UnityEngine;


namespace SoftKitty.MasterCombatCore.Demo
{
    public class DemoIntroduction : MonoBehaviour
    {
        public DemoManager DemoManager;
        public GameObject Player;
        public GameObject HealthBarRoot;
        public GameObject HealthBarObject;
        public GameObject OteObject;
        public GameObject DamageObject;
        public bool [] Questions = new bool[30];
        public Object DemoManagerScript;
        public Object CharacterControlScript;
        public Object CombatDamageComponent;
        public Object GraphObject;
        public GameObject LootPack;
    }
}
