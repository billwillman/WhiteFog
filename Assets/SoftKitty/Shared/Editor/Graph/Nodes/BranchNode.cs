using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;

namespace SoftKitty
{
    public class BranchNode : NodeBaseEditor
    {
        private Branch m_data;
        public Branch data => m_data;

        public BranchNode(Branch _branch)
        {
            m_data = _branch;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[6]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.1F, 0.5F, 0.45F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/BranchNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 45;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(Enum.GetNames(typeof(BranchType)));
            baseSettings.Q<DropdownField>("Type").index = (int)m_data.Type;
            baseSettings.Q<DropdownField>("Type").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type = (BranchType)((DropdownField)x.target).index;
                m_data.FormatPort();
                FormatType(true);
                RefreshExpandedState();
            });
            FormatType(false);
            renameButton.style.display = DisplayStyle.None;
            extensionContainer.Add(baseSettings);
            RefreshExpandedState();
        }

        private void DisconnectAll()
        {

        }

        private void FormatType(bool _disConnect)
        {
            title = GraphEditorText.TypedTitle("Branch", m_data.Type.ToString());
            for (int i = 2; i >= 0; i--)
            {
                if(_disConnect) DisconnectInput(i);
                if (inputContainer.Contains(input[i])) inputContainer.Remove(input[i]);
                input.RemoveAt(i);
            }
            for (int i = 1; i >= 0; i--)
            {
                if (_disConnect) DisconnectOutput(i);
                if (outputContainer.Contains(output[i])) outputContainer.Remove(output[i]);
                output.RemoveAt(i);
            }
            
            InitPort();
            input[1].portColor = portColors[m_data.inputPort[1].Type];
            input[2].portColor = portColors[m_data.inputPort[2].Type];
            input[2].style.display = (m_data.Type!= BranchType.Action ? DisplayStyle.Flex : DisplayStyle.None);
            output[0].portColor = portColors[m_data.outputPort[0].Type];
            output[1].portColor = portColors[m_data.outputPort[0].Type];
            output[1].style.display = (m_data.Type == BranchType.Action ? DisplayStyle.Flex : DisplayStyle.None);

        }



    }
}
