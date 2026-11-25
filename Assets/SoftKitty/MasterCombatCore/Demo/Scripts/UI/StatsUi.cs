using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class StatsUi : MonoBehaviour
    {
        public Text StatName;
        public Text StatValue;
        public Image Glow;
        private Entity mEntity;
        private int mId=-1;
        private int oldValue = 0;
        private float glowAmount = 0F;
        public void Init(Entity _entity, int _attId)
        {
            mEntity = _entity;
            mId = _attId;
            StatName.text = GameManager.GetAttribute(mId).name+" : ";
            oldValue = mEntity.GetAttributeInt(mId);
            StatValue.text = oldValue.ToString();
        }

        void UpdateValue() {
            int _newValue = mEntity.GetAttributeInt(mId);
            if (_newValue != oldValue)
            {
                StatValue.text = _newValue.ToString();
                oldValue = _newValue;
                glowAmount = 1F;
            }
        } 
        void Update()
        {
            glowAmount = Mathf.MoveTowards(glowAmount,0F,Time.deltaTime*1.5F);
            Glow.color = new Color(Glow.color.r, Glow.color.g, Glow.color.b, glowAmount);
            if (mEntity == null || mId == -1) return;
            if(Time.frameCount%60==0) UpdateValue();
        }
    }
}
