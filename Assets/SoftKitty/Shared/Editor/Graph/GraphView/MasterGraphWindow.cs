using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;


namespace SoftKitty
{
    public class MasterGraphWindow : EditorWindow
    {
        public static void OpenWindow(GraphObject _graphObject)
        {
            if (_graphObject == null) return;
            if (wnd != null) wnd.Close();
            wnd = GetWindow<MasterGraphWindow>();
            wnd.minSize = new Vector2(800F, 600F);
            wnd.titleContent = new GUIContent("技能图编辑器");
            wnd.graphObject = _graphObject;
            wnd.openingSnapshot = Instantiate(_graphObject);
            wnd.openingSnapshot.name = _graphObject.name;
            wnd.openingSnapshot.hideFlags = HideFlags.HideAndDontSave;
            wnd.CreateElement();
        }

        private static MasterGraphWindow wnd;
        internal static bool IsEditing(GraphObject graph) => wnd != null && wnd.graphObject == graph;

        internal static void CloseIfEditing(GraphObject graph)
        {
            if (IsEditing(graph)) wnd.Close();
        }

        private GraphObject graphObject;
        private GraphObject openingSnapshot;
        private MasterGraphView graphView;
        private ProgressBar progressBar;
        private string DebugString = "";
        private VisualElement DebugBox;

        void Save()
        {
            graphView.Save();
        }

        void DebugPrint()
        {
            FormatDebugMode();
        }

        private void OnDestroy()
        {
            if (graphObject != null && graphView != null) Save();
            if (openingSnapshot != null) DestroyImmediate(openingSnapshot);
        }

        void Revert()
        {
            if (graphObject == null || openingSnapshot == null) return;
            var assetFlags = graphObject.hideFlags;
            EditorUtility.CopySerialized(openingSnapshot, graphObject);
            graphObject.hideFlags = assetFlags;
            graphObject.pendingSave = false;
            EditorUtility.SetDirty(graphObject);
            graphView.UnregisterCallback<KeyDownEvent>(KeyDown);
            graphView.Clear();
            rootVisualElement.Clear();
            graphView = null;
            CreateElement();
            AssetDatabase.SaveAssets();
            
        }


        private void Update()
        {
            if (graphObject == null) return;
            if (graphView.saving <= 0F && graphObject.pendingSave)
            {
                graphObject.pendingSave = false;
                graphView.Save();
            }

            if (graphView.saving > 0F)
            {
                graphView.saving -= 0.4F;
                progressBar.value = Mathf.Clamp(100F - graphView.saving, 0F, 100F);
                progressBar.visible = true;
                progressBar.title = "正在保存…";
            }
            else if (progressBar.visible) progressBar.visible = false;

           
        }

        void InitGraph()
        {
            graphObject.NodeIconSprites.Clear();
            Dictionary<int, Sprite> _spriteDic = new Dictionary<int, Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/SoftKitty/Shared/Sprites/GraphInterface.png"))
            {
                if (obj.name == "root")
                    _spriteDic.Add(0, (Sprite)obj);
                else if (obj.name == "node") 
                    _spriteDic.Add(1, (Sprite)obj);
                else if (obj.name == "logic")
                    _spriteDic.Add(2, (Sprite)obj);
                else if (obj.name == "math")
                    _spriteDic.Add(3, (Sprite)obj);
                else if (obj.name == "variable")
                    _spriteDic.Add(4, (Sprite)obj);
                else if (obj.name == "entity")
                    _spriteDic.Add(5, (Sprite)obj);
                else if (obj.name == "branch")
                    _spriteDic.Add(6, (Sprite)obj);
            }
            for (int i=0;i<=6;i++) {
                graphObject.NodeIconSprites.Add(_spriteDic[i]);
            }

            graphObject.RootClassNames.Clear();
            string _rootPath = "Assets/SoftKitty/Shared/Scripts/CustomClass/Root/";
            string[] guids = AssetDatabase.FindAssets("t:Script", new[] { _rootPath });
            foreach (string obj in guids)
            {
                graphObject.RootClassNames.Add(AssetDatabase.GUIDToAssetPath(obj).Replace(_rootPath, "").Replace("Root.cs", ""));
            }

            graphObject.ActionClassNames.Clear();
            _rootPath = "Assets/SoftKitty/Shared/Scripts/CustomClass/Action/";
            guids=  AssetDatabase.FindAssets("t:Script", new[] { _rootPath });
            foreach (string obj in guids)
            {
                graphObject.ActionClassNames.Add( AssetDatabase.GUIDToAssetPath(obj).Replace(_rootPath, "").Replace("Action.cs", ""));
            }

            graphObject.HandlerClassNames.Clear();
            _rootPath = "Assets/SoftKitty/Shared/Scripts/CustomClass/Handler/";
            guids = AssetDatabase.FindAssets("t:Script", new[] { _rootPath });
            foreach (string obj in guids)
            {
                graphObject.HandlerClassNames.Add(AssetDatabase.GUIDToAssetPath(obj).Replace(_rootPath, "").Replace("Handler.cs", ""));
            }
        }


        private string GetUid(string _uid)
        {
            if (_uid.Contains("#"))
            {
                string[] _args = _uid.Split("#");
                return _args[0];
            }
            else
            {
                return _uid;
            }
        }

        private void ConnectByData(string _uid, List<PortSetting> _outputSetting)
        {
            for (int i = 0; i < _outputSetting.Count; i++)
            {
                foreach (var code in _outputSetting[i].uid)
                {
                    string[] _args = code.Split("#");
                    string _id = _args[0];
                    int _to = int.Parse(_args[1]);
                    if (graphView.NodeDic.ContainsKey(_id) && graphView.NodeDic[_id].input.Count > _to)
                    {
                        graphView.AddElement(graphView.NodeDic[_uid].output[i].ConnectTo(graphView.NodeDic[_id].input[_to]));
                        if (!graphView.NodeDic[_id].baseData.inputPort[_to].uid.Contains(_uid + "#" + i)) graphView.NodeDic[_id].baseData.inputPort[_to].uid.Add(_uid + "#" + i);
                    }
                   
                }
            }
        }

        private void FormatDebugMode()
        {
            bool _debugMode = SGD_Settings.Instance.GraphDebugMode;
            GraphDebugTextRoot _newDebug = null;
          
            if (!string.IsNullOrEmpty(DebugString)){
                bool _invalid = false;
                try
                {
                    _newDebug = (GraphDebugTextRoot)JsonUtility.FromJson(DebugString.Replace("&lt&", "<").Replace("&rt&", ">"),typeof(GraphDebugTextRoot));
                }
                catch
                {
                    _invalid = true;
                }
                if(_invalid || _newDebug==null) EditorUtility.DisplayDialog("Invalid String", "Invalid String! When Debug Mode enabled, every graph evaluation will log a debug string to the Console. Please copy the string and paste it here.", "Confirm");
            }
            Dictionary<string, string> _debugInfo = new Dictionary<string, string>();
            if (_newDebug != null)
            {
                foreach (var obj in _newDebug.Data) {
                    _debugInfo.Add(obj.NodeUid, obj.DebugText);
                }
            }
            foreach (var obj in graphView.NodeDic.Values)
            {
                string _text = "";
                if (_debugInfo.ContainsKey(obj.baseData.Uid)) _text = _debugInfo[obj.baseData.Uid];
                obj.ToggleDebug(_debugMode, _text);
            }

        }

        private void CreateElement()
        {
            rootVisualElement.Clear();
            InitGraph();

            if (graphObject.RootSet == null || graphObject.RootSet.Uid != "Node0")
            {
                Log("Root node not found, creating a new one.");
                graphObject.RootSet = new Root(true);
            }

            if(graphView==null) graphView = new MasterGraphView() { name = "Combat Graph", viewDataKey = "CombatGraphView", graph = graphObject};
            graphView.saving = 0F;
            graphView.SetupZoom(0.1f, ContentZoomer.DefaultMaxScale, ContentZoomer.DefaultScaleStep, 1F);

            graphView.AddManipulator(new ContentDragger());
            graphView.AddManipulator(new SelectionDragger());
            graphView.AddManipulator(new RectangleSelector());
            graphView.AddManipulator(new ClickSelector());
            graphView.RegisterCallback<KeyDownEvent>(KeyDown);

            graphView.graphViewChanged = GraphViewChanged;

            rootVisualElement.Add(graphView);
            graphView.StretchToParentSize();

           
            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/ToolBox.uxml");
            VisualElement ui = uiAsset.Instantiate();
            ui.style.width = 700;
            ui.style.height = 35;
            ui.Q<MyUILibrary.SlideToggle>("AutoSaveToggle").value = graphObject.autoSave;
            ui.Q<MyUILibrary.SlideToggle>("AutoSaveToggle").RegisterValueChangedCallback(x=>
            {
                graphObject.autoSave = x.newValue;
            });
            ui.Q<MyUILibrary.SlideToggle>("debugToggle").value = SGD_Settings.Instance.GraphDebugMode;
            ui.Q<MyUILibrary.SlideToggle>("debugToggle").RegisterValueChangedCallback(x =>
            {
                 SGD_Settings.Instance.GraphDebugMode = x.newValue;
                 DebugBox.style.display = SGD_Settings.Instance.GraphDebugMode ? DisplayStyle.Flex : DisplayStyle.None;
                 FormatDebugMode();
            });
            ui.Q<Button>("SaveButton").clicked += Save;
            ui.Q<Button>("DiscardButton").clicked += Revert;
            progressBar =ui.Q<ProgressBar>("Progress");
            progressBar.value = 0F;
            progressBar.visible = false;
            rootVisualElement.Add(ui);


            VisualTreeAsset uiAsset2 = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/DebugBox.uxml");
            VisualElement ui2 = uiAsset2.Instantiate();
            DebugBox = ui2;
            ui2.style.width = 700;
            ui2.style.height = 300;
            ui2.Q<TextField>("debugText").RegisterValueChangedCallback(x =>
            {
                DebugString = x.newValue;
            });
            ui2.Q<Button>("debugButton").clicked += DebugPrint;
            rootVisualElement.Add(ui2);
            DebugBox.style.display = SGD_Settings.Instance.GraphDebugMode? DisplayStyle.Flex : DisplayStyle.None;


            GridBackground gridBackground = new GridBackground() { name = "Grid" };
            graphView.Add(gridBackground);
            gridBackground.SendToBack();
            graphView.NodeDic.Clear();
            graphView.Groups.Clear();

           RootNode _combatSetNode = new RootNode(graphObject.RootSet) { name = graphObject.RootSet.Name };
            graphView.AddElement(_combatSetNode);
            _combatSetNode.InitializeNode(graphObject.RootSet, graphObject);
            _combatSetNode.SetPosition(new Rect(graphObject.RootSet.Position.x, graphObject.RootSet.Position.y, 0F, 0F));
             graphView.NodeDic.Add(graphObject.RootSet.Uid, _combatSetNode);

            foreach (var obj in graphObject.RootSet.Notes)
            {
                CustomStickyNote _noteNode = new CustomStickyNote(obj) { name = obj.Name };
                graphView.AddElement(_noteNode);
                _noteNode.InitializeNode(obj);
                _noteNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, obj.Size.x, obj.Size.y));
            }

            foreach (var obj in  graphObject.RootSet.Actions) 
            {
                ActionNode _actionNode = new ActionNode(obj) { name = obj.Name };
                graphView.AddElement(_actionNode);
                _actionNode.InitializeNode(obj, graphObject);
                _actionNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                 graphView.NodeDic.Add(obj.Uid, _actionNode);
            }

            foreach (var obj in graphObject.RootSet.Triggers)
            {
                TriggerNode _triggerNode = new TriggerNode(obj) { name = obj.Name };
                graphView.AddElement(_triggerNode);
                _triggerNode.InitializeNode(obj, graphObject);
                _triggerNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                 graphView.NodeDic.Add(obj.Uid, _triggerNode);
            }

            foreach (var obj in graphObject.RootSet.ConditionGroups)
            {
                ConditionGroupNode _groupNode = new ConditionGroupNode(obj) { name = obj.Name };
                graphView.AddElement(_groupNode);
                _groupNode.InitializeNode(obj, graphObject);
                _groupNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                 graphView.NodeDic.Add(obj.Uid, _groupNode);
            }

            foreach (var obj in graphObject.RootSet.Maths)
            {
                MathNode _mathNode = new MathNode(obj) { name = obj.Name };
                graphView.AddElement(_mathNode);
                _mathNode.InitializeNode(obj, graphObject);
                _mathNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                graphView.NodeDic.Add(obj.Uid, _mathNode);
            }

            foreach (var obj in graphObject.RootSet.Branchs)
            {
                BranchNode _branchNode = new BranchNode(obj) { name = obj.Name };
                graphView.AddElement(_branchNode);
                _branchNode.InitializeNode(obj, graphObject);
                _branchNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                graphView.NodeDic.Add(obj.Uid, _branchNode);
            }

            foreach (var obj in graphObject.RootSet.Variables)
            {
                VariableNode _variableNode = new VariableNode(obj) { name = obj.Name };
                graphView.AddElement(_variableNode);
                _variableNode.InitializeNode(obj, graphObject);
                _variableNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                graphView.NodeDic.Add(obj.Uid, _variableNode);
            }

            foreach (var obj in graphObject.RootSet.Entities)
            {
                EntityNode _entityNode = new EntityNode(obj) { name = obj.Name };
                graphView.AddElement(_entityNode);
                _entityNode.InitializeNode(obj, graphObject);
                _entityNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                graphView.NodeDic.Add(obj.Uid, _entityNode);
            }

            foreach (var obj in graphObject.RootSet.Handlers)
            {
                HandlerNode _handlerNode = new HandlerNode(obj) { name = obj.Name };
                graphView.AddElement(_handlerNode);
                _handlerNode.InitializeNode(obj, graphObject);
                _handlerNode.SetPosition(new Rect(obj.Position.x, obj.Position.y, 0F, 0F));
                 graphView.NodeDic.Add(obj.Uid, _handlerNode);
            }

            foreach (var obj in graphObject.RootSet.Groups)
            {
                var newGroup = new CustomGroupEditor(obj);
                graphView.AddElement(newGroup);
                newGroup.InitializeNode(obj, graphView);
                newGroup.SetPosition(obj.Position);
                foreach (var _child in obj.Children)
                {
                    newGroup.AddElement(graphView.NodeDic[_child]);
                }
                graphView.Groups.Add(newGroup);
            }


            foreach (var obj in graphObject.RootSet.Variables) ConnectByData(obj.Uid, obj.outputPort);
            foreach (var obj in graphObject.RootSet.Maths) ConnectByData(obj.Uid, obj.outputPort);
            foreach (var obj in graphObject.RootSet.Entities) ConnectByData(obj.Uid, obj.outputPort);
            foreach (var obj in graphObject.RootSet.Handlers) ConnectByData(obj.Uid, obj.outputPort);
            foreach (var obj in graphObject.RootSet.ConditionGroups) ConnectByData(obj.Uid, obj.outputPort);
            foreach (var obj in graphObject.RootSet.Branchs) ConnectByData(obj.Uid, obj.outputPort);
            foreach (var obj in graphObject.RootSet.Triggers) ConnectByData(obj.Uid, obj.outputPort);


            ConnectByData(graphObject.RootSet.Uid, graphObject.RootSet.outputPort);

            FormatDebugMode();
            rootVisualElement.MarkDirtyRepaint();

        }

        private void Log(string _text)
        {
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log(_text);
        }

        private GraphViewChange GraphViewChanged(GraphViewChange change)
        {
            if (graphObject == null) return change;
            bool _save = false;
            if (change.edgesToCreate != null)
            {
                List<Edge> validEdges = new List<Edge>();
                foreach (Edge edge in change.edgesToCreate)
                {
                    string _inputUid = ((NodeBaseEditor)edge.input.node).baseData.Uid;
                    string _outputUid = ((NodeBaseEditor)edge.output.node).baseData.Uid;
                    int _inputIndex = ((NodeBaseEditor)edge.input.node).input.IndexOf(edge.input);
                    int _outputIndex = ((NodeBaseEditor)edge.output.node).output.IndexOf(edge.output);
                    if (_inputUid != _outputUid
                        && graphView.NodeDic[_inputUid].baseData.inputPort[_inputIndex].acceptedTypes.Contains(graphView.NodeDic[_outputUid].baseData.outputPort[_outputIndex].Type)
                        && graphView.NodeDic[_outputUid].baseData.outputPort[_outputIndex].acceptedTypes.Contains(graphView.NodeDic[_inputUid].baseData.inputPort[_inputIndex].Type)

                        )
                    {
                        graphView.NodeDic[_inputUid].baseData.inputPort[_inputIndex].uid.Add(_outputUid+"#"+ _outputIndex);
                        graphView.NodeDic[_outputUid].baseData.outputPort[_outputIndex].uid.Add(_inputUid + "#" + _inputIndex);
                        validEdges.Add(edge);
                        Log("Edge Create:  " + _inputUid + "(" + _inputIndex + ") > " + _outputUid + "(" + _outputIndex + ")");
                    }
                    else
                    {
                        Log("Sorry, you can not connect "+ graphView.NodeDic[_inputUid].baseData.inputPort[_inputIndex].Type+" to "+ graphView.NodeDic[_outputUid].baseData.outputPort[_outputIndex].Type);
                    }

                }
                change.edgesToCreate.Clear();
                change.edgesToCreate.AddRange(validEdges);
                _save = true;
            }

            if (change.elementsToRemove != null)
            {
                foreach (GraphElement e in change.elementsToRemove)
                {
                    if (e.GetType() == typeof(Edge))
                    {
                        Edge _edge = (Edge)e;
                        string _inputUid = ((NodeBaseEditor)_edge.input.node).baseData.Uid;
                        string _outputUid = ((NodeBaseEditor)_edge.output.node).baseData.Uid;
                        int _inputIndex = ((NodeBaseEditor)_edge.input.node).input.IndexOf(_edge.input);
                        int _outputIndex = ((NodeBaseEditor)_edge.output.node).output.IndexOf(_edge.output);
                        if (graphView.NodeDic.ContainsKey(_inputUid) &&
                            _inputIndex>=0 &&
                            _inputIndex< graphView.NodeDic[_inputUid].baseData.inputPort.Count &&
                            graphView.NodeDic[_inputUid].baseData.inputPort[_inputIndex].uid.Contains(_outputUid + "#" + _outputIndex))
                            graphView.NodeDic[_inputUid].baseData.inputPort[_inputIndex].uid.Remove(_outputUid + "#" + _outputIndex);
                        if (graphView.NodeDic.ContainsKey(_outputUid) &&
                            _outputIndex>=0 &&
                            _outputIndex< graphView.NodeDic[_outputUid].baseData.outputPort.Count &&
                            graphView.NodeDic[_outputUid].baseData.outputPort[_outputIndex].uid.Contains(_inputUid + "#" + _inputIndex))
                            graphView.NodeDic[_outputUid].baseData.outputPort[_outputIndex].uid.Remove(_inputUid + "#" + _inputIndex);
                        _save = true;
                        Log("Edge Remove:  " + _inputUid + "(" + _inputIndex + ") > " + _outputUid + "(" + _outputIndex + ")");
                    }
                    else if (e.GetType().IsSubclassOf(typeof(NodeBaseEditor)))
                    {
                        //if (e.GetType() == typeof(HandlerNode) || e.GetType() == typeof(ActionNode) || e.GetType() == typeof(VariableNode)) {
                            for (int u = 0; u < ((NodeBaseEditor)e).input.Count; u++) ((NodeBaseEditor)e).DisconnectInput(u);
                            for (int u = 0; u < ((NodeBaseEditor)e).output.Count; u++) ((NodeBaseEditor)e).DisconnectOutput(u);
                        //}
                        RemoveNode((NodeBaseEditor)e);
                        Log("Node Remove:  " + e.GetType());
                        _save = true;
                    }
                    else if (e.GetType() == typeof(CustomStickyNote))
                    {
                        CustomStickyNote _note = (CustomStickyNote)e;
                        bool _found = false;
                        for (int i = graphObject.RootSet.Notes.Count-1; i >=0; i--)
                        {
                            if (graphObject.RootSet.Notes[i].Uid== _note.data.Uid && !_found) {
                                graphObject.RootSet.Notes.RemoveAt(i);
                            }
                        }
                    }
                }
      
            }

            if (change.movedElements != null)
            {
                foreach (GraphElement e in change.movedElements)
                {
                    Log("movedElements: " + e.GetType());
                    if (e.GetType().IsSubclassOf(typeof(NodeBaseEditor)))
                    {
                        ((NodeBaseEditor)e).UpdatePosition(e.GetPosition());
                    }
                    else if (e.GetType() == typeof(CustomStickyNote))
                    {
                        ((CustomStickyNote)e).UpdatePosition(e.GetPosition());
                    }
                    else if (e.GetType() == typeof(CustomGroupEditor))
                    {
                        ((CustomGroupEditor)e).UpdatePosition(e.GetPosition());
                    }
                }
            }

          


           if (_save && graphObject.autoSave) graphObject.pendingSave=true;

            return change;
        }



        public void RemoveNode(NodeBaseEditor _node)
        {
           
            string _uid = _node.baseData.Uid;
            Log("Node Remove (uid:" + _uid+")");
            if ( graphView.NodeDic.ContainsKey(_uid))  graphView.NodeDic.Remove(_uid);

            foreach (var obj in graphObject.RootSet.inputPort)
            {
                if (obj.uid.Contains(_uid)) obj.uid.Remove(_uid);
            }
            foreach (var obj in graphObject.RootSet.outputPort)
            {
                if (obj.uid.Contains(_uid)) obj.uid.Remove(_uid);
            }

            for (int i= graphObject.RootSet.Actions.Count-1;i>=0; i--) {
                if (graphObject.RootSet.Actions[i].Uid == _uid)
                {
                    graphObject.RootSet.Actions.RemoveAt(i);
                }
                else
                {
                    foreach (var obj in graphObject.RootSet.Actions[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u])==_uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Actions[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.Triggers.Count-1; i >=0 ; i--)
            {
                if (graphObject.RootSet.Triggers[i].Uid == _uid)
                    graphObject.RootSet.Triggers.RemoveAt(i);
                else {
                    foreach (var obj in graphObject.RootSet.Triggers[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Triggers[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.ConditionGroups.Count-1; i >=0 ; i--)
            {
                if (graphObject.RootSet.ConditionGroups[i].Uid == _uid)
                    graphObject.RootSet.ConditionGroups.RemoveAt(i);
                else
                {
                    foreach (var obj in graphObject.RootSet.ConditionGroups[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.ConditionGroups[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.Handlers.Count-1; i >=0 ; i--)
            {
                if (graphObject.RootSet.Handlers[i].Uid == _uid)
                    graphObject.RootSet.Handlers.RemoveAt(i);
                else
                {
                    foreach (var obj in graphObject.RootSet.Handlers[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Handlers[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.Variables.Count-1; i >=0 ; i--)
            {
                if (graphObject.RootSet.Variables[i].Uid == _uid)
                    graphObject.RootSet.Variables.RemoveAt(i);
                else
                {
                    foreach (var obj in graphObject.RootSet.Variables[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Variables[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.Maths.Count-1; i >=0 ; i--)
            {
                if (graphObject.RootSet.Maths[i].Uid == _uid)
                    graphObject.RootSet.Maths.RemoveAt(i);
                else
                {
                    foreach (var obj in graphObject.RootSet.Maths[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Maths[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.Branchs.Count - 1; i >= 0; i--)
            {
                if (graphObject.RootSet.Branchs[i].Uid == _uid)
                    graphObject.RootSet.Branchs.RemoveAt(i);
                else
                {
                    foreach (var obj in graphObject.RootSet.Branchs[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Branchs[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }

            for (int i = graphObject.RootSet.Entities.Count-1; i >=0 ; i--)
            {
                if (graphObject.RootSet.Entities[i].Uid == _uid)
                    graphObject.RootSet.Entities.RemoveAt(i);
                else
                {
                    foreach (var obj in graphObject.RootSet.Entities[i].inputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                    foreach (var obj in graphObject.RootSet.Entities[i].outputPort)
                    {
                        for (int u = obj.uid.Count - 1; u >= 0; u--)
                        {
                            if (GetUid(obj.uid[u]) == _uid)
                                obj.uid.RemoveAt(u);
                        }
                    }
                }
            }
        }

        private void KeyDown(KeyDownEvent evt)
        {

        }

    }
}
