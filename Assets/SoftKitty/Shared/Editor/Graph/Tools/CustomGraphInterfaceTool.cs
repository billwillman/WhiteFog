using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor.SceneManagement;

namespace SoftKitty
{
    public class CustomGraphInterfaceTool : EditorWindow
    {

        private NodeInterfaceElementType SelAction = NodeInterfaceElementType.Bool_Variable;
        private string newScriptName = "";
        private int newScriptType = 0;
        public Object actionScript;
        public string actionScriptName;
        public bool _interfaceExist = false;
        private Vector2 scroll;
        public CustomInterface newInterface;
        public bool[] TestBoolValue = new bool[0];
        public int[] TestIntValue = new int[0];
        public string Code ="";


        private string GetScriptPath()
        {
            string _basePath = Application.dataPath;
            string _scriptPath = AssetDatabase.GetAssetPath(actionScript);
            return _basePath.Substring(0, _basePath.Length - 6) + _scriptPath.Substring(0, _scriptPath.Length - 3) + "_interface.txt";
        }

        void OnGUI()
        {
            GUIStyle box = GUI.skin.box;


            EditorUtils.TitleLogo(EditorUtils.GetTexture(EditorIcon.GraphLogo),0);

            GUI.backgroundColor = EditorUtils._titleColor*0.5F;
            GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
            GUILayout.Label("Custom Graph Interface Tool", EditorUtils._boldTitleStyle);
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            EditorUtils.Document("master-combat-core/custom-node-tool");
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            GUI.backgroundColor = Color.white;
            scroll = GUILayout.BeginScrollView(scroll);

            if (actionScript == null)
            {
                EditorUtils.HelpInfo("This tool helps you create custom '<color=#4AFF66>Condition(Handler)</color>' | '<color=#FF2A2A>Action</color>' | '<color=#FFFFFF>Root</color>' type node without manually writing boilerplate code. \n" +
                    "You can design node inputs, exposed variables, and editor UI visually. The tool will automatically generate the base script for the node."+
                    "the base code and node graph interface file are in '<color=#0AAAFF>Assets/SoftKitty/Shared/Scripts/CustomClass/</color>' folder,\n" +
                    "After generation, simply implement your logic in the provided handler methods (for example, Execute() for action nodes). \n" +
                    "Once saved, the custom node becomes immediately available in the Visual Graph editor.", 15);
                GUI.backgroundColor = EditorUtils._black;
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                if (GUILayout.Button("Document")) {

                }
                GUILayout.Space(15);
                GUILayout.EndHorizontal();
                EditorGUILayout.Separator();

            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(15);
            GUILayout.Box(EditorUtils.TextHint("Existing Script:", "Load an existing custom node script to edit its interface definition. You can update inputs, variables, and UI layout, then regenerate the base code to match the new design."), EditorUtils._titleStyle, GUILayout.Width(100));
            GUI.color = Color.white;
            EditorGUI.BeginChangeCheck();
            actionScript = EditorGUILayout.ObjectField(actionScript, typeof(MonoScript), false, GUILayout.Width(203F));
            GUI.backgroundColor = EditorUtils._titleColor;
            if (EditorGUI.EndChangeCheck() || GUILayout.Button(new GUIContent( "Load", "Load an existing custom node script to edit its interface definition. You can update inputs, variables, and UI layout, then regenerate the base code to match the new design."), GUILayout.Width(80)))
            {
                newInterface = null;
                _interfaceExist = false;
                if (actionScript != null)
                {
                    if (((MonoScript)actionScript).GetClass().IsSubclassOf(typeof(CustomNode)) &&  File.Exists(GetScriptPath()))
                    {
                        newInterface = JsonUtility.FromJson<CustomInterface>(File.ReadAllText(GetScriptPath(), System.Text.Encoding.UTF8));
                        _interfaceExist = true;
                        TestBoolValue = new bool[newInterface.Elements.Length];
                        TestIntValue = new int[newInterface.Elements.Length];
                    }
                }
            }
            if (actionScript != null)
            {
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button(new GUIContent("Unload", "Unload the custom node script, make sure save it before click if you want to keep the changes."), GUILayout.Width(80)))
                {
                    newInterface = null;
                    _interfaceExist = false;
                    actionScript = null;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            bool _goodToGo = actionScript != null;
            if (actionScript != null)
            {
                try
                {
                    if (!((MonoScript)actionScript).GetClass().IsSubclassOf(typeof(CustomNode)))
                    {
                        _goodToGo = false;
                        EditorUtils.Warnning("The script you selected is not a Custom Node class, please select another one or create a new one.", 15);
                    }
                    else
                    {
                        EditorUtils.HelpInfo("Modifying an existing custom node interface will overwrite the generated code." +
                            "Any custom logic you added to the generated script may be < color=#FF2200>lost</color>. Please make sure to back up your code before clicking '<color=#FF0000>Overwrite Code</color>' button.", 15);
                    }
                }
                catch
                {
                    _goodToGo = false;
                }
            }

            EditorGUILayout.Separator();

            if (actionScript==null) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.Box(EditorUtils.TextHint("New Script:","Input the name of your new custom node, and click 'Create'."), EditorUtils._titleStyle, GUILayout.Width(100));
                GUI.color = Color.white;
                newScriptName = GUILayout.TextField(newScriptName,GUILayout.Width(100), GUILayout.Height(18));
                string[] _scriptTypes = new string[3] { "Handler", "Action", "Root" };
                GUI.backgroundColor = EditorUtils._black;
                newScriptType = EditorGUILayout.Popup(newScriptType, _scriptTypes, GUILayout.Width(100));
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button("Create",GUILayout.Width(80))) {
                    string _basePath = Application.dataPath;
                    string _newScriptPath = _basePath.Substring(0, _basePath.Length - 6) +"Assets/SoftKitty/Shared/Scripts/CustomClass/" + _scriptTypes[newScriptType] + "/" + actionScriptName + ".cs";
                    string _content = "using UnityEngine;\r\n";
                    _content += "namespace SoftKitty{\r\n";
                    _content += "    public class " + actionScriptName + " : " + _scriptTypes[newScriptType] + "{\r\n";
                    _content += "    }\r\n";
                    _content += "}\r\n";
                    newInterface = null;
                    _interfaceExist = false;
                    File.WriteAllText(_newScriptPath,_content,System.Text.Encoding.UTF8);
                    AssetDatabase.Refresh();
                    actionScript = (MonoScript)AssetDatabase.LoadAssetAtPath("Assets/SoftKitty/Shared/Scripts/CustomClass/" + _scriptTypes[newScriptType] + "/" + actionScriptName + ".cs", typeof(MonoScript));
                    Debug.Log("New script created:  " + _newScriptPath);
                    AssetDatabase.Refresh();
                    _goodToGo = false;
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(newScriptName))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(115);
                    GUI.color = Color.white;
                    actionScriptName = newScriptName + _scriptTypes[newScriptType];
                    GUILayout.Box("Assets/SoftKitty/Shared/Scripts/CustomClass/" + _scriptTypes[newScriptType] + "/" + actionScriptName + ".cs", EditorUtils._boxStyle, GUILayout.Height(20));
                    GUILayout.EndHorizontal();
                }

            }


            EditorGUILayout.Separator();

            if (actionScript != null && _goodToGo)
            {
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = EditorUtils._black;
                GUILayout.Space(10);
                GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                GUI.color = Color.white;
                GUILayout.Space(25);
                GUILayout.Label("Design",GUILayout.Height(25));
                GUILayout.EndHorizontal();
                GUILayout.Space(25);
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;

                actionScriptName = actionScript.name;
                GUILayout.BeginHorizontal();
                GUILayout.Space(35);
                GUI.backgroundColor = EditorUtils._black;
                GUILayout.Label( new GUIContent("Create New Element:", "Add a new input, variable, or option to this custom node. These elements define how the node appears and what data it exposes in the Visual Graph editor."), GUILayout.Width(140));
                SelAction = (NodeInterfaceElementType)EditorGUILayout.EnumPopup(SelAction, GUILayout.Width(150));
                GUI.backgroundColor = EditorUtils._buttonColor;
                if (GUILayout.Button(new GUIContent("Create", "Add a new input, variable, or option to this custom node. These elements define how the node appears and what data it exposes in the Visual Graph editor."), GUILayout.Width(75)))
                {
                    if (newInterface == null) newInterface = new CustomInterface();
                   
                    if (SelAction == NodeInterfaceElementType.Input_Float || SelAction == NodeInterfaceElementType.Input_Entity)
                    {
                        int _count = 0;
                        for (int u = 0; u < newInterface.Elements.Length; u++)
                        {
                            if (newInterface.Elements[u].Type == NodeInterfaceElementType.Input_Float || newInterface.Elements[u].Type == NodeInterfaceElementType.Input_Entity) _count++;
                        }
                        if (_count >= 5)
                        {
                            EditorUtility.DisplayDialog("Max number of [Input Variable] ports", "Sorry you can not have more than 5 [Input Variable] ports.", "OK");
                            GUILayout.EndHorizontal();
                            GUILayout.EndScrollView();
                            return;
                        }
                    }
                    List<NodeInterfaceElement> _elements = new List<NodeInterfaceElement>();
                    if (newInterface.Elements != null) _elements.AddRange(newInterface.Elements);
                    NodeInterfaceElement _newItem = new NodeInterfaceElement();
                    _newItem.Uid = "Element" + _elements.Count.ToString();
                    _newItem.VisibleBind = "";
                    _newItem.Label = "";
                    _newItem.ToolTips = "";
                    _newItem.Index = _elements.Count;
                    _newItem.Type = SelAction;
                    _elements.Add(_newItem);
                    newInterface.Elements = _elements.ToArray();
                    TestBoolValue = new bool[newInterface.Elements.Length];
                    TestIntValue = new int[newInterface.Elements.Length];
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                if (newInterface != null && newInterface.Elements != null)
                {
                    for (int i = 0; i < newInterface.Elements.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(35);
                        GUILayout.BeginVertical(EditorUtils._groupStyle);

                        GUI.backgroundColor = Color.gray;
                        GUILayout.BeginHorizontal(EditorUtils._titleBgStyle,GUILayout.Height(22));
                        GUI.backgroundColor = i > 0 ? Color.white : Color.gray;
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(EditorUtils.GetTexture(EditorIcon.Up), EditorUtils._toolButtonStyle, GUILayout.Width(30), GUILayout.Height(20)) && i > 0)
                        {
                            List<NodeInterfaceElement> _elements = new List<NodeInterfaceElement>();
                            if (newInterface.Elements != null) _elements.AddRange(newInterface.Elements);
                            _elements.Reverse(i - 1, 2);
                            newInterface.Elements = _elements.ToArray();
                        }
                        GUI.backgroundColor = i < newInterface.Elements.Length - 1 ? Color.white : Color.gray;
                        if (GUILayout.Button(EditorUtils.GetTexture(EditorIcon.Down), EditorUtils._toolButtonStyle, GUILayout.Width(30), GUILayout.Height(20)) && i < newInterface.Elements.Length - 1)
                        {
                            List<NodeInterfaceElement> _elements = new List<NodeInterfaceElement>();
                            if (newInterface.Elements != null) _elements.AddRange(newInterface.Elements);
                            _elements.Reverse(i, 2);
                            newInterface.Elements = _elements.ToArray();
                        }
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(17)))
                        {
                            List<NodeInterfaceElement> _elements = new List<NodeInterfaceElement>();
                            if (newInterface.Elements != null) _elements.AddRange(newInterface.Elements);
                            _elements.RemoveAt(i);
                            newInterface.Elements = _elements.ToArray();
                            GUILayout.EndHorizontal();
                            GUILayout.EndScrollView();
                            TestBoolValue = new bool[newInterface.Elements.Length];
                            TestIntValue = new int[newInterface.Elements.Length];
                            return;
                        }
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        int _count = 0;
                        for (int u = 0; u < i; u++)
                        {
                            if (newInterface.Elements[u].Type == newInterface.Elements[i].Type)
                                _count++;
                            else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Int_Variable && newInterface.Elements[u].Type == NodeInterfaceElementType.Enum)
                                _count++;
                            else if (newInterface.Elements[u].Type == NodeInterfaceElementType.Int_Variable && newInterface.Elements[i].Type == NodeInterfaceElementType.Enum)
                                _count++;
                            else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Input_Float && newInterface.Elements[u].Type == NodeInterfaceElementType.Input_Entity)
                                _count++;
                            else if (newInterface.Elements[u].Type == NodeInterfaceElementType.Input_Float && newInterface.Elements[i].Type == NodeInterfaceElementType.Input_Entity)
                                _count++;
                        }

                        newInterface.Elements[i].Index = _count;
                        GUI.backgroundColor = EditorUtils._black;
                        newInterface.Elements[i].Type = (NodeInterfaceElementType)EditorGUILayout.EnumPopup(newInterface.Elements[i].Type, GUILayout.Width(90));
                        GUILayout.Label("[" + newInterface.Elements[i].Index + "]", GUILayout.Width(20));
                        GUI.color = Color.white;
                        if (newInterface.Elements[i].Type != NodeInterfaceElementType.Label && newInterface.Elements[i].Type != NodeInterfaceElementType.Separator)
                        {
                            GUILayout.Label("UID:", GUILayout.Width(30));
                            string _oldUid = newInterface.Elements[i].Uid;
                            GUI.backgroundColor = EditorUtils._gray;
                            newInterface.Elements[i].Uid = GUILayout.TextField(newInterface.Elements[i].Uid, GUILayout.Width(105)).Replace("!", "").Replace("#", "").Replace(" ", "");

                            if (newInterface.Elements[i].Uid == "" || newInterface.Elements[i].Uid == null)
                            {
                                newInterface.Elements[i].Uid = "Element" + i.ToString();
                            }
                            else
                            {
                                bool _foundSame = false;
                                for (int u = 0; u < newInterface.Elements.Length; u++)
                                {
                                    if (u != i && newInterface.Elements[u].Uid == newInterface.Elements[i].Uid) _foundSame = true;
                                }
                                if (_foundSame) newInterface.Elements[i].Uid += "1";
                            }

                            if (_oldUid != newInterface.Elements[i].Uid && _oldUid != "")
                            {
                                foreach (var obj in newInterface.Elements)
                                {
                                    string _compareUid = obj.VisibleBind.Replace("!", "");
                                    string _enumId = "";
                                    if (_compareUid.Contains("#"))
                                    {
                                        string[] _args = _compareUid.Split("#");
                                        _compareUid = _args[0];
                                        _enumId = "#" + _args[1];
                                    }
                                    if (_compareUid == _oldUid)
                                    {
                                        bool _reverse = obj.VisibleBind.Contains("!");
                                        obj.VisibleBind = newInterface.Elements[i].Uid + (_reverse ? "!" : "") + _enumId;
                                    }
                                }
                            }
                        }
                        else
                        {
                            GUILayout.FlexibleSpace();
                        }
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();

                        if (newInterface.Elements[i].Type != NodeInterfaceElementType.Separator)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            GUILayout.Label("Label:", GUILayout.Width(50));
                            newInterface.Elements[i].Label = GUILayout.TextField(newInterface.Elements[i].Label, GUILayout.Width(100));
                            GUILayout.Label("Tooltip:", GUILayout.Width(50));
                            newInterface.Elements[i].ToolTips = GUILayout.TextField(newInterface.Elements[i].ToolTips, GUILayout.Height(20));
                            GUILayout.EndHorizontal();

                            if (newInterface.Elements[i].Type == NodeInterfaceElementType.Enum)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(20);
                                if (newInterface.Elements[i].EnumOptions == null) newInterface.Elements[i].EnumOptions = new string[0];
                                GUILayout.Label("Enum Options(" + newInterface.Elements[i].EnumOptions.Length.ToString() + "):", GUILayout.Width(100));
                                GUI.backgroundColor = EditorUtils._titleColor;
                                if (GUILayout.Button(new GUIContent( "Add","Add a new Enum option."), GUILayout.Width(65)))
                                {
                                    List<string> _options = new List<string>(newInterface.Elements[i].EnumOptions);
                                    _options.Add("New Enum");
                                    newInterface.Elements[i].EnumOptions = _options.ToArray();
                                }
                                if (GUILayout.Button(new GUIContent("Clear","Remove all options."), GUILayout.Width(65)))
                                {
                                    newInterface.Elements[i].EnumOptions = new string[0];
                                }
                                GUI.backgroundColor = Color.white;
                                GUILayout.EndHorizontal();

                                for (int u = 0; u < newInterface.Elements[i].EnumOptions.Length; u++)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(40);
                                    newInterface.Elements[i].EnumOptions[u] = GUILayout.TextField(newInterface.Elements[i].EnumOptions[u], GUILayout.Width(100));
                                    GUI.backgroundColor = EditorUtils._black;
                                    if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(17)))
                                    {
                                        List<string> _options = new List<string>(newInterface.Elements[i].EnumOptions);
                                        _options.RemoveAt(u);
                                        newInterface.Elements[i].EnumOptions = _options.ToArray();
                                        GUI.backgroundColor = Color.white;
                                        GUILayout.EndHorizontal();
                                        break;
                                    }
                                    GUI.backgroundColor = Color.white;
                                    GUILayout.EndHorizontal();
                                }
                            }

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            bool _visibleBind = newInterface.Elements[i].VisibleBind != "";
                            _visibleBind = GUILayout.Toggle(_visibleBind,new GUIContent( "Visible Bind", "Allows this field to be conditionally shown or hidden in the node UI, based on another value. This helps keep the node interface clean and context-aware."), GUILayout.Width(100));
                            List<string> _bindOptions = new List<string>();
                            if (_visibleBind)
                            {
                                foreach (var obj in newInterface.Elements)
                                {
                                    if ((obj.Type == NodeInterfaceElementType.Bool_Variable || obj.Type == NodeInterfaceElementType.Enum) && obj.Uid != "" && !_bindOptions.Contains(obj.Uid) && obj.Uid != newInterface.Elements[i].Uid)
                                    {
                                        _bindOptions.Add(obj.Uid);
                                    }
                                }
                            }
                            if (_visibleBind && newInterface.Elements[i].VisibleBind == "")
                            {
                                if (_bindOptions.Count > 0)
                                {
                                    newInterface.Elements[i].VisibleBind = _bindOptions[0];
                                }
                                else
                                {
                                    _visibleBind = false;
                                    EditorUtility.DisplayDialog("No [Bool Variable] or [Enum] found!", "You can only bind the visibility of this interface element with another [bool variable] or [Enum], No matching element found, please add one before check 'Visible Bind' option.", "OK");
                                }
                            }
                            if (!_visibleBind && newInterface.Elements[i].VisibleBind != "")
                            {
                                newInterface.Elements[i].VisibleBind = "";
                            }
                            if (_visibleBind && _bindOptions.Count > 0)
                            {

                                string _bindUid = newInterface.Elements[i].VisibleBind.Replace("!", "");
                                int _bindEnumId = 0;
                                string[] bindEnumOptions = new string[0];
                                if (_bindUid.Contains("#"))
                                {
                                    string[] _args = _bindUid.Split("#");
                                    _bindUid = _args[0];
                                    int.TryParse(_args[1], out _bindEnumId);
                                }
                                NodeInterfaceElementType _bindType = NodeInterfaceElementType.Bool_Variable;
                                foreach (var obj in newInterface.Elements)
                                {
                                    if (obj.Uid == _bindUid)
                                    {
                                        _bindType = obj.Type;
                                        if (obj.Type == NodeInterfaceElementType.Enum)
                                        {
                                            if (obj.EnumOptions == null) obj.EnumOptions = new string[0];
                                            bindEnumOptions = obj.EnumOptions;
                                        }
                                    }
                                }
                                if (_bindType == NodeInterfaceElementType.Enum && !newInterface.Elements[i].VisibleBind.Contains("#"))
                                {
                                    newInterface.Elements[i].VisibleBind += "#0";
                                    _bindEnumId = 0;
                                }
                                int _index = Mathf.Max(_bindOptions.IndexOf(_bindUid), 0);
                                EditorGUI.BeginChangeCheck();
                                _index = EditorGUILayout.Popup(_index, _bindOptions.ToArray(), GUILayout.Width(100));
                                if (EditorGUI.EndChangeCheck())
                                {
                                    newInterface.Elements[i].VisibleBind = _bindOptions[_index];
                                    _bindUid = newInterface.Elements[i].VisibleBind.Replace("!", "");
                                     _bindEnumId = 0;
                                    bindEnumOptions = new string[0];
                                    if (_bindUid.Contains("#"))
                                    {
                                        string[] _args = _bindUid.Split("#");
                                        _bindUid = _args[0];
                                        int.TryParse(_args[1], out _bindEnumId);
                                    }
                                }
                                if (_bindType == NodeInterfaceElementType.Enum)
                                {
                                    GUILayout.Label(">", GUILayout.Width(15));
                                    _bindEnumId = EditorGUILayout.Popup(_bindEnumId, bindEnumOptions, GUILayout.Width(100));
                                }

                                bool _reverseValue = newInterface.Elements[i].VisibleBind.Contains("!");
                                _reverseValue = GUILayout.Toggle(_reverseValue, "Invert Value", GUILayout.Width(100));
                                if (_bindType == NodeInterfaceElementType.Enum)
                                {
                                    newInterface.Elements[i].VisibleBind = _bindUid + "#" + _bindEnumId.ToString() + (_reverseValue ? "!" : "");
                                }
                                else
                                {
                                    newInterface.Elements[i].VisibleBind = _bindUid + (_reverseValue ? "!" : "");
                                }
                            }

                            GUILayout.EndHorizontal();
                        }

                        GUILayout.EndVertical();
                        GUILayout.Space(35);
                        GUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                    }

                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = EditorUtils._titleColor * 0.6F;
                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                    GUI.color = Color.white;
                    GUILayout.Space(25);
                    GUILayout.Label(new GUIContent("Graph Node Preview", "Preview how this custom node will look and behave inside the Visual Graph editor. This preview updates in real time as you modify the interface design."),GUILayout.Height(25));
                    GUILayout.EndHorizontal();
                    GUILayout.Space(25);
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.white;
                    GUI.color = Color.white;

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(35);
                    GUILayout.BeginVertical(EditorUtils._groupStyle, GUILayout.Width(300));
                    EditorGUILayout.Separator();
                    for (int i = 0; i < newInterface.Elements.Length; i++)
                    {
                        bool _visible = true;
                        if (newInterface.Elements[i].VisibleBind != "")
                        {
                            string _bindUid = newInterface.Elements[i].VisibleBind.Replace("!", "");
                            int _bindEnumId = 0;
                            if (_bindUid.Contains("#"))
                            {
                                string[] _args = _bindUid.Split("#");
                                _bindUid = _args[0];
                                int.TryParse(_args[1], out _bindEnumId);
                            }
                            for (int u = 0; u < newInterface.Elements.Length; u++)
                            {
                                if (_bindUid == newInterface.Elements[u].Uid)
                                {
                                    if (newInterface.Elements[u].Type == NodeInterfaceElementType.Bool_Variable)
                                    {
                                        if (newInterface.Elements[i].VisibleBind.Contains("!"))
                                            _visible = !TestBoolValue[u];
                                        else
                                            _visible = TestBoolValue[u];
                                    }
                                    else if (newInterface.Elements[u].Type == NodeInterfaceElementType.Enum)
                                    {
                                        if (newInterface.Elements[i].VisibleBind.Contains("!"))
                                            _visible = TestIntValue[u] != _bindEnumId;
                                        else
                                            _visible = TestIntValue[u] == _bindEnumId;
                                    }
                                }
                            }
                        }
                        if (_visible)
                        {
                            GUILayout.BeginHorizontal();
                            if (newInterface.Elements[i].Type == NodeInterfaceElementType.Separator)
                            {
                                EditorGUILayout.Separator();
                            }
                            else
                            {
                                if (newInterface.Elements[i].Type == NodeInterfaceElementType.Input_Float)
                                {
                                    GUI.backgroundColor = Color.green;
                                    GUI.color = Color.green;
                                    GUILayout.Box("O", GUILayout.Width(20), GUILayout.Height(18));
                                    GUI.backgroundColor = Color.white;
                                    GUI.color = Color.white;
                                    GUILayout.Label(new GUIContent(newInterface.Elements[i].Label, newInterface.Elements[i].ToolTips), GUILayout.Width(150F), GUILayout.Height(20));
                                }
                                else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Input_Entity)
                                {
                                    GUI.backgroundColor = new Color(0F,0.8F,1F,1F);
                                    GUI.color = new Color(0F, 0.8F, 1F, 1F);
                                    GUILayout.Box("O", GUILayout.Width(20), GUILayout.Height(18));
                                    GUI.backgroundColor = Color.white;
                                    GUI.color = Color.white;
                                    GUILayout.Label(new GUIContent(newInterface.Elements[i].Label, newInterface.Elements[i].ToolTips), GUILayout.Width(150F), GUILayout.Height(20));
                                }
                                else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Label)
                                {
                                    GUILayout.Space(25);
                                    GUILayout.Label(new GUIContent(newInterface.Elements[i].Label, newInterface.Elements[i].ToolTips), GUILayout.Height(20));
                                }
                                else
                                {
                                    GUILayout.Space(25);
                                    GUILayout.Label(new GUIContent(newInterface.Elements[i].Label, newInterface.Elements[i].ToolTips), EditorUtils._titleStyle, GUILayout.Width(120F), GUILayout.Height(20));
                                }

                                GUI.backgroundColor = Color.black;
                                GUI.color = Color.gray;
                                if (newInterface.Elements[i].Type == NodeInterfaceElementType.Float_Variable)
                                {
                                    GUILayout.Box("Float Input Field", GUILayout.Width(100), GUILayout.Height(18));
                                }
                                else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Int_Variable)
                                {
                                    GUILayout.Box("Int Input Field", GUILayout.Width(82), GUILayout.Height(18));
                                    GUILayout.Box("-", GUILayout.Width(18), GUILayout.Height(18));
                                }
                                else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Enum)
                                {
                                    GUI.backgroundColor = Color.white;
                                    TestIntValue[i] = EditorGUILayout.Popup(TestIntValue[i], newInterface.Elements[i].EnumOptions, GUILayout.Width(100), GUILayout.Height(18));
                                }
                                else if (newInterface.Elements[i].Type == NodeInterfaceElementType.String_Variable)
                                {
                                    GUILayout.Box("Text Input Field", GUILayout.Width(100), GUILayout.Height(18));
                                }
                                else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Bool_Variable)
                                {
                                    GUI.backgroundColor = Color.white;
                                    TestBoolValue[i] = GUILayout.Toggle(TestBoolValue[i], "", GUILayout.Width(50), GUILayout.Height(20));
                                }
                                GUI.backgroundColor = Color.white;
                                GUI.color = Color.white;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    EditorGUILayout.Separator();
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = _interfaceExist ? EditorUtils._titleColor : EditorUtils._buttonColor;
                    GUILayout.Space(35);
                    if (GUILayout.Button(new GUIContent((_interfaceExist ? "Overwrite" : "Create") + " Interface Asset",
                        "Apply the current interface design to the node asset. This updates how the node appears in the Visual Graph editor without changing runtime logic."), GUILayout.Width(300)))
                    {
                        string _json = JsonUtility.ToJson(newInterface);
                        File.WriteAllText(GetScriptPath(), _json, System.Text.Encoding.UTF8);
                        AssetDatabase.Refresh();
                        EditorUtility.DisplayDialog("Save", "Node interface configuration saved! Path:\n" + GetScriptPath(), "OK");
                        _interfaceExist = true;
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();
                    if (!_interfaceExist)
                    {
                        EditorUtils.Warnning("You have not saved your node interface configuration yet.", 35);
                    }
                }

                if (newInterface != null)
                {
                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();
                    GUI.color = Color.white;
 
                    string _codeType = actionScriptName;
                    int _baseInputCount = 0;
                    if (actionScriptName.ToLower().EndsWith("handler"))
                    {
                        _codeType = "Handler";
                        _baseInputCount = 0;
                    }
                    else if (actionScriptName.ToLower().EndsWith("action"))
                    {
                        _codeType = "Action";
                        _baseInputCount = 1;
                    }
                    else if (actionScriptName.ToLower().EndsWith("root"))
                    {
                        _codeType = "Root";
                        _baseInputCount = 1;
                    }
                    
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = EditorUtils._buttonColor*0.5F;
                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                    GUI.color = Color.white;
                    GUILayout.Space(25);
                    GUILayout.Label(new GUIContent( "Code:   < <color=#22FF88>" + actionScriptName + "</color> : <color=#22FF88>" + _codeType + "</color> >",
                        "This is the auto-generated base code for the custom node. You only need to implement your logic inside the designated methods. The rest of the code is managed by the system and should not be edited manually."), EditorUtils._richLabelStyle, GUILayout.Height(25));
                    GUILayout.EndHorizontal();
                    GUILayout.Space(25);
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.white;


                    GUILayout.BeginHorizontal();
                    GUILayout.Space(35);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    if (GUILayout.Button(new GUIContent("Copy Code", "Copy the generated base code to the clipboard."), GUILayout.Width(100)))
                    {
                        Debug.Log(Code);
                        GUIUtility.systemCopyBuffer = Code;
                    }
                    GUI.backgroundColor = EditorUtils._red;
                    if (GUILayout.Button(new GUIContent("Overwrite Code", "Replace the existing generated code with the current interface definition. Make sure any custom logic is backed up before proceeding."), GUILayout.Width(100)))
                    {
                        if (EditorUtility.DisplayDialog("Overwrite", "Are you sure you want to overwrite the entire script with the content showing below?", "Confirm", "Cancel"))
                        {
                            string _basePath = Application.dataPath;
                            string _newScriptPath = _basePath.Substring(0, _basePath.Length - 6) + "Assets/SoftKitty/Shared/Scripts/CustomClass/" + _codeType + "/" + actionScriptName + ".cs";
                            File.WriteAllText(_newScriptPath, Code, System.Text.Encoding.UTF8);
                            AssetDatabase.Refresh();
                        }
                    }
                    GUI.backgroundColor = Color.white*0.6F;
                    GUILayout.EndHorizontal();
                    Code = "";
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(35);
                    GUILayout.BeginVertical(EditorUtils._groupStyle);
                    GUI.color = EditorUtils._codeColor;

                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!","using UnityEngine;"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "namespace SoftKitty{"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "    public class " + actionScriptName + ":" + _codeType + "{"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();


                    for (int i = 0; i < newInterface.Elements.Length; i++)
                    {
                        if (newInterface.Elements[i].Type == NodeInterfaceElementType.Enum)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode(newInterface.Elements[i].Uid + "s","        public enum " + newInterface.Elements[i].Uid + "s{"), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                            for (int u = 0; u < newInterface.Elements[i].EnumOptions.Length; u++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(AddCode("None!", "          " + newInterface.Elements[i].EnumOptions[u].Replace(" ", "_") + (u != newInterface.Elements[i].EnumOptions.Length - 1 ? "," : "")), EditorUtils._richLabelStyle);
                                GUILayout.EndHorizontal();
                            }
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode(newInterface.Elements[i].Uid + "s","        public " + newInterface.Elements[i].Uid + "s " + newInterface.Elements[i].Uid + "; //" + newInterface.Elements[i].ToolTips), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                        }
                        else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Input_Entity)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "        public Entity " + newInterface.Elements[i].Uid + "(GraphRuntimeState _runtime) { //" + newInterface.Elements[i].ToolTips), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "            return GetEntity(inputPort[" + (_baseInputCount + newInterface.Elements[i].Index).ToString() + "].connectedNodes[0],_runtime);"), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                        }
                        else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Input_Float)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "        public float " + newInterface.Elements[i].Uid + "(GraphRuntimeState _runtime) { //" + newInterface.Elements[i].ToolTips), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "            return GetVariableValue(inputPort[" + (_baseInputCount + newInterface.Elements[i].Index).ToString() + "].connectedNodes[0],_runtime);"), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                        }
                        else if (newInterface.Elements[i].Type != NodeInterfaceElementType.Separator
                            && newInterface.Elements[i].Type != NodeInterfaceElementType.Label)
                        {
                            string _variableType = "int";
                            if (newInterface.Elements[i].Type.ToString().Contains("_Variable"))
                            {
                                _variableType = newInterface.Elements[i].Type.ToString().Replace("_Variable", "").ToLower();
                            }
                            else if (newInterface.Elements[i].Type.ToString().Contains("Input"))
                            {
                                _variableType = "string";
                            }
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(AddCode("None!", "        public " + _variableType + " " + newInterface.Elements[i].Uid + "; //" + newInterface.Elements[i].ToolTips), EditorUtils._richLabelStyle);
                            GUILayout.EndHorizontal();
                        }


                    }

                    EditorGUILayout.Separator();
                    AddCode(actionScriptName, " ");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "        public " + actionScriptName + "(){}"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();
                    EditorGUILayout.Separator();
                    AddCode(actionScriptName, " ");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "        public " + actionScriptName + "(" + _codeType + " _baseClass) : base(_baseClass){}"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();
                    EditorGUILayout.Separator();
                    AddCode(actionScriptName, " ");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "        public " + actionScriptName + " Init" + _codeType + "(" + _codeType + " _baseClass){"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "            " + actionScriptName + "  _newObj =new " + actionScriptName + "(_baseClass);"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    for (int i = 0; i < newInterface.Elements.Length; i++)
                    {
                        if (newInterface.Elements[i].Type != NodeInterfaceElementType.Separator
                            && newInterface.Elements[i].Type != NodeInterfaceElementType.Label)
                        {
                            GUILayout.BeginHorizontal();
                            if (newInterface.Elements[i].Type == NodeInterfaceElementType.Bool_Variable)
                            {
                                GUILayout.Label(AddCode("None!", "            _newObj." + newInterface.Elements[i].Uid + " = _baseClass.BoolValues[" + newInterface.Elements[i].Index + "];"), EditorUtils._richLabelStyle);
                            }
                            else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Int_Variable)
                            {
                                GUILayout.Label(AddCode("None!", "            _newObj." + newInterface.Elements[i].Uid + " = _baseClass.IntValues[" + newInterface.Elements[i].Index + "];"), EditorUtils._richLabelStyle);
                            }
                            else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Enum)
                            {
                                GUILayout.Label(AddCode(newInterface.Elements[i].Uid + "s", "            _newObj." + newInterface.Elements[i].Uid + " = (" + newInterface.Elements[i].Uid + "s)_baseClass.IntValues[" + newInterface.Elements[i].Index + "];"), EditorUtils._richLabelStyle);
                            }
                            else if (newInterface.Elements[i].Type == NodeInterfaceElementType.Float_Variable)
                            {
                                GUILayout.Label(AddCode("None!", "            _newObj." + newInterface.Elements[i].Uid + " = _baseClass.FloatValues[" + newInterface.Elements[i].Index + "];"), EditorUtils._richLabelStyle);
                            }
                            else if (newInterface.Elements[i].Type == NodeInterfaceElementType.String_Variable)
                            {
                                GUILayout.Label(AddCode("None!", "            _newObj." + newInterface.Elements[i].Uid + " = _baseClass.StringValues[" + newInterface.Elements[i].Index + "];"), EditorUtils._richLabelStyle);
                            }


                            GUILayout.EndHorizontal();
                        }
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "            return _newObj;"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();
                    AddCode("None!", " ");
                    if (_codeType == "Handler")
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "            //if(condition code)SetActive(_runtime, result bool);"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                    }
                    else if (_codeType == "Action")
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "            //Your action code here."), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                    }
                    else if (_codeType == "Root")
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        public override void StructureBuild(){ //New Root Class"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "            //Your code here."), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        public override void StructureReset(){ //New Root Class with everything reset to default"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "            //Your code here."), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(AddCode("None!", "        }"), EditorUtils._richLabelStyle);
                        GUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.Separator();
                    AddCode("None!", " ");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "    }"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(AddCode("None!", "}"), EditorUtils._richLabelStyle);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();

                    GUI.color = Color.white;
                    GUILayout.EndVertical();
                    GUILayout.Space(35);
                    GUILayout.EndHorizontal();
                }
            }


            GUILayout.EndScrollView();

        }

        private string AddCode(string _scriptName, string _text)
        {
            Code += _text + "\r\n";
            return GetDisplayCode(_scriptName,_text);
        }

        private string GetDisplayCode(string _scriptName, string _text)
        {
            int _index = _text.IndexOf("//");
            string _comment = "";
            if (_index >= 0)
            {
                _comment = "<color=#57a64a>" + _text.Substring(_index, _text.Length-_index)+ "</color>";
                _text = _text.Substring(0, _index);
            }
            return _text.Replace("string", "<color=#569cd6>string</color>")
                .Replace("enum", "<color=#569cd6>enum</color>")
                .Replace("GraphRuntimeState", "<color=#569cd6>GraphRuntimeState</color>")
                .Replace(" base", "<color=#569cd6> base</color>")
                .Replace("new ", "<color=#569cd6>new </color>")
                .Replace("class", "<color=#569cd6>class</color>")
                .Replace("override void", "<color=#569cd6>override void</color>")
                .Replace("float", "<color=#569cd6>float</color>")
                .Replace("int", "<color=#569cd6>int</color>")
                .Replace("bool", "<color=#569cd6>bool</color>")
                .Replace("public", "<color=#569cd6>public</color>")
                .Replace("private", "<color=#569cd6>private</color>")
                .Replace("using", "<color=#569cd6>using</color>")
                .Replace("namespace", "<color=#569cd6>namespace</color>")
                .Replace(actionScriptName, "<color=#4ec9b0>"+ actionScriptName + "</color>")
                .Replace(_scriptName, "<color=#7fc2a1>" + _scriptName + "</color>")
                .Replace("Action ", "<color=#4ec9b0>Action </color>")
                .Replace("Handler ", "<color=#4ec9b0>Handler </color>")
                .Replace("Root ", "<color=#4ec9b0>Root </color>")
                .Replace(":Action", "<color=#4ec9b0> : Action</color>")
                .Replace(":Handler", "<color=#4ec9b0> : Handler</color>")
                .Replace(":Root", "<color=#4ec9b0> : Root</color>")
                .Replace(" Entity ", "<color=#4ec9b0> Entity </color>")
                .Replace("_runtime", "<color=#9cdcfe>_runtime</color>")
                .Replace("_newObj", "<color=#9cdcfe>_newObj</color>")
                .Replace("_baseClass", "<color=#9cdcfe>_baseClass</color>")
                .Replace("GetEntity", "<color=#dcdcaa>GetEntity</color>")
                .Replace("GetVariableValue", "<color=#dcdcaa>GetVariableValue</color>")
                .Replace("InitAction", "<color=#dcdcaa>InitAction</color>")
                .Replace("InitHandler", "<color=#dcdcaa>InitHandler</color>")
                .Replace("Execute", "<color=#dcdcaa>Execute</color>")
                .Replace(" Update", "<color=#dcdcaa> Update</color>")
                .Replace("return", "<color=#d8a0df>return</color>")
                + _comment;
        }



    }
}
