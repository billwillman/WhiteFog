using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterCombatCore.Demo
{
    /// <summary>
    /// This is an example how to use OverTimeEffect class on interface.
    /// </summary>
    public class OteUi : MonoBehaviour
    {
        public Image Background;
        public Image Frame;
        public RawImage icon;
        public Text layer;
        public Image timeMask;
        private bool inited = false;
        private float duration = 1F;
        private float targetFill = 1F;
        public void Init(OverTimeEffectData _oteData)
        {
            OverTimeEffect _setting = GameManager.GetOverTimeEffect(_oteData.uid);
            if (_setting != null)
            {
                duration = Mathf.Max(_oteData.timer, _setting.duration);
                icon.texture = _setting.icon;
                Background.color = _setting.backGroundColor;
                Frame.color = _setting.frameColor;
            }
            else
            {
                duration = _oteData.timer;
                Background.color = Color.black;
                Frame.color = Color.grey;
            }
            inited = true;
            UpdateOte(_oteData);
        }

        public void UpdateOte(OverTimeEffectData _oteData)
        {
            if (!inited) return;
            duration = Mathf.Max(_oteData.timer, duration,1F);
            targetFill=Mathf.Max(0, (duration - (_oteData.timer - 1)) / duration );
            timeMask.fillAmount = (duration - _oteData.timer) / duration;
            if (_oteData.layer > 1)
                layer.text = _oteData.layer.ToString();
            else
                layer.text = "";
        }

        private void Update()
        {
            if (!inited) return;
            timeMask.fillAmount = Mathf.MoveTowards(timeMask.fillAmount, targetFill, Time.deltaTime/ duration);
        }

        public void ReturnToPool()
        {
            inited = false;
            timeMask.fillAmount = 0F;
            HealthBarManager.ReturnOteToPool(this);
        }
         
    }
}
