using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;


namespace SoftKitty
{
    public class NodeBaseEditor : Node
    {
        protected GraphObject mainDataObject;
        private NodeBase m_baseData;
        public NodeBase baseData => m_baseData;
        public List<Port> output = new List<Port>();
        public List<Port> input = new List<Port>();
        protected VisualElement baseSettings;
        protected VisualElement titleIcon;
        protected TextField renameField;
        protected Label titleLable;
        protected VisualElement minArrow;
        protected VisualElement renameButton;
        protected VisualElement extraSettings;
        protected VisualElement debugPanel;
        protected Label debugLabel;


        protected Dictionary<string, Color> portColors = new Dictionary<string, Color>()
        {
             { "ActionIn",new Color(0.6F, 0.2F, 0.2F, 1F) } ,
             { "TriggerOut",new Color(0.6F, 0.55F, 0.2F, 1F) } ,
             { "TriggerIn",new Color(0.2F, 0.6F, 0.2F, 1F) } ,
             { "ConditionIn",new Color(0.2F, 0.6F, 0.2F, 1F) } ,
             { "ConditionOut",new Color(0.2F, 0.6F, 0.2F, 1F) } ,
             { "RootIn",new Color(0.6F, 0.55F, 0.2F, 1F) } ,
             { "RootOut",new Color(0.6F, 0.2F, 0.2F, 1F) } ,
             { "MathIn",new Color(0.6F, 0.6F, 0.6F, 1F) } ,
             { "Variable",new Color(0.6F, 0.6F, 0.6F, 1F) } ,
             { "CustomVariable",new Color(0.6F, 0.6F, 0.6F, 1F) },
             { "EntityIn",new Color(0.2F, 0.5F, 0.6F, 1F) },
             { "EntityOut",new Color(0.2F, 0.5F, 0.6F, 1F) },
            { "BranchIn",new Color(0.2F, 0.6F, 0.2F, 1F) } ,
            { "BranchInAction",new Color(0.6F, 0.2F, 0.2F, 1F) } ,
            { "BranchOutAction",new Color(0.6F, 0.2F, 0.2F, 1F) } ,
            { "BranchInEntity",new Color(0.2F, 0.5F, 0.6F, 1F) },
            { "BranchInValue",new Color(0.6F, 0.6F, 0.6F, 1F) } ,
        };

        public void InitializeNode(NodeBase _data, GraphObject _mainDataObject)
        {
            titleContainer.tooltip = "#"+ _data.Uid+"#";
            mainDataObject = _mainDataObject;
            m_baseData = _data;
            UseDefaultStyling();
            title = GraphEditorText.Localize(m_baseData.Name);
            titleContainer.RemoveAt(titleContainer.childCount-1);
            titleContainer.ElementAt(0).style.left = 8;
            titleContainer.ElementAt(0).style.right = 20;
            titleContainer.ElementAt(0).style.fontSize = 14;
            titleLable = (Label)titleContainer.ElementAt(0);
            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/NodeTitle.uxml");
            VisualElement _titleBox = uiAsset.Instantiate();
            _titleBox.style.width = 70;
            _titleBox.style.height = 30;
            _titleBox.Q<Button>("MinButton").clicked += SwitchMiniState;
            minArrow = _titleBox.Q<VisualElement>("MinButton");
            renameButton = _titleBox.Q<VisualElement>("RenameButton");
            _titleBox.Q<Button>("RenameButton").clicked += Rename;
            _titleBox.Q<Button>("RenameConfirm").clicked += RenameConfirm;
            renameField = _titleBox.Q<TextField>("RenameField");
            renameField.style.display = DisplayStyle.None;

            titleContainer.Add(_titleBox);
            titleIcon = new VisualElement();
            titleIcon.style.width = 18;
            titleIcon.style.height = 18;
            titleIcon.style.backgroundColor = new StyleColor(new Color(0F,0F,0F,0F));
            titleIcon.style.alignSelf = Align.Center;
            titleIcon.style.left = 8;
            titleIcon.style.right = 8;

            VisualTreeAsset uiAsset2 = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/DebugPanel.uxml");
            VisualElement _debugBox = uiAsset2.Instantiate();
            _debugBox.style.height = 30;
            debugPanel = _debugBox;
            debugLabel = _debugBox.Q<Label>("Item");
            debugLabel.text = "[调试]";
            extensionContainer.Add(_debugBox);
            debugPanel.style.display = DisplayStyle.None;

            InitPort();

            Init();
            titleContainer.Insert(0, titleIcon);
            ApplyDisplayState();
            MarkDirtyRepaint();
        }

        protected static VisualElement InstantiateSettings(VisualTreeAsset template)
        {
            var element = template.Instantiate();
            element.Query<DropdownField>().ForEach(field =>
            {
                if (field.name != "Attribute") GraphEditorText.FormatDropdown(field);
            });
            return element;
        }

        protected void InitPort()
        {
            for (int i = 0; i < m_baseData.inputPort.Count; i++)
            {
                if (m_baseData.inputPort[i].createByDefault)
                {
                    Port _newPort = InstantiatePort(Orientation.Horizontal, Direction.Input, m_baseData.inputPort[i].allowMutipleConnection ? Port.Capacity.Multi : Port.Capacity.Single, typeof(NodeBase));
                    _newPort.portName = GraphEditorText.Localize(m_baseData.inputPort[i].Name);
                    _newPort.portColor = portColors[m_baseData.inputPort[i].Type];
                    input.Add(_newPort);
                    inputContainer.Add(_newPort);
                }
            }

            for (int i = 0; i < m_baseData.outputPort.Count; i++)
            {
                if (m_baseData.outputPort[i].createByDefault)
                {
                    Port _newPort = InstantiatePort(Orientation.Horizontal, Direction.Output, m_baseData.outputPort[i].allowMutipleConnection ? Port.Capacity.Multi : Port.Capacity.Single, typeof(NodeBase));
                    _newPort.portName = GraphEditorText.Localize(m_baseData.outputPort[i].Name);
                    _newPort.portColor = portColors[m_baseData.outputPort[i].Type];
                    output.Add(_newPort);
                    outputContainer.Add(_newPort);
                }
            }
        }

        public void DisconnectInput(int _index)
        {
            if (input.Count <= _index) return;
            foreach (var _edge in input[_index].connections)
            {
                string _inputUid = ((NodeBaseEditor)_edge.input.node).baseData.Uid;
                string _outputUid = ((NodeBaseEditor)_edge.output.node).baseData.Uid;
                int _inputIndex = ((NodeBaseEditor)_edge.input.node).input.IndexOf(_edge.input);
                int _outputIndex = ((NodeBaseEditor)_edge.output.node).output.IndexOf(_edge.output);
                ((NodeBaseEditor)_edge.input.node).baseData.inputPort[_inputIndex].uid.Remove(_outputUid + "#" + _outputIndex);
                ((NodeBaseEditor)_edge.output.node).baseData.outputPort[_outputIndex].uid.Remove(_inputUid + "#" + _inputIndex);
                _edge.output.Disconnect(_edge);
                _edge.parent.Remove(_edge);
            }
            input[_index].DisconnectAll();
            m_baseData.inputPort[_index].uid.Clear();
        }

        public void DisconnectOutput(int _index)
        {
            if (output.Count <= _index) return;
            foreach (var _edge in output[_index].connections)
            {
                string _inputUid = ((NodeBaseEditor)_edge.input.node).baseData.Uid;
                string _outputUid = ((NodeBaseEditor)_edge.output.node).baseData.Uid;
                int _inputIndex = ((NodeBaseEditor)_edge.input.node).input.IndexOf(_edge.input);
                int _outputIndex = ((NodeBaseEditor)_edge.output.node).output.IndexOf(_edge.output);
                ((NodeBaseEditor)_edge.input.node).baseData.inputPort[_inputIndex].uid.Remove(_outputUid + "#" + _outputIndex);
                ((NodeBaseEditor)_edge.output.node).baseData.outputPort[_outputIndex].uid.Remove(_inputUid + "#" + _inputIndex);
                _edge.input.Disconnect(_edge);
                _edge.parent.Remove(_edge);
            }
            output[_index].DisconnectAll();
            m_baseData.outputPort[_index].uid.Clear();
        }

        void Rename()
        {
            if (renameField.style.display == DisplayStyle.None)
            {
                renameField.value = m_baseData.Name;
                renameField.style.display = DisplayStyle.Flex;
            }
            else
            {
                RenameConfirm();
            }
        }

        void RenameConfirm() {
            renameField.style.display = DisplayStyle.None;
            m_baseData.Name = renameField.value;
            title = GraphEditorText.Localize(m_baseData.Name);
        }

        void SwitchMiniState()
        {
            m_baseData.minimized = !m_baseData.minimized;
            ApplyDisplayState();
            var rect = GetPosition();
            if (m_baseData.minimized) 
                rect.y -= 1;
            else
                rect.y += 1;
            SetPosition(rect);
        }


        void ApplyDisplayState()
        {
            if (baseSettings != null) baseSettings.style.display = m_baseData.minimized ? DisplayStyle.None : DisplayStyle.Flex;
            if (extraSettings != null) extraSettings.style.display = m_baseData.minimized ? DisplayStyle.None : DisplayStyle.Flex;
            VisualElementRotate(ref minArrow, m_baseData.minimized ? 0F : 180F);
        }

        protected void VisualElementRotate(ref VisualElement _ve, float _angle)
        {
            StyleRotate _rot = _ve.resolvedStyle.rotate;
            Rotate _rotValue = _rot.value;
            _rotValue.angle = _angle;
            _rot.value = _rotValue;
            _ve.style.rotate = _rot;
        }

        public void ToggleDebug(bool _visible, string _text="")
        {
            debugPanel.style.display = _visible ? DisplayStyle.Flex:DisplayStyle.None;
            debugLabel.text = "[调试]" + _text;
        }


        public virtual void Init()
        {
           
        }

        public void UpdatePosition(Rect _pos)
        {
            m_baseData.Position = new Vector2(_pos.x,_pos.y);
        }

        public void Move(Vector2 _pos)
        {
            var rect = GetPosition();
            rect.position = _pos;
            SetPosition(rect);
        }

    }
}
