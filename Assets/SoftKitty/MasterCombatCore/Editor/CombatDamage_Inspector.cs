using UnityEngine;
using UnityEditor;
using System.Collections.Generic;


namespace SoftKitty
{
    [CustomEditor(typeof(CombatDamage))]
    public class CombatDamage_Inspector : Editor
    {

        int _varCount = 0;
        public override void OnInspectorGUI()
        {
           
            CombatDamage myTarget = (CombatDamage)target;
            EditorUtils.TitleLogo(EditorUtils.GetTexture("CombatLogo"),0);
           
            GUILayout.BeginHorizontal();
            myTarget.Enable = GUILayout.Toggle(myTarget.Enable, new GUIContent("Enable Damage", "Enable or disable damage detection."));
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Ignore Same Target Within:", "After damaging a target, the same target will be ignored for this duration (in seconds)."), GUILayout.Width(150));
            myTarget.DamageCoolDown = EditorGUILayout.Slider(myTarget.DamageCoolDown,0F,1F);
            GUILayout.Label("Sec", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            EditorUtils.Pre(EditorUtils.TextHint("Graph:", "Visual graph object that defines the damage calculation logic."), 0,50);
            GUI.backgroundColor = EditorUtils._titleColor;
            myTarget.mGraph = (GraphObject)EditorGUILayout.ObjectField(myTarget.mGraph, typeof(GraphObject), false, GUILayout.Width(200));
            if (myTarget.mGraph!=null) {
               
                if (GUILayout.Button("Design", GUILayout.Width(100)))
                {
                    MasterGraphWindow.OpenWindow(myTarget.mGraph);
                }
                EditorUtils.ResetColor();
            }
            EditorUtils.End(0);
            if (myTarget.mGraph==null) {
                EditorUtils.Warnning("You must assign a <color=#FFFF22>Graph Object</color> for this component to work.\n" +
"Right-click in the <color=#3388FF>Project</color> panel and select:\n" +
"<color=#3388FF>Create > SoftKitty > Graph Data</color>\n" +
"to create a new graph object.\n" +
"Double-click it to design your damage formula,\n" +
"then drag the graph object into the field above.\n" +
"You can also find preset graph objects at:\n" +
"<color=#3388FF>Assets/SoftKitty/MasterCombatCore/Presets/</color>", 20);
            }

            EditorGUILayout.Separator();

            if (myTarget.mGraph != null)
            {
                EditorUtils.Info(myTarget.mGraph.GetSummary, 20);
                EditorGUILayout.Separator();
            }

            if (myTarget.mGraph != null)
            {
                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.BeginVertical(EditorUtils._groupStyle);
                EditorUtils.Label(EditorUtils.TextHint("Dynamic Variables: (" + _varCount + ")", "Dynamic variables defined in the assigned graph object."), 0);
                _varCount = 0;
                for (int i = 0; i < myTarget.mGraph.RootSet.Variables.Count; i++)
                {
                    if (myTarget.mGraph.RootSet.Variables[i].Type == VariableType.DynamicVariable)
                    {
                        int _index = GetDynamicFloatIndex(myTarget, myTarget.mGraph.RootSet.Variables[i].VariableUid);
                        if (_index == -1)
                        {
                            myTarget.DynamicFloatList.Add(new DynamicFloat(myTarget.mGraph.RootSet.Variables[i].VariableUid, myTarget.mGraph.RootSet.Variables[i].Constant));
                            _index = myTarget.DynamicFloatList.Count - 1;
                        }
                        _varCount++;
                        EditorUtils.Pre(EditorUtils.TextHint("(uid:" + myTarget.mGraph.RootSet.Variables[i].VariableUid + ")  (default:" + myTarget.mGraph.RootSet.Variables[i].Constant + ")"), 20, 200);
                        myTarget.DynamicFloatList[_index].value = EditorGUILayout.FloatField(myTarget.DynamicFloatList[_index].value, GUILayout.Width(50));
                        GUI.backgroundColor = Color.gray;
                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Reset), "Reset the value to default."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            EditorGUI.FocusTextInControl(null);
                            myTarget.DynamicFloatList[_index].value = myTarget.mGraph.RootSet.Variables[i].Constant;
                        }
                        EditorUtils.ResetColor();
                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the uid of this dynamic variable."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            EditorGUI.FocusTextInControl(null);
                            GUIUtility.systemCopyBuffer = myTarget.mGraph.RootSet.Variables[i].VariableUid;
                            EditorUtils.ShowBoxNotification("uid of this dynamic variable copied!");
                        }
                        EditorUtils.End(20);
                        RemoveDuplicateDynamicFloat(myTarget, myTarget.mGraph.RootSet.Variables[i].VariableUid, _index);
                    }
                }
                if (_varCount == 0)
                {
                    EditorUtils.Label(EditorUtils.TextHint("None"), 20);
                    myTarget.DynamicFloatList.Clear();
                }
                else
                {
                    EditorUtils.HelpInfo("You can also set this value via code by calling:\n" +
"<color=#55FF77><CombatDamage>().SetDynamicValue(string _uid, float _value)</color>"
, 20);
                }
                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(20);
                GUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }
            EditorGUILayout.Separator();

            EditorUtils.TitleButton(EditorUtils.TextHint("Optional References"),ref myTarget.ui_optionalRef);
            if (myTarget.ui_optionalRef) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                if (!myTarget.PhysicsDetection)
                {
                    EditorUtils.Pre(EditorUtils.TextHint("Target Entity:", "The entity that receives the damage."), 10);
                    myTarget.TargetEntity = (EntityComponent)EditorGUILayout.ObjectField(myTarget.TargetEntity, typeof(EntityComponent), true, GUILayout.Width(150));
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button("Clear", GUILayout.Width(60)))
                    {
                        myTarget.TargetEntity = null;
                        UnityEditor.EditorUtility.SetDirty(myTarget);
                    }
                    EditorUtils.ResetColor();
                    EditorUtils.End(10);
                    EditorUtils.HelpInfo("You can leave this field empty and set it via code by calling:\n" +
"<color=#55FF77><CombatDamage>().SetTargetEntity(Entity _entity)</color>"
, 10);
                }
                EditorUtils.Pre(EditorUtils.TextHint("Dealer Entity:", "The entity that deals the damage."), 10);
                myTarget.DealerEntity = (EntityComponent)EditorGUILayout.ObjectField(myTarget.DealerEntity, typeof(EntityComponent), true, GUILayout.Width(150));
                GUI.backgroundColor =EditorUtils._black;
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    myTarget.DealerEntity = null;
                    UnityEditor.EditorUtility.SetDirty(myTarget);
                }
                EditorUtils.ResetColor();
                EditorUtils.End(10);
                EditorUtils.HelpInfo("You can leave this field empty and set it via code by calling:\n" +
"<color=#55FF77><CombatDamage>().SetDealerEntity(Entity _entity)</color>"
, 10);

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(23);
                GUILayout.EndHorizontal();
            }
           
           

            EditorUtils.TitleButton(EditorUtils.TextHint("Physics Detection"), ref myTarget.ui_physics);
            if (myTarget.ui_physics) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.Toggle(ref myTarget.is2D, EditorUtils.TextHint("2D Physics:", "Enable to use Physics2D for 2D games."), 10);

                EditorUtils.Toggle(ref myTarget.PhysicsDetection, EditorUtils.TextHint("Enable Physics Detection:", "Enable or disable physics-based damage detection. This is optional; damage can also be applied manually via DealDamage()."), 10);
                if (myTarget.PhysicsDetection) {
                    myTarget.TargetEntity = null;
                    EditorUtils.Pre(EditorUtils.TextHint("Detection Mode:", "Physics detection mode."), 10);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    myTarget.PhysicsType = (CombatDamage.PhysicsTypes)EditorGUILayout.EnumPopup(myTarget.PhysicsType, GUILayout.Width(150));
                    EditorUtils.ResetColor();
                    EditorUtils.End(10);

                    switch (myTarget.PhysicsType)
                    {
                        case CombatDamage.PhysicsTypes.Rigidbody_Collider:
                            EditorUtils.HelpInfo("When game objects:\n" +
"<color=#55FF77>with <EntityComponent>\n" +
"with <Rigidbody>\n" +
"with any collider on itself or its children (\"Is Trigger\" unchecked)\n" +
"with a matching tag in its Entity data\n" +
"within the LayerMask\n</color>" +
"collide with this object, damage detection will be triggered.\n" +
"Whether damage is applied depends on your Graph design."
,
                                30);
                            break;
                        case CombatDamage.PhysicsTypes.Trigger_Collider:
                            EditorUtils.HelpInfo("When game objects:\n" +
"<color=#55FF77>with <EntityComponent>\n" +
"with any collider on itself or its children\n" +
"with a matching tag in its Entity data\n" +
"within the LayerMask\n</color>" +
"collide with this object, damage detection will be triggered.\n" +
"Whether damage is applied depends on your Graph design."
,
                                30);
                            break;
                        case CombatDamage.PhysicsTypes.Raycast:
                            EditorUtils.HelpInfo("A ray will be cast from the specified position in the specified direction.\n" +
"When it hits any game objects:\n" +
"<color=#55FF77>with <EntityComponent>\n" +
"with any collider on itself or its children\n" +
"with a matching tag in its Entity data\n" +
"within the LayerMask\n</color>" +
"damage detection will be triggered and the ray will stop.\n" +
"Whether damage is applied depends on your Graph design."
,
                                30);
                            break;
                        case CombatDamage.PhysicsTypes.SphereCast:
                            EditorUtils.HelpInfo("A sphere will be cast from the specified position in the specified direction.\n" +
"When it hits any game objects:\n" +
"<color=#55FF77>with <EntityComponent>\n" +
"with any collider on itself or its children\n" +
"with a matching tag in its Entity data\n" +
"within the LayerMask\n</color>" +
"damage detection will be triggered and the sphere will stop.\n" +
"Whether damage is applied depends on your Graph design."
,
                               30);
                            break;
                        case CombatDamage.PhysicsTypes.Overlap_Sphere:
                            EditorUtils.HelpInfo("A sphere will be placed at the specified position.\n" +
"Any game objects overlapping with it:\n" +
"<color=#55FF77>with <EntityComponent>\n" +
"with any collider on itself or its children\n" +
"with a matching tag in its Entity data\n" +
"within the LayerMask\n</color>" +
"will trigger damage detection.\n" +
"Whether damage is applied depends on your Graph design."
,
                              30);
                            break;
                        case CombatDamage.PhysicsTypes.Overlap_Capsule:
                            EditorUtils.HelpInfo("A capsule will be placed using the specified position and direction.\n" +
"Any game objects overlapping with it:\n" +
"<color=#55FF77>with <EntityComponent>\n" +
"with any collider on itself or its children\n" +
"with a matching tag in its Entity data\n" +
"within the LayerMask\n</color>" +
"will trigger damage detection.\n" +
"Whether damage is applied depends on your Graph design."
,
                              30);
                            break;
                    }

                    if (myTarget.PhysicsType != CombatDamage.PhysicsTypes.Trigger_Collider && myTarget.PhysicsType != CombatDamage.PhysicsTypes.Rigidbody_Collider)
                    {
                        EditorUtils.Pre(EditorUtils.TextHint("Physics Detection Interval: Every ", "Number of frames between each physics detection check."), 10, 190);
                        myTarget.PhysicsInterval = EditorGUILayout.IntField(Mathf.Clamp(myTarget.PhysicsInterval,1,30), GUILayout.Width(20));
                        GUILayout.Label(" Frame");
                        EditorUtils.End(10);
                    }

                    EditorUtils.LayerMaskField(ref myTarget.PhysicsLayerMask, EditorUtils.TextHint("LayerMask:", "Layers included in the physics detection."), 10);
                    if (myTarget.PhysicsLayerMask.value == 0) {
                        EditorUtils.Warnning("Please select at least one layer in the LayerMask above.".ToString(), 30);
                    }

                    if (myTarget.PhysicsType != CombatDamage.PhysicsTypes.Rigidbody_Collider) {
                        EditorUtils.Toggle(ref myTarget.IgnoreTriggerColliders, EditorUtils.TextHint("Ignore isTrigger Colliders:", "Whether colliders marked as ¡°Is Trigger¡± are included in detection."),10);
                    }

                    EditorUtils.Pre(EditorUtils.TextHint("Tag Filter: (" + myTarget.TagFilter.Count + ")", "Tag filter for target entities. A target must have at least one of the specified tags."), 10);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    if (GUILayout.Button("Add", GUILayout.Width(50)))
                    {
                        myTarget.TagFilter.Add("Input Tag Here");
                        UnityEditor.EditorUtility.SetDirty(myTarget);
                    }
                    GUI.backgroundColor = Color.white;
                    EditorUtils.End(10);
                    bool _avaliableTag = false;
                    for (int i = 0; i < myTarget.TagFilter.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        myTarget.TagFilter[i] = GUILayout.TextField(myTarget.TagFilter[i], GUILayout.Width(100));
                        if (myTarget.TagFilter[i] != "Input Tag Here") _avaliableTag = true;
                        GUI.backgroundColor = EditorUtils._red;
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            myTarget.TagFilter.RemoveAt(i);
                            UnityEditor.EditorUtility.SetDirty(myTarget);
                        }
                        EditorUtils.ResetColor();
                        EditorUtils.End(30);
                    }
                    if (!_avaliableTag)
                    {
                        EditorUtils.Warnning("Please add at least one valid tag.", 30);
                    }
                    if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Rigidbody_Collider)
                    {


                        if (myTarget.is2D)
                        {
                            myTarget.mColliders.Clear();
                            EditorUtils.Pre(EditorUtils.TextHint("2D Colliders: (" + myTarget.mColliders2D.Count + ")", "Add 2d colliders used for damage detection to this list."), 10);
                            GUI.backgroundColor = EditorUtils._titleColor;
                            if (GUILayout.Button("Add", GUILayout.Width(50)))
                            {
                                myTarget.mColliders2D.Add(null);
                                UnityEditor.EditorUtility.SetDirty(myTarget);
                            }
                            if (GUILayout.Button("Auto Search", GUILayout.Width(100)))
                            {
                                foreach (var obj in myTarget.GetComponentsInChildren<Collider2D>())
                                {
                                    if (!myTarget.mColliders2D.Contains(obj)) myTarget.mColliders2D.Add(obj);
                                }
                                UnityEditor.EditorUtility.SetDirty(myTarget);
                            }
                            GUI.backgroundColor = Color.white;
                            EditorUtils.End(10);

                            for (int i = 0; i < myTarget.mColliders2D.Count; i++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                myTarget.mColliders2D[i] = (Collider2D)EditorGUILayout.ObjectField(myTarget.mColliders2D[i], typeof(Collider2D), true, GUILayout.Width(150));
                                GUI.backgroundColor = EditorUtils._red;
                                if (GUILayout.Button("X", GUILayout.Width(20)))
                                {
                                    myTarget.mColliders2D.RemoveAt(i);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                EditorUtils.End(30);

                                if (myTarget.mColliders2D[i] == null)
                                {
                                    EditorUtils.Warnning("2D Collider cannot be null. Please assign a 2D collider or remove this entry.", 30);
                                }
                                else if (myTarget.mColliders2D[i].isTrigger && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Rigidbody_Collider)
                                {
                                    EditorUtils.Warnning("The 2D collider must have ¡°Is Trigger¡± unchecked for [" + myTarget.PhysicsType + "].", 30);
                                    GUI.backgroundColor = EditorUtils._red;
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(30);
                                    if (GUILayout.Button("Auto Fix"))
                                    {
                                        myTarget.mColliders2D[i].isTrigger = false;
                                        UnityEditor.EditorUtility.SetDirty(myTarget.mColliders2D[i]);
                                        UnityEditor.EditorUtility.SetDirty(myTarget);
                                    }
                                    EditorUtils.ResetColor();
                                    GUILayout.Space(30);
                                    GUILayout.EndHorizontal();
                                }
                                else if (!myTarget.mColliders2D[i].isTrigger && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider)
                                {
                                    EditorUtils.Warnning("The 2D collider must have ¡°Is Trigger¡± checked for [" + myTarget.PhysicsType + "].", 30);
                                    GUI.backgroundColor = EditorUtils._red;
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(30);
                                    if (GUILayout.Button("Auto Fix"))
                                    {
                                        myTarget.mColliders2D[i].isTrigger = true;
                                        UnityEditor.EditorUtility.SetDirty(myTarget.mColliders2D[i]);
                                        UnityEditor.EditorUtility.SetDirty(myTarget);
                                    }
                                    EditorUtils.ResetColor();
                                    GUILayout.Space(30);
                                    GUILayout.EndHorizontal();
                                }
                            }
                            if (myTarget.mColliders2D.Count == 0)
                            {
                                EditorUtils.Warnning("At least one 2D collider is required for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    Collider2D _newCol = null;
                                    if (myTarget.GetComponent<Collider2D>())
                                    {
                                        _newCol = myTarget.GetComponent<Collider2D>();
                                    }
                                    else if (myTarget.GetComponentInChildren<Collider2D>(false))
                                    {
                                        _newCol = myTarget.GetComponentInChildren<Collider2D>(false);
                                    }
                                    else
                                    {
                                        _newCol = myTarget.gameObject.AddComponent<BoxCollider2D>();
                                    }
                                    _newCol.isTrigger = (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider);
                                    UnityEditor.EditorUtility.SetDirty(_newCol);
                                    myTarget.mColliders2D.Add(_newCol);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        }
                        else
                        {
                            myTarget.mColliders2D.Clear();
                            EditorUtils.Pre(EditorUtils.TextHint("Colliders: (" + myTarget.mColliders.Count + ")", "Add colliders used for damage detection to this list."), 10);
                            GUI.backgroundColor = EditorUtils._titleColor;
                            if (GUILayout.Button("Add", GUILayout.Width(50)))
                            {
                                myTarget.mColliders.Add(null);
                                UnityEditor.EditorUtility.SetDirty(myTarget);
                            }
                            if (GUILayout.Button("Auto Search", GUILayout.Width(100)))
                            {
                                foreach (var obj in myTarget.GetComponentsInChildren<Collider>())
                                {
                                    if (!myTarget.mColliders.Contains(obj)) myTarget.mColliders.Add(obj);
                                }
                                UnityEditor.EditorUtility.SetDirty(myTarget);
                            }
                            GUI.backgroundColor = Color.white;
                            EditorUtils.End(10);

                            for (int i = 0; i < myTarget.mColliders.Count; i++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                myTarget.mColliders[i] = (Collider)EditorGUILayout.ObjectField(myTarget.mColliders[i], typeof(Collider), true, GUILayout.Width(150));
                                GUI.backgroundColor = EditorUtils._red;
                                if (GUILayout.Button("X", GUILayout.Width(20)))
                                {
                                    myTarget.mColliders.RemoveAt(i);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                EditorUtils.End(30);

                                if (myTarget.mColliders[i] == null)
                                {
                                    EditorUtils.Warnning("Collider cannot be null. Please assign a collider or remove this entry.", 30);
                                }
                                else if (myTarget.mColliders[i].isTrigger && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Rigidbody_Collider)
                                {
                                    EditorUtils.Warnning("The collider must have ¡°Is Trigger¡± unchecked for [" + myTarget.PhysicsType + "].", 30);
                                    GUI.backgroundColor = EditorUtils._red;
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(30);
                                    if (GUILayout.Button("Auto Fix"))
                                    {
                                        myTarget.mColliders[i].isTrigger = false;
                                        UnityEditor.EditorUtility.SetDirty(myTarget.mColliders[i]);
                                        UnityEditor.EditorUtility.SetDirty(myTarget);
                                    }
                                    EditorUtils.ResetColor();
                                    GUILayout.Space(30);
                                    GUILayout.EndHorizontal();
                                }
                                else if (!myTarget.mColliders[i].isTrigger && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider)
                                {
                                    EditorUtils.Warnning("The collider must have ¡°Is Trigger¡± checked for [" + myTarget.PhysicsType + "].", 30);
                                    GUI.backgroundColor = EditorUtils._red;
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(30);
                                    if (GUILayout.Button("Auto Fix"))
                                    {
                                        myTarget.mColliders[i].isTrigger = true;
                                        UnityEditor.EditorUtility.SetDirty(myTarget.mColliders[i]);
                                        UnityEditor.EditorUtility.SetDirty(myTarget);
                                    }
                                    EditorUtils.ResetColor();
                                    GUILayout.Space(30);
                                    GUILayout.EndHorizontal();
                                }
                            }
                            if (myTarget.mColliders.Count == 0)
                            {
                                EditorUtils.Warnning("At least one collider is required for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    Collider _newCol = null;
                                    if (myTarget.GetComponent<Collider>())
                                    {
                                        _newCol = myTarget.GetComponent<Collider>();
                                    }
                                    else if (myTarget.GetComponentInChildren<Collider>(false))
                                    {
                                        _newCol = myTarget.GetComponentInChildren<Collider>(false);
                                    }
                                    else
                                    {
                                        _newCol = myTarget.gameObject.AddComponent<BoxCollider>();
                                    }
                                    _newCol.isTrigger = (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider);
                                    UnityEditor.EditorUtility.SetDirty(_newCol);
                                    myTarget.mColliders.Add(_newCol);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        }

                        if (myTarget.is2D)
                        {
                            if (myTarget.mRigid != null) DestroyImmediate(myTarget.mRigid);
                            myTarget.mRigid = null;

                            EditorUtils.Pre(EditorUtils.TextHint("Rigidbody2D:", "A Rigidbody2D component is required for [" + myTarget.PhysicsType + "], and must be attached to the same GameObject as this script."), 10);
                            myTarget.mRigid2D = (Rigidbody2D)EditorGUILayout.ObjectField(myTarget.mRigid2D, typeof(Rigidbody2D), true, GUILayout.Width(150));
                            EditorUtils.End(10);
                            if (myTarget.mRigid2D == null)
                            {
                                EditorUtils.Warnning("A Rigidbody2D component is required for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    Rigidbody2D _newRigi = null;
                                    if (myTarget.GetComponent<Rigidbody2D>())
                                    {
                                        _newRigi = myTarget.GetComponent<Rigidbody2D>();
                                    }
                                    else
                                    {
                                        _newRigi = myTarget.gameObject.AddComponent<Rigidbody2D>();
                                    }
                                    _newRigi.bodyType = (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider? RigidbodyType2D.Kinematic: RigidbodyType2D.Dynamic);
                                    myTarget.mRigid2D = _newRigi;
                                    UnityEditor.EditorUtility.SetDirty(_newRigi);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                            else if (myTarget.mRigid2D.gameObject != myTarget.gameObject)
                            {
                                EditorUtils.Warnning("The Rigidbody2D must be on the same GameObject as this script.", 30);
                            }
                            else if (myTarget.mRigid2D.bodyType== RigidbodyType2D.Kinematic && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Rigidbody_Collider)
                            {
                                EditorUtils.Warnning("The 'Body Type' of the Rigidbody2D must not be 'Kinematic' for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid2D.bodyType = RigidbodyType2D.Dynamic;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid2D);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                            else if (myTarget.mRigid2D.bodyType != RigidbodyType2D.Kinematic && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider)
                            {
                                EditorUtils.Warnning("The 'Body Type' of the Rigidbody2D must be 'Kinematic' for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid2D.bodyType = RigidbodyType2D.Kinematic;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid2D);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }

                        }
                        else
                        {
                            if (myTarget.mRigid2D != null) DestroyImmediate(myTarget.mRigid2D);
                            myTarget.mRigid2D = null;

                            EditorUtils.Pre(EditorUtils.TextHint("Rigidbody:", "A Rigidbody component is required for [" + myTarget.PhysicsType + "], and must be attached to the same GameObject as this script."), 10);
                            myTarget.mRigid = (Rigidbody)EditorGUILayout.ObjectField(myTarget.mRigid, typeof(Rigidbody), true, GUILayout.Width(150));
                            EditorUtils.End(10);
                            if (myTarget.mRigid == null)
                            {
                                EditorUtils.Warnning("A Rigidbody component is required for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    Rigidbody _newRigi = null;
                                    if (myTarget.GetComponent<Rigidbody>())
                                    {
                                        _newRigi = myTarget.GetComponent<Rigidbody>();
                                    }
                                    else
                                    {
                                        _newRigi = myTarget.gameObject.AddComponent<Rigidbody>();
                                    }
                                    _newRigi.isKinematic = (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider);
                                    myTarget.mRigid = _newRigi;
                                    UnityEditor.EditorUtility.SetDirty(_newRigi);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                            else if (myTarget.mRigid.gameObject != myTarget.gameObject)
                            {
                                EditorUtils.Warnning("The Rigidbody must be on the same GameObject as this script.", 30);
                            }
                            else if (myTarget.mRigid.isKinematic && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Rigidbody_Collider)
                            {
                                EditorUtils.Warnning("The Rigidbody must have `Is Kinematic` unchecked for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid.isKinematic = false;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                            else if (!myTarget.mRigid.isKinematic && myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider)
                            {
                                EditorUtils.Warnning("The Rigidbody must have `Is Kinematic` checked for [" + myTarget.PhysicsType + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid.isKinematic = true;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        }
                    }

                    if (myTarget.is2D) {
                        EditorUtils.FloatField(ref myTarget.min2dDepth,EditorUtils.TextHint("Min Depth", "Only include objects with a Z coordinate (depth) greater than or equal to this value."),10,true,-1F,150, "m");
                        EditorUtils.FloatField(ref myTarget.max2dDepth, EditorUtils.TextHint("Max Depth", "Only include objects with a Z coordinate (depth) less than or equal to this value."), 10, true, 1F, 150, "m");
                        EditorGUILayout.Separator();
                    }

                    if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.SphereCast || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Overlap_Capsule || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Overlap_Sphere)
                    {
                        EditorUtils.FloatSlider(ref myTarget.Radius, 0.05F, 10F, EditorUtils.TextHint(myTarget.PhysicsType.ToString().Replace("_"," ") + " Radius"), "m", 10);
                    }
                    if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Raycast || myTarget.PhysicsType == CombatDamage.PhysicsTypes.SphereCast || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Overlap_Capsule)
                    {
                        EditorUtils.FloatSlider(ref myTarget.Distance, 0.1F, 100F, EditorUtils.TextHint(myTarget.PhysicsType.ToString().Replace("_", " ") + " Length"), "m", 10);
                    }

                    if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Raycast || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Overlap_Capsule || myTarget.PhysicsType == CombatDamage.PhysicsTypes.SphereCast)
                    {
                        EditorUtils.Pre(EditorUtils.TextHint("Direction:", "Local-space direction of this transform."),10,70);
                        GUI.backgroundColor =EditorUtils._black;
                        if (!myTarget.is2D) {
                            if (GUILayout.Button("Fwd", GUILayout.Width(40))) { myTarget.DirOffset = Vector3.forward; UnityEditor.EditorUtility.SetDirty(myTarget); }
                            if (GUILayout.Button("Back", GUILayout.Width(40))) { myTarget.DirOffset = Vector3.back; UnityEditor.EditorUtility.SetDirty(myTarget); }
                        }
                        if (GUILayout.Button("Left", GUILayout.Width(40))) { myTarget.DirOffset = Vector3.left; UnityEditor.EditorUtility.SetDirty(myTarget); }
                        if (GUILayout.Button("Right", GUILayout.Width(40))) { myTarget.DirOffset = Vector3.right; UnityEditor.EditorUtility.SetDirty(myTarget); }
                        if (GUILayout.Button("Up", GUILayout.Width(40))) { myTarget.DirOffset = Vector3.up; UnityEditor.EditorUtility.SetDirty(myTarget); }
                        if (GUILayout.Button("Down", GUILayout.Width(40))) { myTarget.DirOffset = Vector3.down; UnityEditor.EditorUtility.SetDirty(myTarget); }
                        GUI.backgroundColor = EditorUtils._titleColor;
                        EditorUtils.End(10);
                        EditorUtils.FloatSlider(ref myTarget.DirOffset.x, -1F, 1F, EditorUtils.TextHint("X:"), "", 30, 30);
                        EditorUtils.FloatSlider(ref myTarget.DirOffset.y, -1F,1F, EditorUtils.TextHint("Y:"), "", 30, 30);
                        if (!myTarget.is2D)
                        {
                            EditorUtils.FloatSlider(ref myTarget.DirOffset.z, -1F, 1F, EditorUtils.TextHint("Z:"), "", 30, 30);
                            if (myTarget.DirOffset.x == 0F && myTarget.DirOffset.y == 0F && myTarget.DirOffset.z == 0F) myTarget.DirOffset.z = 1F;
                        }
                        else
                        {
                            myTarget.DirOffset.z = 0F;
                            if (myTarget.DirOffset.x == 0F && myTarget.DirOffset.y == 0F && myTarget.DirOffset.z == 0F) myTarget.DirOffset.x = 1F;
                        }
                        GUI.backgroundColor = Color.white;
                    }

                    if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Raycast || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Overlap_Capsule || myTarget.PhysicsType == CombatDamage.PhysicsTypes.SphereCast || myTarget.PhysicsType == CombatDamage.PhysicsTypes.Overlap_Sphere)
                    {
                        EditorUtils.Pre(EditorUtils.TextHint("Position Offset:", "Local-space offset of the cast point."), 10,100);
                        GUI.backgroundColor =EditorUtils._black;
                        if (GUILayout.Button("Reset", GUILayout.Width(40))) { myTarget.PosOffset = Vector3.zero; UnityEditor.EditorUtility.SetDirty(myTarget); }
                        EditorUtils.End(10);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        EditorUtils.FloatSlider(ref myTarget.PosOffset.x, -3F, 3F, EditorUtils.TextHint("X:"), "m", 30,30);
                        EditorUtils.FloatSlider(ref myTarget.PosOffset.y, -3F, 3F, EditorUtils.TextHint("Y:"), "m", 30,30);
                        if (!myTarget.is2D)
                        {
                            EditorUtils.FloatSlider(ref myTarget.PosOffset.z, -50F, 50F, EditorUtils.TextHint("Z:"), "m", 30, 30);
                        }
                        else
                        {
                            myTarget.PosOffset.z = 0F;
                        }
                        GUI.backgroundColor = Color.white;
                      

                        EditorUtils.Toggle(ref myTarget.PhysicsPreview, EditorUtils.TextHint("Preview", "Preview physics detection gizmos in the Scene view."), 10);
                    }
                }
                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(23);
                GUILayout.EndHorizontal();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Projectile"), ref myTarget.ui_projectile);
            if (myTarget.ui_projectile)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.Pre(EditorUtils.TextHint("Projectile Mode:", "Projectile behavior mode."), 10);
                GUI.backgroundColor = myTarget.ProjectileMode == CombatDamage.ProjectileModes.Disable? EditorUtils._gray: EditorUtils._titleColor;
                myTarget.ProjectileMode = (CombatDamage.ProjectileModes)EditorGUILayout.EnumPopup(myTarget.ProjectileMode, GUILayout.Width(150));
                EditorUtils.ResetColor();
                EditorUtils.End(10);
                if (myTarget.ProjectileMode != CombatDamage.ProjectileModes.Disable && !myTarget.PhysicsDetection)
                {
                    EditorUtils.HelpInfo("It is highly recommended to enable '<color=#55FF77>Physics Detection</color>' for projectile, otherwise you will have to manually call <color=#55FF77>DealDamage()</color>.", 30);
                }

                switch (myTarget.ProjectileMode)
                {
                    case CombatDamage.ProjectileModes.Disable:
                        EditorUtils.HelpInfo("Projectile mode is currently disabled.",
                            30);
                        break;
                    case CombatDamage.ProjectileModes.ScriptDrive:
                        EditorUtils.HelpInfo("The transform will be driven forward by <color=#55FF77>Script</color>.\n" +
"This mode is more predictable but less realistic.\n" +
"It does not require a Rigidbody"+(myTarget.is2D?"2D":"")+" component\n" +
"and does NOT work with <color=#FFFF77>Rigidbody_Collider</color> detection mode."
,
                            30);
                        break;
                    case CombatDamage.ProjectileModes.PhysicsDrive:
                        EditorUtils.HelpInfo("The transform will be driven forward by <color=#55FF77>Physics</color>.\n" +
"This mode is more realistic but less predictable.\n" +
"It requires a <color=#55FF77>Rigidbody" + (myTarget.is2D ? "2D" : "") + "</color> component\n" +
"and does NOT work with <color=#FFFF77>Trigger_Collider</color> detection mode."
,
                            30);
                        break;
                }

                if (myTarget.ProjectileMode!= CombatDamage.ProjectileModes.Disable) {
                    if (myTarget.ProjectileMode == CombatDamage.ProjectileModes.PhysicsDrive)
                    {

                        if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Trigger_Collider)
                        {
                            EditorUtils.Warnning("Projectile [<color=#FF7777>Physics Drive</color>] mode does not work with the " +
"[<color=#FF7777>Trigger_Collider</color>] detection mode.\n" +
"Please change one of these settings."
, 30);
                        }
                        else if (!myTarget.is2D)
                        {
                            if (myTarget.mRigid == null)
                            {
                                EditorUtils.Warnning("The Rigidbody component is required for [" + myTarget.ProjectileMode + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    Rigidbody _newRigi = null;
                                    if (myTarget.GetComponent<Rigidbody>())
                                    {
                                        _newRigi = myTarget.GetComponent<Rigidbody>();
                                    }
                                    else
                                    {
                                        _newRigi = myTarget.gameObject.AddComponent<Rigidbody>();
                                    }
                                    _newRigi.isKinematic = false;
                                    myTarget.mRigid = _newRigi;
                                    UnityEditor.EditorUtility.SetDirty(_newRigi);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                            else if (myTarget.mRigid.gameObject != myTarget.gameObject)
                            {
                                EditorUtils.Warnning("The Rigiedbody must be on the same GameObject as this script.", 30);
                            }
                            else if (myTarget.mRigid.isKinematic)
                            {
                                EditorUtils.Warnning("The Rigiedbody must uncheck 'is Kinematic' for [" + myTarget.ProjectileMode + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid.isKinematic = false;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        }
                        else
                        {
                            if (myTarget.mRigid2D == null)
                            {
                                EditorUtils.Warnning("The Rigidbody2D component is required for [" + myTarget.ProjectileMode + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    Rigidbody2D _newRigi = null;
                                    if (myTarget.GetComponent<Rigidbody2D>())
                                    {
                                        _newRigi = myTarget.GetComponent<Rigidbody2D>();
                                    }
                                    else
                                    {
                                        _newRigi = myTarget.gameObject.AddComponent<Rigidbody2D>();
                                    }
                                    _newRigi.bodyType =  RigidbodyType2D.Dynamic;
                                    myTarget.mRigid2D = _newRigi;
                                    UnityEditor.EditorUtility.SetDirty(_newRigi);
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                            else if (myTarget.mRigid2D.gameObject != myTarget.gameObject)
                            {
                                EditorUtils.Warnning("The Rigidbody2D must be on the same GameObject as this script.", 30);
                            }
                            else if (myTarget.mRigid2D.bodyType== RigidbodyType2D.Kinematic)
                            {
                                EditorUtils.Warnning("The 'Body Type' of the Rigidbody2D must not be 'Kinematic' for [" + myTarget.ProjectileMode + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid2D.bodyType = RigidbodyType2D.Dynamic;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid2D);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        }

                        EditorUtils.Toggle(ref myTarget.ProjectileUseGravity, EditorUtils.TextHint("Enable Gravity:", "Enable or disable gravity for the Rigidbody."), 30, 170);
                    } 
                    else if (myTarget.ProjectileMode == CombatDamage.ProjectileModes.ScriptDrive) {
                        if (myTarget.PhysicsType == CombatDamage.PhysicsTypes.Rigidbody_Collider)
                        {
                            EditorUtils.Warnning("Projectile [<color=#FF7777>Script Drive</color>] mode does not work with the " +
"[<color=#FF7777>Rigidbody_Collider</color>] detection mode.\n" +
"Please change one of these settings."
, 30);
                        }
                        else if (myTarget.is2D)
                        {
                            if ((myTarget.mRigid2D != null && myTarget.mRigid2D.bodyType != RigidbodyType2D.Kinematic ))
                            {
                                EditorUtils.Warnning("The 'Body Type' of the Rigiedbody2D must be 'Kinematic' for [" + myTarget.ProjectileMode + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid2D.bodyType = RigidbodyType2D.Kinematic;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid2D);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        } else {
                            if ((myTarget.mRigid != null && !myTarget.mRigid.isKinematic))
                            {
                                EditorUtils.Warnning("The Rigiedbody must check 'is Kinematic' for [" + myTarget.ProjectileMode + "].", 30);
                                GUI.backgroundColor = EditorUtils._red;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                if (GUILayout.Button("Auto Fix"))
                                {
                                    myTarget.mRigid.isKinematic = true;
                                    UnityEditor.EditorUtility.SetDirty(myTarget);
                                    UnityEditor.EditorUtility.SetDirty(myTarget.mRigid);
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(30);
                                GUILayout.EndHorizontal();
                            }
                        }

                        EditorUtils.FloatSlider(ref myTarget.ProjectileGravity, 0F, 2F, EditorUtils.TextHint("Gravity Multiplier:", "Multiplier applied to gravity."), "x", 30,170);
                    }

                    EditorUtils.Toggle(ref myTarget.LaunchOnStart, EditorUtils.TextHint("Launch On Start/On Enable:", "Launch the projectile on start/on enable, if you toggle it off you will have to manually call <CombatDamage>().Launch() to launch the projectile."),30,170);

                    EditorUtils.FloatSlider(ref myTarget.ProjectileSpeed, 1F, 100F, EditorUtils.TextHint("Speed:", "Projectile speed in meters per second."), "m/s", 30, 170);

                    if (myTarget.ProjectileMode == CombatDamage.ProjectileModes.ScriptDrive)
                    {
                        EditorUtils.Pre(EditorUtils.TextHint("Speed Curve Over Lifetime:", "Controls projectile speed over its lifetime."), 30, 170);
                        myTarget.ProjectileSpeedCurve = EditorGUILayout.CurveField(myTarget.ProjectileSpeedCurve);
                        EditorUtils.End(20);
                    }

                    EditorUtils.FloatSlider(ref myTarget.ProjectileLife, 0.5F, 30F, EditorUtils.TextHint("Lifetime:", "Lifetime in seconds. The projectile is destroyed when time expires."), "sec", 30, 170);
                    EditorUtils.Pre(EditorUtils.TextHint("Exhausted Prefab (Optional):", "Prefab instantiated when the projectile lifetime expires."), 50,170);
                    GUI.backgroundColor = EditorUtils._black;
                    myTarget.ExhaustedPrefab = (GameObject)EditorGUILayout.ObjectField(myTarget.ExhaustedPrefab, typeof(GameObject),true );
                    EditorUtils.ResetColor();
                    EditorUtils.End(20);
                    EditorUtils.Toggle(ref myTarget.DestroyOnHitGround, EditorUtils.TextHint("Destroy when hit ground:", "Destroy the projectile when it hits the ground."), 30, 170);
                    if (myTarget.DestroyOnHitGround) {
                        EditorUtils.Pre(EditorUtils.TextHint("Hit Ground Prefab (Optional):", "Prefab instantiated when the projectile hits the ground."), 50, 170);
                        GUI.backgroundColor = EditorUtils._black;
                        myTarget.HitGroundEffectPrefab = (GameObject)EditorGUILayout.ObjectField(myTarget.HitGroundEffectPrefab, typeof(GameObject), true);
                        EditorUtils.ResetColor();
                        EditorUtils.End(20);
                    }
                    EditorUtils.Toggle(ref myTarget.DestroyOnHitTarget, EditorUtils.TextHint("Destroy when hit enemy:", "Destroy the projectile when it hits the enemy."), 30, 170);
                    if (myTarget.DestroyOnHitTarget)
                    {
                        EditorUtils.Pre(EditorUtils.TextHint("Hit Enemy Prefab (Optional):", "Prefab instantiated when the projectile hits the enemy."), 50, 170);
                        GUI.backgroundColor = EditorUtils._black;
                        myTarget.HitTargetEffectPrefab = (GameObject)EditorGUILayout.ObjectField(myTarget.HitTargetEffectPrefab, typeof(GameObject), true);
                        EditorUtils.ResetColor();
                        EditorUtils.End(20);
                    }
                    EditorUtils.Toggle(ref myTarget.AutoSeekingTarget, EditorUtils.TextHint("Auto-seeking Targets:", "Whether the projectile automatically seeks targets while in flight."), 30, 170);
                    if (myTarget.AutoSeekingTarget) {
                        EditorUtils.FloatSlider(ref myTarget.SeekingRadius, 1F, 100F, EditorUtils.TextHint("Seeking Targets Radius:", "Radius (in meters) within which the projectile can seek targets."), "m", 50, 170);
                        EditorUtils.FloatSlider(ref myTarget.SeekingAngularVelocity, 1F, 300F, EditorUtils.TextHint("Tracking Angular Velocity:", "Maximum degrees per second the projectile can rotate when tracking a target."), "deg", 50, 170);
                    }
                }

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(23);
                GUILayout.EndHorizontal();

            }

            EditorGUILayout.Separator();
            EditorUtils.Document("master-combat-core/damage-component/combat-damage");
            if(GUI.changed) UnityEditor.EditorUtility.SetDirty(myTarget);
        }

        private int GetDynamicFloatIndex(CombatDamage _script,string _uid) {
            for (int i=0;i< _script.DynamicFloatList.Count;i++) {
                if (_script.DynamicFloatList[i].uid == _uid) return i;
            }
            return -1;
        }

        private void RemoveDuplicateDynamicFloat(CombatDamage _script, string _uid, int _index)
        {
            for (int i = _script.DynamicFloatList.Count-1; i >=0; i--)
            {
                if (_script.DynamicFloatList[i].uid == _uid && i != _index)
                {
                    _script.DynamicFloatList.RemoveAt(i);
                    if (i < _index) _index--;
                }
            }
        }

    }
}
