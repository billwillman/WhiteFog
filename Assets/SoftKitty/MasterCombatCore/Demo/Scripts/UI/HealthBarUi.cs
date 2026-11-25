using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore.Demo
{
    /// <summary>
    /// This is an example how to use Entity class on interface.
    /// </summary>
    public class HealthBarUi : MonoBehaviour
    {
        public bool StaticBar = false;
        public string uid = "";
        public float fullWidth = 250F;
        public Image bar;
        public Image followBar;
        public Image XpBar;
        public Text barValue;
        public Text levelNum;
        public RectTransform oteRoot;
        public RectTransform damageRoot;
        public Color DamageColor;
        public Color CriticalDamageColor;
        public Color HealColor;
        public CanvasGroup Group;
        public EntityComponent mEntity;
        private Dictionary<string, OteUi> mOteDic = new Dictionary<string, OteUi>();
        private bool inited = false;
        private bool outView = true;
        public void Init(EntityComponent _entity)
        {
            mEntity = _entity;
            uid = _entity.uid;
            mEntity.RegisterEntityEventCallback(OnEntityEvent);
            mEntity.RegisterOverTimeChangeCallback(OnOverTimeChange);
            StateUpdate(true);
            PositionUpdate();
            inited = true;
        }

        public void OnEntityEvent(string _eventName, float _floatArg, Entity _entityArg, bool _boolArg)
        {
            if (!inited  || Mathf.RoundToInt(_floatArg)==0) return;
            if (_eventName == "Damage")
            {
                HealthBarManager.CreateDamage(Mathf.RoundToInt(-_floatArg),this, _boolArg?CriticalDamageColor:DamageColor, _boolArg);
            }
            else if(_eventName == "Heal")
            {
                HealthBarManager.CreateDamage(Mathf.RoundToInt(_floatArg), this,HealColor, false);
            }
        }

        public void OnOverTimeChange(string _uid, OverTimeEffectData _data, OverTimeEffectEventType _eventType)
        {
            if (_eventType == OverTimeEffectEventType.Add || _eventType == OverTimeEffectEventType.Update)
            {
                if (!mOteDic.ContainsKey(_uid))
                {
                    OteUi _newOte = HealthBarManager.CreateOte(_data, this);
                    mOteDic.Add(_uid, _newOte);
                }
                else
                {
                    mOteDic[_uid].UpdateOte(_data);
                }
            }
            else
            {
                OteUi _ote = null;
                mOteDic.TryGetValue(_uid, out _ote);
                if (_ote!=null)
                {
                    _ote.ReturnToPool();
                    mOteDic.Remove(_uid);
                }
            }
        }


        public void ReturnToPool()
        {
            ResetBar();
            ClearTextAndOte();
            HealthBarManager.ReturnBarToPool(this);
        }

        private void ResetBar()
        {
            mEntity.UnRegisterEntityEventCallback(OnEntityEvent);
            mEntity.UnRegisterOverTimeChangeCallback(OnOverTimeChange);
            mEntity = null;
            inited = false;
            outView = true;
            levelNum.text = "-";
        }

        void ClearTextAndOte()
        {
            foreach (var obj in damageRoot.GetComponentsInChildren<DamageUi>())
            {
                obj.ReturnToPool();
            }
            foreach (var obj in oteRoot.GetComponentsInChildren<OteUi>())
            {
                obj.ReturnToPool();
            }
            mOteDic.Clear();
        }

        void Update()
        {
            if (!inited) return;
            StateUpdate(false);
        }

        void LateUpdate()
        {
            if (!inited) return;
            PositionUpdate();
        }

        

        void StateUpdate(bool _instance)
        {
            if (mEntity == null)
            {
                if (inited) {
                    if (StaticBar)
                    {
                        ResetBar();
                        ClearTextAndOte();
                    }
                    else
                    {
                        ReturnToPool();
                    }
                }
                return;
            }
            if (!mEntity.AvailableForInteraction || mEntity.GetAttributeFloat("chp") <= 0F)
            {
                if (!StaticBar) ReturnToPool();
                return;
            }
            if (!StaticBar)
            {
                Group.alpha = outView ? 0F : Mathf.MoveTowards(Group.alpha,1F,Time.deltaTime*3F);
                if (outView) return;
            }
            
            string _level = mEntity.GetAttributeInt("lvl").ToString();
            float _hp = mEntity.GetAttributeFloat("chp");
            float _maxhp = mEntity.GetAttributeFloat("hp");
            float _width = fullWidth * Mathf.Clamp01(_hp / _maxhp);

            Vector2 _barSize = bar.rectTransform.sizeDelta;
            Vector2 _followSize= followBar.rectTransform.sizeDelta;
            if (_instance)
            {
                _barSize.x = _width;
                _followSize.x = _width;
            }
            else
            {
                if (_barSize.x < _width)
                    _barSize.x = Mathf.Lerp(_barSize.x, _width, Time.deltaTime * 2F);
                else
                    _barSize.x = _width;
                if (_followSize.x > _width)
                    _followSize.x = Mathf.Lerp(_followSize.x, _width, Time.deltaTime * 2F);
                else
                    _followSize.x = _width;
            }
            bar.rectTransform.sizeDelta = _barSize;
            followBar.rectTransform.sizeDelta = _followSize;
            string _text = Mathf.CeilToInt(_hp).ToString() + " / " + Mathf.CeilToInt(_maxhp);
            if (barValue.text != _text) barValue.text = _text;
            if (levelNum.text != _level)
            {
                if (inited && levelNum.text!="-") levelNum.GetComponentInParent<Animation>().Play();
                levelNum.text = _level;
            }

            if (XpBar)
            {
                float _xpWidth = fullWidth * Mathf.Clamp01(mEntity.GetAttributeFloat("xp") / mEntity.GetAttributeFloat("mxp"));
                XpBar.rectTransform.sizeDelta = new Vector2(
                    Mathf.Lerp(XpBar.rectTransform.sizeDelta.x, _xpWidth,Time.deltaTime*5F),
                    XpBar.rectTransform.sizeDelta.y
                    ); ;
            }

        }

        void PositionUpdate()
        {
            if (StaticBar) return;
            if (mEntity == null) return;
            Vector3 _pos = GetScreenPos(mEntity.transform.position, 2.05F);
            if (_pos == Vector3.zero)
            {
                outView = true;
                return;
            }
            float _dis = Vector3.Distance(mEntity.Position, Camera.main.transform.position);
            outView = _dis>30F;
            transform.localPosition = _pos;
        }

        Vector3 GetScreenPos(Vector3 _worldPos, float _yOffset)
        {
            Vector3 posDelta = _worldPos;
            posDelta.y += _yOffset;
            Vector3 pos = Vector3.zero;
            pos = Camera.main.WorldToViewportPoint(posDelta);
            if (pos.z < 0F)
            {
                return Vector3.zero;
            }
            pos.z = 0F;

            pos.x *= HealthBarManager.instance.FloatingBarRoot.GetComponent<RectTransform>().rect.width;
            pos.y *= HealthBarManager.instance.FloatingBarRoot.GetComponent<RectTransform>().rect.height;

            return new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), 0F);
        }

    }
}
