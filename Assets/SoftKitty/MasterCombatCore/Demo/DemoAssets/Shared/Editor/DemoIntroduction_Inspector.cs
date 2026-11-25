using UnityEngine;
using UnityEditor;

namespace SoftKitty.MasterCombatCore.Demo
{
    [CustomEditor(typeof(DemoIntroduction))]
    public class DemoIntroduction_Inspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            EditorUtils.TitleLogo(EditorUtils.GetTexture("CombatLogo"), 0);
            EditorUtils.HelpInfo("To better walk you through how this demo is built, it is recommended to open a second Inspector window, select this GameObject, and lock it by clicking the lock icon in the top-right corner of the Inspector.", 0);
            EditorGUILayout.Separator();

            EditorUtils.Document();

            EditorUtils.BoxInfo(" Useful Links");
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Demo Manager"))
            {
                Selection.activeObject = myTarget.DemoManager;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            if (GUILayout.Button("HealhBar Manager"))
            {
                Selection.activeObject = myTarget.HealthBarRoot;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            if (GUILayout.Button("Entity Manager"))
            {
                SettingsService.OpenProjectSettings("Project/SoftKitty/Entity Manager");
            }
            GUILayout.EndHorizontal();
            GUI.backgroundColor = EditorUtils._titleColor;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Player"))
            {
                Selection.activeObject = myTarget.Player;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            if (GUILayout.Button("Enemy"))
            {
                Selection.activeObject = myTarget.DemoManager.EnemyPrefab;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            if (GUILayout.Button("Loot Pack"))
            {
                Selection.activeObject = myTarget.LootPack;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            GUILayout.EndHorizontal();
            GUI.backgroundColor = EditorUtils._gray;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Character Script"))
            {
                Selection.activeObject = myTarget.CharacterControlScript;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            if (GUILayout.Button("Damage Script"))
            {
                Selection.activeObject = myTarget.CombatDamageComponent;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            if (GUILayout.Button("Demo Script"))
            {
                Selection.activeObject = myTarget.DemoManagerScript;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            GUILayout.EndHorizontal();
            EditorUtils.ResetColor();


            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How are enemies spawned?"), ref myTarget.Questions[0], 0,false);
            if (myTarget.Questions[0])
            {
                StartAnswer();

                StartLine();
                Label("First, I have 8 different enemy Entity data assets stored in the ");
                EntityManager();
                EndLine();
                StartLine();
                Label("Then, I collect all of their UIDs into a list inside the ");
                DemoManager();
                EndLine();
                StartLine();
                Label("I instantiate the ");
                EnemyPrefab();
                Label(" and randomly assign one UID from that list.");
                EndLine();
                StartLine();
                Label("You can check the logic in ");
                Code("void SpawnEnemy(string _uid)");
                Label(" inside ");
                DemoManager();
                EndLine();

                EndAnswer();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How do you manage difficulty and player leveling?"), ref myTarget.Questions[1], 0, false);

            if (myTarget.Questions[1])
            {
                StartAnswer();

                StartLine();
                Label("In ");
                AttributesData();
                Label(", there is a setting called '<color=#FFCC44>Upgrade Increment</color>'.");
                EndLine();
                LabelLine("This value defines how much each Attribute increases per '<color=#FFCC44>Upgrade Level</color>'.");
                LabelLine("You can apply the '<color=#FFCC44>Upgrade Level</color>' when querying the <<color=#55FF88>AttributeData</color>> class:");
                CodeLine("AttributeData.GetFloat(int _upgradeLevel)");
                StartLine();
                Label("Or you can directly assign '<color=#55FF88>AttributesUpgradeLevel</color>' on the Entity, as shown in ");
                DemoManagerScript();
                EndLine();


                EndAnswer();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How is damage synchronized with animations?"), ref myTarget.Questions[2], 0, false);

            if (myTarget.Questions[2])
            {
                StartAnswer();
                StartLine();
                Label("First, I set up animation events on animation clips and bind them to functions in ");
                CharacterControlScript();
                EndLine();
                CodeLine("void SwordDamageEnable(float _multiplier)");
                CodeLine("void ShieldDamageEnable(float _multiplier)");
                StartLine();
                Label("When these functions are triggered by animations, they call into ");
                CombatDamageComponent();
                EndLine();
                CodeLine("SwordDamageScript.SetEnable(0.1F);");
                LabelLine("This enables damage detection for 0.1 seconds.");
                StartLine();
                CombatDamageComponent();
                Label(" then detects hit targets and applies damage.");
                EndLine();
                StartLine();
                Label("After 0.1 seconds, ");
                CombatDamageComponent();
                Label(" automatically stops damage detection.");
                EndLine();
                LabelLine("Note: The '<color=#FFCC44>Ignore same target within</color>' option is set to 0.2 seconds.");
                LabelLine("This ensures the same entity cannot be damaged multiple times by a single attack.");
                EndAnswer();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How do you implement effects like life steal, damage reflection, energy shield, or instant kill?"), ref myTarget.Questions[3], 0, false);
            if (myTarget.Questions[3])
            {
                StartAnswer();
                StartLine();
                Label("All of these mechanics are defined inside the ");
                GraphObject();
                Label(" assigned to the ");
                CombatDamageComponent();
                EndLine();
                StartLine();
                Label("These abilities are driven by Attributes, and ");
                LootPack();
                Label(" provides bonuses to those Attributes.");
                EndLine();
                EndAnswer(); 
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How do you implement on-hit buffs and Damage-over-Time (DoT)?"), ref myTarget.Questions[4], 0, false);
            if (myTarget.Questions[4])
            {
                StartAnswer();
                StartLine();
                Label("I use an <color=#FFCC44>'OverTimeEffect'</color> Action Node inside the ");
                GraphObject();
                EndLine();
                StartLine();
                Label("which is assigned to the ");
                CombatDamageComponent();
                EndLine();
                LabelLine("It exposes two <color=#55FF88>Dynamic Variables</color>: <color=#FFCC44>TargetOTE</color> and <color=#FFCC44>DealerOTE</color>.");
                StartLine();
                Label("These variables reference the <color=#55FF88>OverTimeEffect</color> ID defined in ");
                OteData();
                EndLine();
                EditorGUILayout.Separator();
                StartLine();
                Label("I also define two <color=#55FF88>Custom Data</color> entries on the Entity: <color=#FFCC44>hit_dot</color> and <color=#FFCC44>hit_buff</color>, located in ");
                EntityManager();
                EndLine();
                EditorGUILayout.Separator();
                StartLine();
                Label("When the player picks up a ");
                LootPack();
                Label(", it sets these <color=#55FF88>Custom Data</color> values using:");
                EndLine();
                CodeLine("GameManager.GetPlayer().SetCustomInt(Uid, Value);");
                StartLine();
                Label("The ");
                CharacterControlScript();
                Label(" reads these values and assigns <color=#FFCC44>TargetOTE</color> and <color=#FFCC44>DealerOTE</color> accordingly.");
                EndLine();
                StartLine();
                CombatDamageComponent();
                Label(" then applies the corresponding <color=#FFCC44>hit_dot</color> and <color=#FFCC44>hit_buff</color> effects.");
                EndLine();
                EndAnswer();
            }
            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How do you implement shield blocking?"), ref myTarget.Questions[5], 0, false);
            if (myTarget.Questions[5])
            {
                StartAnswer();
                StartLine();
                Label("I define an Attribute called <color=#FFCC44>'Blocking State'</color> inside ");
                AttributesData();
                EndLine();
                StartLine();
                Label("When the character is blocking with a shield, ");
                CharacterControlScript();
                Label(" sets this value to 60.");
                EndLine();
                StartLine();
                Label("Inside the ");
                GraphObject();
                Label(", an '<color=#55FF88>AngleDifference</color>' node compares this Attribute value with");
                EndLine();
                LabelLine("the angle between the character's forward direction and the vector toward the attacker.");
                LabelLine("If the angle is smaller than the Attribute value, a block event is triggered.");
                EndAnswer();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How do you save and load game progress?"), ref myTarget.Questions[6], 0, false);
            if (myTarget.Questions[6])
            {
                StartAnswer();
                StartLine();
                Label("In ");
                DemoManager();
                Label(", when you quit the game, the current progress is saved unless the game is over.");
                EndLine();
                LabelLine("The save data is loaded in <color=#55FF88>Awake()</color> the next time the game starts.");
                EditorGUILayout.Separator();
                LabelLine("To save Entity data, I simply call:");
                CodeLine("GameManager.EntityManagerData.Save(string _path, string _extraInfo)");
                StartLine();
                Label("   - <color=#55FF88>_extraInfo</color> is used to store additional data in the same save file, such as the difficulty value from ");
                DemoManager();
                EndLine();
                LabelLine("To load Entity data, I call:");
                CodeLine("string _extrajson = GameManager.EntityManagerData.Load(string _path)");
                StartLine();
                Label("   - This function returns the extra data as a JSON string, which I again apply to the difficulty setting in ");
                DemoManager();
                EndLine();
                EditorGUILayout.Separator();
                LabelLine("Worth mentioning: this demo intentionally goes a bit overboard for demonstration purposes.");
                LabelLine("If you look closely, you may notice that character animations are also saved.");
                LabelLine("When the game is loaded, enemies will continue the animation they were playing previously.");
                StartLine();
                Label("I created two <color=#FFCC44>CustomData</color> entries in ");
                EntityManager();
                Label(": '<color=#55FF88>animation</color>' and '<color=#55FF88>ani_time</color>', which are saved together with each Entity.");
                EndLine();

                StartLine();
                Label("Inside ");
                CharacterControlScript();
                Label(", I synchronize these values with <color=#55FF88>AnimatorState.shortNameHash</color> and <color=#55FF88>normalizedTime</color>.");
                EndLine();

                StartLine();
                Label("When loading the game, I use ");
                Code("Animator.Play(_hash, 0, _time);");
                Label(" to resume the animation.");
                EndLine();

                EditorGUILayout.Separator();
                LabelLine("In summary:");
                LabelLine("Store all Entity-related data inside <color=#FFCC44>CustomData</color>, and it will be automatically saved and loaded with the Entity.");
                StartLine();
                Label("Store general game progress data using <color=#55FF88>_extraInfo</color> when calling Save/Load from ");
                EntityManager();
                EndLine();
                EndAnswer();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Q: How does the Health Bar UI connect to Entities?"), ref myTarget.Questions[7], 0, false);
            if (myTarget.Questions[7])
            {
                StartAnswer();
                StartLine();
                HealthBarRoot();
                Label(" acts as a pool manager for health bars, over-time-effect (OTE) prefabs, and damage number objects.");
                EndLine();
                StartLine();
                CharacterControlScript();
                Label(" calls it in <color=#55FF88>Start()</color> to create a health bar by passing its Entity instance:");
                EndLine();
                CodeLine("HealthBarManager.CreateBar(EntityInstance);");
                EditorGUILayout.Separator();
                StartLine();
                HealthBarObj();
                Label(" represents the health bar instance for each Entity and registers two callbacks with the Entity:");
                EndLine();
                StartLine();
                Code("OverTimeEffectChangeEvent");
                Label(" for over-time effect updates.");
                EndLine();
                StartLine();
                Code("EntityEvent");
                Label(" for receiving damage-related events from the ");
                GraphObject();
                EndLine();
                EditorGUILayout.Separator();

                StartLine();
                Label("When receiving <color=#55FF88>OverTimeEffectChangeEvent</color>, ");
                HealthBarObj();
                Label(" requests a new ");
                OteObj();
                EndLine();
                StartLine();
                Label("from ");
                HealthBarRoot();
                EndLine();

                EditorGUILayout.Separator();

                StartLine();
                Label("When receiving an <color=#55FF88>EntityEvent</color> of type 'Damage', ");
                HealthBarObj();
                Label(" requests a new ");
                DamageObj();
                EndLine();
                StartLine();
                Label("from ");
                HealthBarRoot();
                EndLine();

                EndAnswer();
            }

        }

        private void LabelLine(string _text)
        {
            StartLine();
            Label(_text);
            EndLine();
        }

        private void CodeLine(string _text)
        {
            StartLine();
            Code(_text);
            EndLine();
        }


        private void AttributesData()
        {
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("Attributes Setting", GUILayout.Height(15)))
            {
                SettingsService.OpenProjectSettings("Project/SoftKitty/SubData - Attributes");
            }
            EditorUtils.ResetColor();
        }

        private void OteData()
        {
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("OverTimeEffect Setting", GUILayout.Height(15)))
            {
                SettingsService.OpenProjectSettings("Project/SoftKitty/SubData - Over Time Effect");
            }
            EditorUtils.ResetColor();
        }

        private void EnemyPrefab()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Enemy Prefab", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.DemoManager.EnemyPrefab;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }

        private void HealthBarRoot()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("HealthBarManager", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.HealthBarRoot;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }

        private void HealthBarObj()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("HealthBar Prefab", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.HealthBarObject;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }

        private void OteObj()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("OverTimeEffect Prefab", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.OteObject;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }

        private void DamageObj()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("Damage Number Prefab", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.DamageObject;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }

        private void LootPack()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Loot Pack", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.LootPack;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }

        private void DemoManager()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Demo Manager", GUILayout.Height(15)))
            {
                Selection.activeGameObject = myTarget.DemoManager.gameObject;
                EditorGUIUtility.PingObject(Selection.activeGameObject);
            }
            EditorUtils.ResetColor();
        }
        private void CombatDamageComponent()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._buttonColor;
            if (GUILayout.Button("<CombatDamage> component", GUILayout.Height(15)))
            {
                Selection.activeObject = myTarget.CombatDamageComponent;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            EditorUtils.ResetColor();
        }

        private void GraphObject()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("[Graph Object]", GUILayout.Height(15)))
            {
                Selection.activeObject = myTarget.GraphObject;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            EditorUtils.ResetColor();
        }
        private void DemoManagerScript()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("DemoManager.cs", GUILayout.Height(15)))
            {
                Selection.activeObject = myTarget.DemoManagerScript;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            EditorUtils.ResetColor();
        }
        private void CharacterControlScript()
        {
            DemoIntroduction myTarget = (DemoIntroduction)target;
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("CharacterControl.cs", GUILayout.Height(15)))
            {
                Selection.activeObject = myTarget.CharacterControlScript;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            EditorUtils.ResetColor();
        }
        private void EntityManager()
        {
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("Entity Manager", GUILayout.Height(15)))
            {
                SettingsService.OpenProjectSettings("Project/SoftKitty/Entity Manager");
            }
            EditorUtils.ResetColor();
        }

        private void Label(string _text)
        {
            _text = _text.Replace(" Entity ", "<color=#33ccFF> Entity </color>").
                Replace(" Attribute ", "<color=#33ccFF> Attribute </color>").
                Replace("Action Node", "<color=#FF4444>Action Node</color>");
            GUILayout.Label(_text, EditorUtils._richLabelStyle);
        }

        private void Code(string _text)
        {
            GUI.backgroundColor = Color.gray * 0.6F;
            GUILayout.Box("C# [", EditorUtils._smallBoxStyle, GUILayout.Width(35), GUILayout.Height(16));
            GUILayout.Label("<color=#55FF88>"+_text+"</color>", EditorUtils._richLabelStyle);
            GUILayout.Box("]", EditorUtils._smallBoxStyle, GUILayout.Width(20), GUILayout.Height(16));
            GUI.backgroundColor = Color.white;
        }

        private void StartAnswer()
        {
            GUILayout.BeginVertical(EditorUtils._groupStyle);
            EditorGUILayout.Separator();
        }

        private void EndAnswer()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            GUILayout.EndVertical();
            EditorGUILayout.Separator();
        }

        private void StartLine()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
        }

        private void EndLine()
        {
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

       
    }
}
