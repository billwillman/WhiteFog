using UnityEngine;
using UnityEditor;

namespace SoftKitty.MasterCombatCore
{
    public class CustomHierarchyMenu
    {
        [MenuItem("GameObject/Soft Kitty/Create Damage Object", false, 10)]
        static void CreateDamageObject(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("New Damage Object");
            CombatDamage _damage= go.AddComponent<CombatDamage>();
            Undo.RegisterCreatedObjectUndo(go, "Create Damage Object");
            Selection.activeObject = go;  
        }

        [MenuItem("GameObject/Soft Kitty/Create Damage Object", true)]
        static bool ValidateCreateDamageObject()
        {
            return Selection.activeGameObject == null;
        }


        [MenuItem("GameObject/Soft Kitty/Create Damage Object as Child", false, 10)]
        static void CreateDamageObjectAsChild(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("New Damage Object");
            go.transform.SetParent(Selection.activeGameObject.transform);
            CombatDamage _damage = go.AddComponent<CombatDamage>();
            Undo.RegisterCreatedObjectUndo(go, "Create Damage Object as Child");
            Selection.activeObject = go;
        }

        [MenuItem("GameObject/Soft Kitty/Create Damage Object as Child", true)]
        static bool ValidateCreateDamageObjectAsChild()
        {
            return Selection.activeGameObject != null;
        }
    }
}
