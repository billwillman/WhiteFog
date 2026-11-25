using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace SoftKitty
{
    public class EntityNode : NodeBaseEditor
    {
        private EntityClass m_data;
        public EntityClass data => m_data;
        private TextField uidField;


        public EntityNode(EntityClass _entity)
        {
            m_data = _entity;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[5]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.1F, 0.3F, 0.4F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/EntityNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 100;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(Enum.GetNames(typeof(EntityType)));
            baseSettings.Q<DropdownField>("Type").index = (int)m_data.Type;
            baseSettings.Q<DropdownField>("Type").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type = (EntityType)((DropdownField)x.target).index;
                FormatType();
                RefreshExpandedState();
            });

            uidField= baseSettings.Q<TextField>("Uid");
            uidField.value = m_data.EntityUid;

            FormatType();
            extensionContainer.Add(baseSettings);
            RefreshExpandedState();
        }

        private void FormatType()
        {
            if (m_data.Type == EntityType.Specified)
            {
                baseSettings.style.height = 80;
                uidField.style.display = DisplayStyle.Flex;
            }
            else
            {
                baseSettings.style.height = 50;
                uidField.style.display = DisplayStyle.None;
            }
            if (title.Contains("Entity (Player)")|| title.Contains("Entity (Dealer)") || title.Contains("Entity (Target)") || title.Contains("Entity (Specified)"))
            {
                title = GraphEditorText.TypedTitle("Entity", m_data.Type.ToString());
            }
        }

    }
}
