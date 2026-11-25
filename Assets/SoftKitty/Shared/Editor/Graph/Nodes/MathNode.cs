using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;

namespace SoftKitty
{
    public class MathNode : NodeBaseEditor
    {
        private MathClass m_data;
        public MathClass data => m_data;

        public MathNode(MathClass _math)
        {
            m_data = _math;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[3]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.1F, 0.1F, 0.1F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/MathNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 45;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(Enum.GetNames(typeof(MathType)));
            baseSettings.Q<DropdownField>("Type").index = (int)m_data.Type;
            baseSettings.Q<DropdownField>("Type").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type = (MathType)((DropdownField)x.target).index;
                FormatType();
                RefreshExpandedState();
            });
            FormatType();
            renameButton.style.display = DisplayStyle.None;
            extensionContainer.Add(baseSettings);
            RefreshExpandedState();
        }

        private void FormatType()
        {
            title = GraphEditorText.TypedTitle("Math", m_data.Type.ToString());
            input[0].portName = GraphEditorText.Localize(m_data.NameOfValueA());
            input[1].style.display = (m_data.ValueB_Visible() ? DisplayStyle.Flex : DisplayStyle.None);
            input[1].portName = GraphEditorText.Localize(m_data.NameOfValueB());
            input[2].style.display = (m_data.ValueC_Visible() ? DisplayStyle.Flex : DisplayStyle.None);
            input[2].portName = GraphEditorText.Localize(m_data.NameOfValueC());
            if (input[1].style.display == DisplayStyle.None)
            {
                DisconnectInput(1);
            }
            if (input[2].style.display == DisplayStyle.None)
            {
                DisconnectInput(2);
            }
        }

    }
}
