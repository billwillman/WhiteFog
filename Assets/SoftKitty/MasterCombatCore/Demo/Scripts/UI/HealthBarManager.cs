using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore.Demo
{
    /// <summary>
    /// This is a pool manager for ui elements like: health bar, ote effect, damage pop
    /// </summary>
    public class HealthBarManager : MonoBehaviour
    {
        public Transform FloatingBarRoot;
        public Transform PoolRoot;
        public HealthBarUi [] StaticBarList;
        public GameObject BarObj;
        public GameObject DamageObj;
        public GameObject OteObj;
        public static HealthBarManager instance;
        private static Dictionary<string, HealthBarUi> ActiveBars = new Dictionary<string, HealthBarUi>();
        public List<HealthBarUi> BarInPool = new List<HealthBarUi>();
        public List<DamageUi> DamageInPool = new List<DamageUi>();
        public List<OteUi> OteInPool = new List<OteUi>();

        void Awake()
        {
            instance = this;
            ActiveBars.Clear();
            foreach (var obj in StaticBarList) { //Add static bars into the active list.
                ActiveBars.Add(obj.uid, obj);
            }

            BarInPool.Clear();
            DamageInPool.Clear();
            OteInPool.Clear();
            for (int i=0;i<10;i++) {//Prewarm the pool
                HealthBarUi _newBar = Instantiate(BarObj, PoolRoot).GetComponent<HealthBarUi>();
                BarInPool.Add(_newBar);
                DamageUi _newDamage = Instantiate(DamageObj, PoolRoot).GetComponent<DamageUi>();
                DamageInPool.Add(_newDamage);
                OteUi _newOte = Instantiate(OteObj, PoolRoot).GetComponent<OteUi>();
                OteInPool.Add(_newOte);
            }
        }

        public static void CreateBar(EntityComponent _entity)
        {
            if (ActiveBars.ContainsKey(_entity.uid))
            {
                ActiveBars[_entity.uid].Init(_entity);
                ActiveBars[_entity.uid].gameObject.SetActive(true);
                return;
            }
            if (instance.BarInPool.Count > 0)
            {
                HealthBarUi _newBar = instance.BarInPool[0];
                instance.BarInPool.Remove(_newBar);
                _newBar.transform.SetParent(instance.FloatingBarRoot);
                _newBar.Init(_entity);
                _newBar.gameObject.SetActive(true);
                ActiveBars.Add(_entity.uid, _newBar);
            }
            else
            {
                HealthBarUi _newBar = Instantiate(instance.BarObj, instance.FloatingBarRoot).GetComponent<HealthBarUi>();
                _newBar.Init(_entity);
                _newBar.gameObject.SetActive(true);
                ActiveBars.Add(_entity.uid, _newBar);
            }
        }

        public static void ReturnBarToPool(HealthBarUi _bar)
        {
            if(ActiveBars.ContainsKey(_bar.uid)) ActiveBars.Remove(_bar.uid);
            _bar.uid = "";
            _bar.gameObject.SetActive(false);
            _bar.transform.SetParent(instance.PoolRoot);
            instance.BarInPool.Add(_bar);
        }

        public static void CreateDamage(int _damage, HealthBarUi _bar, Color _color, bool _critical=false)
        {
            if (instance.DamageInPool.Count > 0)
            {
                DamageUi _newDamage = instance.DamageInPool[0];
                instance.DamageInPool.Remove(_newDamage);
                _newDamage.transform.SetParent(_bar.damageRoot);
                _newDamage.transform.localPosition = Vector3.zero;
                _newDamage.transform.localEulerAngles = Vector3.zero;
                _newDamage.transform.localScale = Vector3.one;
                _newDamage.gameObject.SetActive(true);
                _newDamage.Init(_damage, _color, _critical);
            }
            else
            {
                DamageUi _newDamage = Instantiate(instance.DamageObj, _bar.damageRoot).GetComponent<DamageUi>();
                _newDamage.transform.localPosition = Vector3.zero;
                _newDamage.transform.localEulerAngles = Vector3.zero;
                _newDamage.transform.localScale = Vector3.one;
                _newDamage.gameObject.SetActive(true);
                _newDamage.Init(_damage, _color, _critical);
            }
        }

        public static void ReturnDamageToPool(DamageUi _damage)
        {
            _damage.transform.SetParent(instance.PoolRoot);
            _damage.gameObject.SetActive(false);
            instance.DamageInPool.Add(_damage);
        }

        public static OteUi CreateOte(OverTimeEffectData _data, HealthBarUi _bar)
        {
            if (instance.DamageInPool.Count > 0)
            {
                OteUi _newOte = instance.OteInPool[0];
                instance.OteInPool.Remove(_newOte);
                _newOte.transform.SetParent(_bar.oteRoot);
                _newOte.transform.localEulerAngles = Vector3.zero;
                _newOte.transform.localScale = Vector3.one;
                _newOte.gameObject.SetActive(true);
                _newOte.Init(_data);
                return _newOte;
            }
            else
            {
                OteUi _newOte = Instantiate(instance.OteObj, _bar.oteRoot).GetComponent<OteUi>();
                _newOte.transform.localEulerAngles = Vector3.zero;
                _newOte.transform.localScale = Vector3.one;
                _newOte.gameObject.SetActive(true);
                _newOte.Init(_data);
                return _newOte;
            }
        }

        public static void ReturnOteToPool(OteUi _ote)
        {
            _ote.transform.SetParent(instance.PoolRoot);
            _ote.gameObject.SetActive(false);
            instance.OteInPool.Add(_ote);
        }

    }
}
