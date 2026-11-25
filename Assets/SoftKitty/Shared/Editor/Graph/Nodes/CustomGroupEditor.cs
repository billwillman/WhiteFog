using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace SoftKitty
{
    public class CustomGroupEditor : Group
    {
        private CustomGroup m_data;
        private MasterGraphView mView;
        public CustomGroup data => m_data;
        public CustomGroupEditor(CustomGroup _group)
        {
            m_data = _group;
            title = _group.Title;
        }
        public CustomGroupEditor()
        {
            title = "New Group";
        }

        public void UpdatePosition(Rect _pos)
        {
            m_data.Position = _pos;
            foreach (var obj in containedElements)
            {
                if (obj is NodeBaseEditor)
                {
                   ((NodeBaseEditor)obj).UpdatePosition(obj.GetPosition());
                }
            }
        }

        public void Move(Vector2 _pos, Vector2 _size)
        {
            var rect = new Rect(_pos, _size);
            SetPosition(rect);
        }


        public void InitializeNode(CustomGroup _data, MasterGraphView _view)
        {
            m_data = _data;
            mView = _view;
            title = m_data.Title;
            MarkDirtyRepaint();
        }

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            base.OnElementsAdded(elements);
            foreach (var obj in elements)
            {
                if (obj is NodeBaseEditor)
                {
                    if (!m_data.Children.Contains(((NodeBaseEditor)obj).baseData.Uid)) m_data.Children.Add(((NodeBaseEditor)obj).baseData.Uid);
                }
            }
        }

        protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            base.OnElementsRemoved(elements);
            foreach (var obj in elements)
            {
                if (obj is NodeBaseEditor)
                {
                    if (m_data.Children.Contains(((NodeBaseEditor)obj).baseData.Uid)) m_data.Children.Remove(((NodeBaseEditor)obj).baseData.Uid);
                }
            }
            if (containedElements.Count() <= 0)
            {
                mView.RemoveGroup(this);
            }
        }

        protected override void OnGroupRenamed(string oldName, string newName)
        {
            base.OnGroupRenamed(oldName, newName);
            m_data.Title = newName;
        }

    }
}
