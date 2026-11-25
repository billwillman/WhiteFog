using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterCombatCore.Demo
{
    /// <summary>
    /// This is an example how to use OverTimeEffect class on interface.
    /// </summary>
    public class DamageUi : MonoBehaviour
    {
        public Text num;
        public Animation ani;
        private bool inited = false;
        private float timer=0F;
        public void Init(int _damage, Color _color, bool _critical)
        {
            if (_damage < 0)
            {
                num.text = (-_damage).ToString();
            }
            else
            {
                num.text = "+ "+ _damage.ToString();
            }
            num.color = _color;
            ani.Stop();
            ani.Play(_critical ? "DamageCriticalPop" : "DamagePop");
            timer = 0F;
            inited = true;
        }

        public void ReturnToPool()
        {
            inited = false;
            timer = 0F;
            ani.Stop();
            HealthBarManager.ReturnDamageToPool(this);
        }

        void Update()
        {
            if (!inited) return;
            timer = Mathf.MoveTowards(timer,1F,Time.deltaTime);
            if (timer >= 1F) ReturnToPool();
        }
    }
}
