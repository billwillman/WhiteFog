using UnityEngine;

namespace SoftKitty
{
    [System.Serializable]
    public class Note
    {
        public string Uid;
        public string Name;
        public string Text;
        public Vector2 Position;
        public Vector2 Size;
        public int FontSize=0;
        public int Theme=0;

        public Note()
        {
            Name = "Title...";
            Text = "Content...";
            Position = Vector2.zero;
            Size = new Vector2(300, 100);
            FontSize = 1;
            Theme = 0;
        }

        public void RandomeUid()
        {
            Uid = Random.Range(0, 9).ToString() + Random.Range(0, 99).ToString() + Random.Range(0, 999).ToString() + Random.Range(0, 9).ToString() + Random.Range(0, 9).ToString();
        }
 
      
        public Note(Note _source)
        {
            this.Uid= _source.Uid;
            this.Name = _source.Name;
            this.Text = _source.Text;
            this.Size = _source.Size;
            this.FontSize = _source.FontSize;
            this.Theme = _source.Theme;
            this.Position = _source.Position;
        }
        public Note Copy()
        {
            Note _copy = new Note(this);
            return _copy;
        }
    }
}
