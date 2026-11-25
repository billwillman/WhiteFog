using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;


namespace SoftKitty
{
    public class RootNode : CustomNodeEditor
    {
        private Root m_data;
        public Root data => m_data;

        public RootNode(Root _root)
        {
            m_data = _root;
        }

        

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[0]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.5F, 0.5F, 0.5F, 1F));
            capabilities -= Capabilities.Deletable;

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/RootNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 50;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(mainDataObject.RootClassNames);
            baseSettings.Q<DropdownField>("Type").index = mainDataObject.RootClassNames.IndexOf(m_data.ClassName);
            baseSettings.Q<DropdownField>("Type").value = m_data.ClassName;
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.ClassName = x.newValue;
                DeleteAllCustomPort(m_data, 1);
                m_data.ResetInputPortSetting();
                LoadClassInterface(m_data, "Root", 1);
            });
            extensionContainer.Add(baseSettings);

            m_data.ResetInputPortSetting();
            LoadClassInterface(m_data, "Root",1);
            RefreshExpandedState();
        }

    }
}
