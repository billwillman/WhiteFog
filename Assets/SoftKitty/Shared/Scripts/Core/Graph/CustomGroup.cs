using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    [System.Serializable]
    public class CustomGroup 
    {
        public Rect Position;
        public List<string> Children = new List<string>();
        public string Title;
        public string Uid;

        public CustomGroup()
        {

        }
        public CustomGroup(CustomGroup _source)
        {
            this.Position = _source.Position;
            this.Title = _source.Title;
            this.Position = _source.Position;
            this.Uid = _source.Uid;
            Children.Clear();
            for (int i = 0; i < _source.Children.Count; i++)
            {
                Children.Add(_source.Children[i]);
            }
        }

        public void RandomeUid()
        {
            Uid = Random.Range(0, 99).ToString() + Random.Range(0, 9).ToString() + Random.Range(0, 999).ToString() + Random.Range(0, 9).ToString();
        }
        public CustomGroup Copy(  )
        {
            CustomGroup _copy = new CustomGroup(this);
            return _copy;
        }
    }
}
