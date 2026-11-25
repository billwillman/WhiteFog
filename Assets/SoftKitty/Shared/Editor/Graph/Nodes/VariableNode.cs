using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace SoftKitty
{
    public class VariableNode : NodeBaseEditor
    {
        private VariableClass m_data;
        public VariableClass data => m_data;
        private DropdownField attributeField;
        private TextField uidField;
        private FloatField valueField;
        private FloatField minField;
        private FloatField maxField;
        public Port EntityPort;

        public VariableNode(VariableClass _trigger)
        {
            m_data = _trigger;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[4]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.1F, 0.1F, 0.1F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/VariableNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 100;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(Enum.GetNames(typeof(VariableType)));
            baseSettings.Q<DropdownField>("Type").index = (int)m_data.Type;

            EntityPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(NodeBase));
            EntityPort.portName = GraphEditorText.Localize(m_data.inputPort[0].Name);
            EntityPort.portColor = portColors[m_data.inputPort[0].Type];
            input.Add(EntityPort);
            EntityPort.style.display = DisplayStyle.None;
            baseSettings.Q<VisualElement>("Root").Add(EntityPort);

            baseSettings.Q<DropdownField>("Type").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type = (VariableType)((DropdownField)x.target).index;
                EntityPort.style.display = m_data.Type == VariableType.Attribute ? DisplayStyle.Flex : DisplayStyle.None;
                FormatType();
                RefreshExpandedState();
            });
            EntityPort.style.display = m_data.Type== VariableType.Attribute ? DisplayStyle.Flex : DisplayStyle.None;


            var attributes = GameManager.AttributeData;
            List<string> _attList = new List<string>(attributes != null ? attributes.AttributesUidArray : Array.Empty<string>());
            attributeField = baseSettings.Q<DropdownField>("Attribute");
            attributeField.choices.Clear();
            attributeField.choices.AddRange(_attList);
            attributeField.index = _attList.IndexOf(m_data.AttributeKey);
            attributeField.RegisterValueChangedCallback(x =>
            {
                if (x.target != attributeField) return;
                int index = attributeField.index;
                if (index >= 0 && index < _attList.Count) m_data.AttributeKey = _attList[index];
            });
            attributeField.value = m_data.AttributeKey;


            valueField = baseSettings.Q<FloatField>("Value");
            valueField.value = m_data.Constant;
            valueField.RegisterValueChangedCallback(x =>
            {
                m_data.Constant = x.newValue;
            });

            uidField = baseSettings.Q<TextField>("Uid");
            uidField.value = m_data.VariableUid;
            uidField.RegisterValueChangedCallback(x =>
            {
                m_data.VariableUid = x.newValue;
            });

            minField = baseSettings.Q<FloatField>("Min");
            minField.value = m_data.Constant;
            minField.RegisterValueChangedCallback(x =>
            {
                m_data.Constant = x.newValue;
            });

            maxField = baseSettings.Q<FloatField>("Max");
            maxField.value = m_data.Max;
            maxField.RegisterValueChangedCallback(x =>
            {
                m_data.Max = x.newValue;
            });

            FormatType();
            renameButton.style.display = DisplayStyle.None;
            extensionContainer.Add(baseSettings);
            RefreshExpandedState();
        }

        private void FormatType()
        {
            switch (m_data.Type) {
                case VariableType.Constant:
                    baseSettings.style.height = 80;
                    attributeField.style.display = DisplayStyle.None;
                    uidField.style.display = DisplayStyle.None;
                    valueField.style.display = DisplayStyle.Flex;
                    minField.style.display = DisplayStyle.None;
                    maxField.style.display = DisplayStyle.None;
                    break;
                case VariableType.Attribute:
                    baseSettings.style.height = 110;
                    attributeField.style.display = DisplayStyle.Flex;
                    uidField.style.display = DisplayStyle.None;
                    valueField.style.display = DisplayStyle.None;
                    minField.style.display = DisplayStyle.None;
                    maxField.style.display = DisplayStyle.None;
                    break;
                case VariableType.DynamicVariable:
                    baseSettings.style.height = 110;
                    attributeField.style.display = DisplayStyle.None;
                    uidField.style.display = DisplayStyle.Flex;
                    valueField.style.display = DisplayStyle.Flex;
                    minField.style.display = DisplayStyle.None;
                    maxField.style.display = DisplayStyle.None;
                    break;
                case VariableType.Random:
                    baseSettings.style.height = 110;
                    attributeField.style.display = DisplayStyle.None;
                    uidField.style.display = DisplayStyle.None;
                    valueField.style.display = DisplayStyle.None;
                    minField.style.display = DisplayStyle.Flex;
                    maxField.style.display = DisplayStyle.Flex;
                    break;
            }
            title = GraphEditorText.TypedTitle("Variable", m_data.Type.ToString());
        }

    }
}
