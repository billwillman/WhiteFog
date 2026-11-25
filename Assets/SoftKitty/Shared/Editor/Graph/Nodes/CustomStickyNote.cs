using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;



namespace SoftKitty
{
    public class CustomStickyNote : StickyNote
    {
        private Note m_data;
        public Note data => m_data;

        public CustomStickyNote(Note _note)
        {
            m_data = _note;
            title = _note.Name;
            contents = _note.Text;
            fontSize = (StickyNoteFontSize)_note.FontSize;
            theme = (StickyNoteTheme)_note.Theme;
        }
        public CustomStickyNote()
        {
            title = "Title...";
            contents = "Content...";
        }

        public void UpdatePosition(Rect _pos)
        {
            m_data.Position = new Vector2(_pos.x, _pos.y );
            m_data.Size= new Vector2(_pos.width, _pos.height);
        }

        public void Move(Vector2 _pos, Vector2 _size)
        {
            var rect = new Rect(_pos, _size) ;
            SetPosition(rect);
        }

        public override void OnResized()
        {
            base.OnResized();
            Rect _pos = GetPosition();
            m_data.Size = new Vector2(_pos.width, _pos.height);
        }

        public void InitializeNode(Note _data)
        {
            m_data = _data;
            title = m_data.Name;
            contents = m_data.Text;
            fontSize = (StickyNoteFontSize)m_data.FontSize;
            theme = (StickyNoteTheme)m_data.Theme;
            this.RegisterCallback<StickyNoteChangeEvent>(OnStickyNoteChanged);
            MarkDirtyRepaint();
        }

        private void OnStickyNoteChanged(StickyNoteChangeEvent evt)
        {
            StickyNote changedNote = evt.currentTarget as StickyNote;
            if (changedNote != null)
            {
                m_data.Name = changedNote.title;
                m_data.Text = changedNote.contents;
                m_data.FontSize= (int)changedNote.fontSize;
                m_data.Theme = (int)changedNote.theme;
                
            }
        }

        
    }
}
