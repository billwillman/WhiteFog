using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore
{

    [InitializeOnLoad]
    public static class OverTimeEffectInspectorRegister
    {
        static OverTimeEffectInspectorRegister()
        {
            OverTimeEffectInspectorRegistry.Register(new CombatOverTimeInspectorModule());
        }
    }

    public class CombatOverTimeInspectorModule : IOverTimeEffectInspectorModule
    {
        public bool DrawInspector(OverTimeEffectObject myTarget, int i)
        {
            EditorUtils.PreBox(new GUIContent("Design Graph:", "The visual graph that defines the behavior and logic of this effect."), 10, EditorUtils._buttonColor, 130);

            myTarget.overTimeEffects[i].DesignGraph = (GraphObject)EditorGUILayout.ObjectField(myTarget.overTimeEffects[i].DesignGraph, typeof(GraphObject), false);
            GUI.backgroundColor = myTarget.overTimeEffects[i].DesignGraph != null ? EditorUtils._titleColor : Color.gray;
            if (GUILayout.Button("Design", GUILayout.Width(60)))
            {
                if (myTarget.overTimeEffects[i].DesignGraph != null) MasterGraphWindow.OpenWindow((GraphObject)myTarget.overTimeEffects[i].DesignGraph);
            }
            EditorUtils.ResetColor();
            EditorUtils.End(10);

            EditorUtils.Pre(new GUIContent("Variables: (" + myTarget.overTimeEffects[i].DynamicVariables.Count.ToString()
                + ")", "Variables passed to the design graph (e.g. damage, element type)."), 10, 130);
            GUI.backgroundColor = myTarget.overTimeEffects[i].DesignGraph != null ? EditorUtils._titleColor : Color.gray;
            if (GUILayout.Button(new GUIContent("Read From Design Graph", "Read variables from the assigned design graph."), GUILayout.Width(160)))
            {
                if (myTarget.overTimeEffects[i].DesignGraph != null)
                {
                    GraphObject _designGraph = (GraphObject)myTarget.overTimeEffects[i].DesignGraph;
                    Dictionary<string, float> _valueDic = new Dictionary<string, float>();
                    for (int y = 0; y < myTarget.overTimeEffects[i].DynamicVariables.Count; y++)
                    {
                        string _uid = myTarget.overTimeEffects[i].DynamicVariables[y].uid;
                        if (!_valueDic.ContainsKey(_uid)) _valueDic.Add(_uid, myTarget.overTimeEffects[i].DynamicVariables[y].value);
                    }
                    myTarget.overTimeEffects[i].DynamicVariables.Clear();
                    for (int x = 0; x < _designGraph.RootSet.GetDynamicFloatList().Count; x++)
                    {
                        string _uid = _designGraph.RootSet.GetDynamicFloatList()[x].uid;
                        myTarget.overTimeEffects[i].DynamicVariables.Add(new DynamicFloat(_uid,
                           _valueDic.ContainsKey(_uid) ? _valueDic[_uid] : _designGraph.RootSet.GetDynamicFloatList()[x].value));
                    }
                }
            }
            EditorUtils.ResetColor();
            EditorUtils.End(10);
            if (myTarget.overTimeEffects[i].DesignGraph != null)
            {
                GraphObject _designGraph = (GraphObject)myTarget.overTimeEffects[i].DesignGraph;
                for (int x = 0; x < myTarget.overTimeEffects[i].DynamicVariables.Count; x++)
                {
                    if (myTarget.overTimeEffects[i].DynamicVariables[x].uid != "Layer" && _designGraph.RootSet.GetDynamicFloat(myTarget.overTimeEffects[i].DynamicVariables[x].uid) != null)
                    {
                        EditorUtils.FloatField(ref myTarget.overTimeEffects[i].DynamicVariables[x].value, EditorUtils.TextHint(myTarget.overTimeEffects[i].DynamicVariables[x].uid + " :"),
                            30, true, _designGraph.RootSet.GetDynamicFloat(myTarget.overTimeEffects[i].DynamicVariables[x].uid).value, 110);
                    }
                }
                GUI.color = Color.gray * 1.3F;
                if (myTarget.overTimeEffects[i].layered && myTarget.overTimeEffects[i].maxLayer > 1)
                {
                    EditorUtils.Pre(EditorUtils.TextHint("Layer :"), 30, 110);
                    GUILayout.Label("(Auto set Variable)", GUILayout.Height(17));
                    EditorUtils.End(30);
                }
                EditorUtils.ResetColor();
            }
            return false;
        }
    }

}