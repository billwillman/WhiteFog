using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;

namespace SoftKitty
{
    public class ConditionGroupNode : NodeBaseEditor
    {
        private ConditionGroup m_data;
        public ConditionGroup data => m_data;
        

        public ConditionGroupNode(ConditionGroup _conditionGroup)
        {
            m_data = _conditionGroup;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[2]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.1F, 0.2F, 0.4F, 1F));
            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/ConditionGroupNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 45;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(Enum.GetNames(typeof(LogicType)));
            baseSettings.Q<DropdownField>("Type").index = (int)m_data.Type;
            baseSettings.Q<DropdownField>("Type").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type = (LogicType)((DropdownField)x.target).index;
                RefreshExpandedState();
            });
            extensionContainer.Add(baseSettings);
            RefreshExpandedState();
        }


    }
}
