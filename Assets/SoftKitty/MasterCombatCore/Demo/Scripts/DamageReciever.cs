using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore
{
    public class DamageReciever : MonoBehaviour
    {
        public bool Enable = true;
        public float DamageMultiplier = 1F;

        private void Start()
        {
            SetEnable(Enable);
        }

        public EntityComponent Entity
        {
            get
            {
                if (_entity==null) {
                    if (GetComponentInParent<EntityComponent>())
                        _entity = GetComponentInParent<EntityComponent>();
                    else if (GetComponent<EntityComponent>())
                        _entity = GetComponent<EntityComponent>();
                    else if (GetComponentInChildren<EntityComponent>())
                        _entity = GetComponent<EntityComponent>();

                    if (_entity == null)
                    {
                        SetEnable(false);
                    }
                }
                return _entity;
            }
        }


        public void SetEnable(bool _enable)
        {
            Enable = _enable;
            foreach (var obj in Colliders) {
                obj.enabled = _enable;
            }
        }

        public Collider[] Colliders
        {
            get
            {
                if (_colliders.Length == 0)
                {
                    _colliders = GetComponents<Collider>();
                }
                return _colliders;
            }
        }

        private EntityComponent _entity;
        private Collider []  _colliders = new Collider[0];
    }
}
