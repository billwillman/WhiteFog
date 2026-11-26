using UnityEngine;
using UnityEditor;
using System.Collections.Generic;


namespace SoftKitty
{
    [CustomEditor(typeof(GraphObject), true)]
    public class GraphObject_Inspector : Editor
    {

        private bool nativeInspector = false;
        private GUIStyle _labelStyle;
        private GUIStyle _summaryStyle;
        public override void OnInspectorGUI()
        {
            GraphObject myTarget = (GraphObject)target;
            if (myTarget is SkillFormula formula)
            {
                EditorGUILayout.LabelField("运行时输出节点", formula.outputUid);
                if (GUILayout.Button("在公式页编辑与试算")) SkillWorkbench.Open(formula);
                using (new EditorGUI.DisabledScope(formula.RootSet == null))
                    if (GUILayout.Button("打开公式节点树")) MasterGraphWindow.OpenWindow(formula);
                return;
            }
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.alignment = TextAnchor.MiddleLeft;
            _summaryStyle= new GUIStyle(GUI.skin.box);
            _summaryStyle.alignment = TextAnchor.MiddleLeft;
            _summaryStyle.padding = new RectOffset(25,25,15,15);
            _summaryStyle.richText = true;

            GUILayout.BeginHorizontal();
            GUILayout.Box(EditorUtils.GetTexture(EditorIcon.GraphLogo));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0F, 0.8F, 1F);
            if (GUILayout.Button(new GUIContent("打开技能工作台", "以时间轴和表单编辑当前图资产。")))
            {
                SkillWorkbench.Open(myTarget);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("打开技能图编辑器", "编辑当前技能图的节点和连线。")))
            {
                MasterGraphWindow.OpenWindow(myTarget);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button(new GUIContent("设计自定义节点", "打开自定义节点接口工具。")))
            {
                var _window = EditorWindow.GetWindow<CustomGraphInterfaceTool>(false, "自定义节点接口", true);
                _window.minSize = new Vector2(580F, 700F);
                _window.Show();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button(new GUIContent("导出技能图", "将技能图和关联数据打包成 UnityPackage。")))
            {
                string _path= EditorUtility.SaveFilePanel("Share this Graph",
                    "Please select a folder to save the package file of this Graph and associated data files.",myTarget.name, "unitypackage");
                if (!string.IsNullOrEmpty(_path)) {

                    List<string> _fileList = new List<string>();
                    _fileList.Add(AssetDatabase.GetAssetPath(myTarget));
                    bool _hasAtt= false;
                    foreach (var obj in myTarget.RootSet.Handlers)
                    {
                        if (obj.ClassName.ToLower().Contains("attribute")) _hasAtt = true;
                    }
                    foreach (var obj in myTarget.RootSet.Actions)
                    {
                        if (obj.ClassName.ToLower().Contains("attribute")) _hasAtt = true;
                        var c = obj.calculation;
                        if (obj.ClassName == "FormulaImpact" && c != null)
                        {
                            _hasAtt = true;
                            foreach (var asset in new ScriptableObject[] { c.damage, c.accuracy, c.settlement, c.levels })
                            {
                                if (!asset) continue;
                                string path = AssetDatabase.GetAssetPath(asset);
                                if (!_fileList.Contains(path)) _fileList.Add(path);
                            }
                        }
                    }
                    foreach (var obj in myTarget.RootSet.Variables)
                    {
                        if (obj.Type== VariableType.Attribute) _hasAtt = true;
                    }
                    if (_hasAtt && GameManager.AttributeData!=null) _fileList.Add(AssetDatabase.GetAssetPath(GameManager.AttributeData));
                    bool _hasOTE = false;
                    foreach (var obj in myTarget.RootSet.Handlers)
                    {
                        if (obj.ClassName.ToLower().Contains("over") && obj.ClassName.ToLower().Contains("time"))_hasOTE = true;
                    }
                    foreach (var obj in myTarget.RootSet.Actions)
                    {
                        if (obj.ClassName.ToLower().Contains("over") && obj.ClassName.ToLower().Contains("time"))_hasOTE = true;
                    }
                    if (_hasOTE && GameManager.OverTimeEffectData!=null) _fileList.Add(AssetDatabase.GetAssetPath(GameManager.OverTimeEffectData));
                    AssetDatabase.ExportPackage(_fileList.ToArray(), _path, ExportPackageOptions.Interactive);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button(new GUIContent("重置", "清空当前技能图的数据。")))
            {
                if (EditorUtility.DisplayDialog("重置技能图", "确定清空当前技能图的数据吗？", "重置", "取消"))
                {
                    myTarget.RootSet = null;
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.white;
                    return;
                }
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();
            EditorUtils.Document("master-combat-core/graph-system/GraphObject",true);

            GUI.backgroundColor = Color.white;

            if (myTarget.RootSet != null)
            {
                EditorGUILayout.Separator();

                GUILayout.BeginVertical();
                GUI.color = Color.white;
                GUI.backgroundColor = new Color(0.1F,0.5F,0.9F,1F);
                GUILayout.Box(myTarget.GetSummary, _summaryStyle);
                GUI.backgroundColor = Color.white;
                GUILayout.EndVertical();

                EditorGUILayout.Separator();
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("Summary:");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Actions:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Actions.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Triggers:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Triggers.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Condition Groups:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.ConditionGroups.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Conditions:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Handlers.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Math Nodes:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Maths.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Variable Nodes:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Variables.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Entity Nodes:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Entities.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.65F, 0.8F, 0.8F, 1F);
                GUILayout.Label("Branch Nodes:", _labelStyle, GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label(myTarget.RootSet.Branchs.Count.ToString(), _labelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.8F, 0.75F, 0.6F, 1F);
                GUILayout.Label("Dynamic Variable: ", _labelStyle, GUILayout.Width(120));
                GUI.color = new Color(0.6F, 0.8F, 0.65F, 1F);
                GUILayout.Label(" // <GraphInstance>().SetDynamicValue(string _uid, float _value)", _labelStyle);
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

      
                foreach (var obj in myTarget.RootSet.GetDynamicFloatList()) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUI.color = Color.white;
                        GUILayout.Label("[Float Variable]  (uid: "+obj.uid+")  (default value: "+obj.value+")", _labelStyle);
                        GUILayout.EndHorizontal();
                }
                if (myTarget.RootSet.GetDynamicFloatList().Count == 0) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.color = Color.white;
                    GUILayout.Label("None", _labelStyle);
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUI.color = new Color(0.8F, 0.75F, 0.6F, 1F);
                GUILayout.Label("Involved Entity: ", _labelStyle, GUILayout.Width(120));
                GUI.color = new Color(0.6F, 0.8F, 0.65F, 1F);
                GUILayout.Label(" // <GraphInstance>().SetDealerEntity(Entity _entity) or SetTargetEntity..", _labelStyle);
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                int _count = 0;
                foreach (var obj in myTarget.RootSet.Entities)
                {
                    if (obj.Type == EntityType.Target)
                    {
                        _count++;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUI.color = Color.white;
                        GUILayout.Label("[Entity #" + _count + "]  (Target)  (api: SetTargetEntity)", _labelStyle);
                        GUILayout.EndHorizontal();
                    }else if (obj.Type == EntityType.Dealer)
                    {
                        _count++;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUI.color = Color.white;
                        GUILayout.Label("[Entity #" + _count + "]  (Dealer)  (api: SetDealerEntity)", _labelStyle);
                        GUILayout.EndHorizontal();
                    }
                }
                if (_count == 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.color = Color.white;
                    GUILayout.Label("None", _labelStyle);
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();

                EditorGUILayout.Separator();

                GUI.color = Color.white;
                nativeInspector = EditorGUILayout.Foldout(nativeInspector, "Native Inspector");
                if (nativeInspector)
                {
                    base.OnInspectorGUI();
                }
            }
        }
    }
}
