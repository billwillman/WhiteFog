using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SoftKitty
{
    /// <summary>
    /// CombatDamage inherits from GraphInstance and adds specific functionality for handling damage detection and dealing damage in combat scenarios.
    /// You can take this as an example create your own script inherits from GraphInstance, using GraphObject to achieve unique function.
    /// </summary>
    public class CombatDamage : GraphInstance
    {
        #region Variables
        public List<DynamicFloat> DynamicFloatList = new List<DynamicFloat>();
        public EntityComponent TargetEntity;
        public EntityComponent DealerEntity;
        public bool Enable = true;
        public float DamageCoolDown = 0.2F;
        public bool PhysicsDetection = false;
        public enum PhysicsTypes
        {
            Trigger_Collider,
            Rigidbody_Collider,
            Raycast,
            SphereCast,
            Overlap_Sphere,
            Overlap_Capsule
        }
        public bool is2D = false;
        public float min2dDepth = -1F;
        public float max2dDepth = 1F;
        public PhysicsTypes PhysicsType;
        public int PhysicsInterval = 1;
        public LayerMask PhysicsLayerMask;
        public List<Collider> mColliders = new List<Collider>();
        public List<Collider2D> mColliders2D = new List<Collider2D>();
        public Rigidbody mRigid;
        public Rigidbody2D mRigid2D;
        public List<string> TagFilter = new List<string>();
        public float Distance=1F;
        public float Radius=0.2F;
        public Vector3 DirOffset = new Vector3(0F, 0F, 1F);
        public Vector3 PosOffset = new Vector3(0F, 0F, 0.5F);
        public bool IgnoreTriggerColliders = false;
        public bool PhysicsPreview = true;
        public enum ProjectileModes
        {
            Disable,
            ScriptDrive,
            PhysicsDrive
        }
        public ProjectileModes ProjectileMode;
        public float ProjectileGravity = 1F;
        public bool ProjectileUseGravity = true;
        public float ProjectileSpeed = 40F;
        public float ProjectileLife = 8F;
        public bool DestroyOnHitGround = true;
        public bool DestroyOnHitTarget = true;
        public GameObject ExhaustedPrefab;
        public GameObject HitGroundEffectPrefab;
        public GameObject HitTargetEffectPrefab;
        public bool AutoSeekingTarget=false;
        public float SeekingRadius = 20F;
        public float SeekingAngularVelocity = 150F;
        public bool LaunchOnStart = true;
        public AnimationCurve ProjectileSpeedCurve=new AnimationCurve();
        #endregion

        #region Internal Methods
        [HideInInspector]
        public bool ui_optionalRef = false;
        [HideInInspector]
        public bool ui_physics = false;
        [HideInInspector]
        public bool ui_projectile = false;
        private Coroutine AutoDisableCo;
        private Dictionary<EntityComponent, float> DamagedEntities = new Dictionary<EntityComponent, float>();
        private List<EntityComponent> PendingRemoveEntities = new List<EntityComponent>();
        private List<EntityComponent> DamagedEntitiesKeys = new List<EntityComponent>();
        private Entity instanceEntity;
        private bool projectileLaunched = false;
        private EntityComponent seekingTarget;
        private float life = 0F;

        private ContactFilter2D groundFilter = new ContactFilter2D();
        private ContactFilter2D filter = new ContactFilter2D();
        private RaycastHit2D[] _hits = new RaycastHit2D[1];
        private RaycastHit2D[] _groundHits = new RaycastHit2D[1];
        private Collider2D[] _cols = new Collider2D[10];
        private int _hitCount = 0;
        private RaycastHit hit;
        private RaycastHit groundHit;
        private Collider[] _3dCols = new Collider[10];
        private Collider[] _3dSeekCols = new Collider[5];
        private int _seekCount = 0;
        private Collider2D[] _seekCols = new Collider2D[5];
        private Vector3 projectileDir;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            if (ProjectileMode!= ProjectileModes.Disable) {
                Color _oriColor = Gizmos.color;
                Gizmos.color = Color.yellow;
                if (is2D)
                {
                    Gizmos.DrawLine(transform.position, transform.position + transform.right * 3F);
                    Gizmos.DrawLine(transform.position + transform.right * 3F, transform.position + transform.right * 2F + transform.up * 1F);
                    Gizmos.DrawLine(transform.position + transform.right * 3F, transform.position + transform.right * 2F - transform.up * 1F);
                }
                else
                {
                    Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3F);
                    Gizmos.DrawLine(transform.position + transform.forward * 3F, transform.position + transform.forward * 2F + transform.up * 1F);
                    Gizmos.DrawLine(transform.position + transform.forward * 3F, transform.position + transform.forward * 2F - transform.up * 1F);
                }
                Gizmos.color = _oriColor;
            }
            if (PhysicsDetection && PhysicsPreview && PhysicsType != PhysicsTypes.Trigger_Collider && PhysicsType != PhysicsTypes.Rigidbody_Collider)
            {
                Color _oriColor = Gizmos.color;
                Gizmos.color = Color.green;
                switch (PhysicsType) {
                    case PhysicsTypes.Raycast:
                        Gizmos.DrawLine(GetPhysicsPos(), GetPhysicsPos() + GetPhysicsDir() * Distance);
                        break;
                    case PhysicsTypes.SphereCast:
                        Gizmos.DrawWireSphere(GetPhysicsPos(), Radius);
                        Gizmos.DrawLine(GetPhysicsPos()-transform.right* Radius, GetPhysicsPos() + GetPhysicsDir() * Distance - transform.right * Radius);
                        Gizmos.DrawLine(GetPhysicsPos() + transform.right * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance + transform.right * Radius);
                        Gizmos.DrawLine(GetPhysicsPos() - transform.up * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance - transform.up * Radius);
                        Gizmos.DrawLine(GetPhysicsPos() + transform.up * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance + transform.up * Radius);
                        Gizmos.DrawWireSphere(GetPhysicsPos() + GetPhysicsDir() * Distance, Radius);
                        break;
                    case PhysicsTypes.Overlap_Capsule:
                        Gizmos.DrawWireSphere(GetPhysicsPos(), Radius);
                        Gizmos.DrawLine(GetPhysicsPos() - transform.right * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance - transform.right * Radius);
                        Gizmos.DrawLine(GetPhysicsPos() + transform.right * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance + transform.right * Radius);
                        Gizmos.DrawLine(GetPhysicsPos() - transform.up * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance - transform.up * Radius);
                        Gizmos.DrawLine(GetPhysicsPos() + transform.up * Radius, GetPhysicsPos() + GetPhysicsDir() * Distance + transform.up * Radius);
                        Gizmos.DrawWireSphere(GetPhysicsPos() + GetPhysicsDir() * Distance, Radius);
                        break;
                    case PhysicsTypes.Overlap_Sphere:
                        Gizmos.DrawWireSphere(GetPhysicsPos(), Radius);
                        break;
                }
                Gizmos.color = _oriColor;
            }
        }
#endif
        IEnumerator AutoDisableLater(float _time)
        {
            yield return new WaitForSeconds(_time);
            SetEnable(false);
        }
        
        private void VerifyObject(GameObject obj)
        {
            EntityComponent _entity = GetTarget(obj);
            if (_entity!=null){
                if (DamageCoolDown > 0F && DamagedEntities.ContainsKey(_entity)) return;
                if (DamageCoolDown > 0F) DamagedEntities.Add(_entity, DamageCoolDown);
                DealDamage(_entity.mData);
            }
        }

        private EntityComponent GetTarget(GameObject obj)
        {
            EntityComponent _entity = null;
            if (obj.GetComponent<EntityComponent>())
                _entity = obj.GetComponent<EntityComponent>();
            else if (obj.GetComponentInChildren<EntityComponent>())
                _entity = obj.GetComponentInChildren<EntityComponent>();
            else if (obj.GetComponentInParent<EntityComponent>())
                _entity = obj.GetComponentInParent<EntityComponent>();

            if (_entity != null && _entity.mData != null && _entity.hasMatchingTag(TagFilter)) return _entity;
            return null;
        }

        private Vector3 GetPhysicsPos()
        {
            return transform.position + transform.rotation * PosOffset;
        }
        private Vector3 GetPhysicsDir()
        {
            return (transform.rotation * DirOffset).normalized;
        }
        private Vector2 GetPhysics2dPos()
        {
            return new Vector2(GetPhysicsPos().x, GetPhysicsPos().y);
        }
        private Vector2 GetPhysics2dDir()
        {
            return new Vector2(GetPhysicsDir().x, GetPhysicsDir().y);
        }
        public void UpdateInstanceEntity()
        {
            instanceEntity.Position = transform.position;
            instanceEntity.Forward = transform.forward;
            instanceEntity.Scale = transform.localScale;
        }

        public override void Init()
        {
            base.Init();
            if (currentRootInstance != null)
            {
                mRuntime.RootState.RootInstance = gameObject;
                foreach (var obj in currentRootInstance.Handlers) obj.Init();
                foreach (var obj in currentRootInstance.ConditionGroups) obj.Init();
                instanceEntity = new Entity();
                instanceEntity.uid = "instance";
                mRuntime.RootState.SetInvolvedEntity("Instance", instanceEntity);
                inited = true;
            }
        }

        #endregion

        #region MonoBehaviour
        public override void Start()
        {
            base.Start();
            SetEnable(Enable);
            if (ProjectileMode!= ProjectileModes.Disable && LaunchOnStart) Launch();
        }

        public virtual void OnEnable()
        {
            if (ProjectileMode != ProjectileModes.Disable && LaunchOnStart) Launch();
        }
        public virtual void Update()
        {
            if (DamageCoolDown > 0F)
            {
                PendingRemoveEntities.Clear();
                DamagedEntitiesKeys.Clear();
                DamagedEntitiesKeys.AddRange(DamagedEntities.Keys);
                foreach (var _key in DamagedEntitiesKeys)
                {
                    DamagedEntities[_key] = Mathf.MoveTowards(DamagedEntities[_key], 0F, Time.deltaTime);
                    if (DamagedEntities[_key] <= 0F) PendingRemoveEntities.Add(_key);
                }
                foreach (var _key in PendingRemoveEntities)
                {
                    if (DamagedEntities.ContainsKey(_key)) DamagedEntities.Remove(_key);
                }
                DamagedEntitiesKeys.Clear();
                PendingRemoveEntities.Clear();
            }
        }
        public virtual void FixedUpdate()
        {
            if (is2D)
            {
                Physics2DCast();
            }
            else
            {
                PhysicsCast();
            }
            if (projectileLaunched && ProjectileMode != ProjectileModes.Disable)
            {
                life += Time.fixedDeltaTime;

                if (AutoSeekingTarget)
                {
                    if (seekingTarget == null)
                    {
                        if (is2D)
                        {
                            filter.useTriggers = !IgnoreTriggerColliders;
                            filter.minDepth = min2dDepth;
                            filter.maxDepth = max2dDepth;
                            filter.layerMask = PhysicsLayerMask;
                            filter.useLayerMask = true;
                            filter.useDepth = true;
                            filter.useTriggers = true;

                            _seekCount = Physics2D.OverlapCircle(transform.position, SeekingRadius, filter, _seekCols);
                            for (int i=0;i< _seekCount;i++){
                                if (i < _seekCols.Length && _seekCols[i]!=null) {
                                    EntityComponent _target = GetTarget(_seekCols[i].gameObject);
                                    if (_target != null)
                                    {
                                        if (seekingTarget != null)
                                        {
                                            if (Vector3.Angle(transform.right, (_target.Position - transform.position).normalized) < Vector3.Angle(transform.right, (seekingTarget.Position - transform.position).normalized))
                                            {
                                                seekingTarget = _target;
                                            }
                                        }
                                        else
                                        {
                                            seekingTarget = _target;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            _3dSeekCols = Physics.OverlapSphere(transform.position, SeekingRadius, PhysicsLayerMask, IgnoreTriggerColliders ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
                            foreach (var obj in _3dSeekCols)
                            {
                                EntityComponent _target = GetTarget(obj.gameObject);
                                if (_target != null)
                                {
                                    if (seekingTarget != null)
                                    {
                                        if (Vector3.Angle(transform.forward, (_target.Position - transform.position).normalized) < Vector3.Angle(transform.forward, (seekingTarget.Position - transform.position).normalized))
                                        {
                                            seekingTarget = _target;
                                        }
                                    }
                                    else
                                    {
                                        seekingTarget = _target;
                                    }
                                }
                            }
                        }

                        
                    }
                    else
                    {
                        projectileDir = (seekingTarget.Position + Vector3.up * 0.5F - transform.position).normalized;
                        if (is2D) projectileDir.z = 0F;
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(projectileDir), SeekingAngularVelocity*Time.fixedDeltaTime);
                    }
                }

                if (life >= ProjectileLife)
                {
                    if (ExhaustedPrefab != null)
                    {
                       GameObject _newEffect=  Instantiate(ExhaustedPrefab, transform.position, Quaternion.identity);
                        _newEffect.SetActive(true);
                    }
                    Destroy(gameObject);
                }
                else if (ProjectileMode == ProjectileModes.ScriptDrive)
                {
                    
                    if (is2D)
                    {
                        projectileDir = transform.right;
                        projectileDir.z = 0F;
                    }
                    else
                    {
                        projectileDir = transform.forward;
                    }
                    transform.position += projectileDir * Mathf.Max(1F, ProjectileSpeed * ProjectileSpeedCurve.Evaluate(Mathf.Clamp01(life/ProjectileLife)))*Time.fixedDeltaTime;
                    projectileDir += Vector3.down * ProjectileGravity * 0.1F * Time.fixedDeltaTime;
                    if (is2D)
                    {
                        projectileDir.z = 0F;
                        transform.right = projectileDir.normalized;
                    }
                    else
                    {
                        transform.forward = projectileDir.normalized;
                    }
                }
                if (DestroyOnHitGround || HitGroundEffectPrefab)
                {
                    if (is2D)
                    {
                        groundFilter.useTriggers = false;
                        groundFilter.minDepth = -0.1F;
                        groundFilter.maxDepth = 0.1F;
                        groundFilter.layerMask = SGD_Settings.Instance.GroundLayer;
                        groundFilter.useLayerMask = true;
                        groundFilter.useDepth = true;
                        groundFilter.useTriggers = true;
                        if (Physics2D.CircleCast(transform.position,0.1F, transform.right, groundFilter, _groundHits, 1F) > 0)
                        {
                            CheckingGround(_groundHits[0].collider.gameObject, groundHit.point);
                        }
                    }
                    else
                    {
                        if (Physics.SphereCast(transform.position, 0.1F, transform.forward, out groundHit, 1F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                        {
                            CheckingGround(groundHit.collider.gameObject, groundHit.point);
                        }
                    }
                }
            }
        }

        private void CheckingGround(GameObject _obj, Vector3 _point)
        {
            if ((SGD_Settings.Instance.GroundLayer & (1 << _obj.layer)) != 0)
            {
                if (HitGroundEffectPrefab != null)
                {
                   GameObject _newEffect=  Instantiate(HitGroundEffectPrefab, _point, Quaternion.identity);
                    _newEffect.SetActive(true);
                }
                if (DestroyOnHitGround) Destroy(gameObject, Mathf.Clamp(DamageCoolDown * 0.8F, Time.fixedDeltaTime * 2F, 0.3F));
            }
        }

        public virtual void OnCollisionEnter(Collision collision)
        {
            if (is2D) return;
            if (projectileLaunched && ProjectileMode != ProjectileModes.Disable && (DestroyOnHitGround || HitGroundEffectPrefab)) CheckingGround(collision.collider.gameObject, collision.contacts[0].point);
            if (!PhysicsDetection || PhysicsType != PhysicsTypes.Rigidbody_Collider || !Enable) return;
            if (collision.collider != null && (!collision.collider.isTrigger || !IgnoreTriggerColliders) && (PhysicsLayerMask & (1 << collision.collider.gameObject.layer)) != 0) VerifyObject(collision.collider.gameObject);
        }

        public virtual void OnTriggerEnter(Collider other)
        {
            if (is2D) return;
            if (projectileLaunched && ProjectileMode != ProjectileModes.Disable && (DestroyOnHitGround || HitGroundEffectPrefab)) CheckingGround(other.gameObject, transform.position);
            if (!PhysicsDetection || PhysicsType != PhysicsTypes.Trigger_Collider || !Enable) return;
            if ((!other.isTrigger || !IgnoreTriggerColliders) && (PhysicsLayerMask & (1 << other.gameObject.layer)) != 0) VerifyObject(other.gameObject);
           
        }

        public virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (!is2D) return;
            if (projectileLaunched && ProjectileMode != ProjectileModes.Disable && (DestroyOnHitGround || HitGroundEffectPrefab)) CheckingGround(collision.collider.gameObject, collision.contacts[0].point);
            if (!PhysicsDetection || PhysicsType != PhysicsTypes.Rigidbody_Collider || !Enable) return;
            if (collision.collider != null && (!collision.collider.isTrigger || !IgnoreTriggerColliders) && (PhysicsLayerMask & (1 << collision.collider.gameObject.layer)) != 0) VerifyObject(collision.collider.gameObject);
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            if (!is2D) return;
            if (projectileLaunched && ProjectileMode != ProjectileModes.Disable && (DestroyOnHitGround || HitGroundEffectPrefab)) CheckingGround(other.gameObject, transform.position);
            if (!PhysicsDetection || PhysicsType != PhysicsTypes.Trigger_Collider || !Enable) return;
            if ((!other.isTrigger || !IgnoreTriggerColliders) && (PhysicsLayerMask & (1 << other.gameObject.layer)) != 0) VerifyObject(other.gameObject);
        }

        #endregion

        #region Methods
        /// <summary>
        /// Enables or disables damage detection.
        /// </summary>
        /// <param name="_eanble"></param>
        public void SetEnable(bool _eanble)
        {
            Enable = _eanble;
            foreach (var obj in mColliders)
            {
                if(obj) obj.enabled = Enable;
            }
            foreach (var obj in mColliders2D)
            {
                if (obj) obj.enabled = Enable;
            }
            if (mRigid) mRigid.isKinematic = (PhysicsType == PhysicsTypes.Trigger_Collider || !Enable);
            if (mRigid2D) mRigid2D.bodyType = (PhysicsType == PhysicsTypes.Trigger_Collider || !Enable)? RigidbodyType2D.Kinematic: RigidbodyType2D.Dynamic;
            if (Enable) {
                if (is2D)
                {
                    Physics2DCast();
                }
                else
                {
                    PhysicsCast();
                }
            }
        }
        /// <summary>
        /// Enables damage detection and automatically disables it after a specified number of seconds.
        /// </summary>
        /// <param name="_autoDisableAfterSeconds"></param>
        public void SetEnable(float _autoDisableAfterSeconds)
        {
            SetEnable(true);
            if (AutoDisableCo != null) StopCoroutine(AutoDisableCo);
            AutoDisableCo=StartCoroutine(AutoDisableLater(_autoDisableAfterSeconds));
        }

        /// <summary>
        /// Launch the projectile to its current forward direction. The damage detection will be enabled in the same time.
        /// </summary>
        public void Launch() {
            Launch(is2D? transform.right:transform.forward);
        }

        /// <summary>
        /// Launch the projectile to the specified direction. The damage detection will be enabled in the same time.
        /// </summary>
        /// <param name="_direction"></param>
        public void Launch(Vector3 _direction) {
            if (projectileLaunched) return;
            if (is2D) _direction.z = 0F;
            transform.parent = null;
            if (is2D)
            {
                transform.right = _direction;
            }
            else
            {
                transform.forward = _direction;
            }
            seekingTarget = null;
            life = 0F;
            if (ProjectileMode == ProjectileModes.PhysicsDrive)
            {
                if (mRigid != null)
                {
                    mRigid.useGravity = ProjectileUseGravity;
#if UNITY_6000_0_OR_NEWER
                    mRigid.linearVelocity = Vector3.zero;
#else
                    mRigid.velocity = Vector3.zero;
#endif
                    mRigid.AddForce(transform.forward * ProjectileSpeed * mRigid.mass, ForceMode.Impulse);
                }
                if (mRigid2D != null)
                {
                    mRigid2D.gravityScale = ProjectileUseGravity?ProjectileGravity:0F;
#if UNITY_6000_0_OR_NEWER
                    mRigid2D.linearVelocity = Vector3.zero;
#else
                    mRigid2D.velocity = Vector3.zero;
#endif
                    mRigid2D.AddForce(new Vector2 (transform.forward.x, transform.forward.y) * ProjectileSpeed * mRigid2D.mass, ForceMode2D.Impulse);
                }
            }
            SetEnable(true);
            projectileLaunched = true;
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<CombatDamage> -> Launch(" + _direction+") > "+gameObject.name);
        }


        
        /// <summary>
        /// Perform a Physics detection for Raycast|SphereCast|Overlap_Sphere|Overlap_Capsule
        /// </summary>
        public void PhysicsCast()
        {
            if (is2D) return;
            if (!PhysicsDetection || PhysicsType == PhysicsTypes.Trigger_Collider || PhysicsType == PhysicsTypes.Rigidbody_Collider || Time.frameCount % PhysicsInterval != 0 || !Enable) return;
            switch (PhysicsType)
            {
                case PhysicsTypes.Raycast:
                    if (Physics.Raycast(GetPhysicsPos(), GetPhysicsDir(), out hit, Distance, PhysicsLayerMask, IgnoreTriggerColliders ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide))
                    {
                        VerifyObject(hit.collider.gameObject);
                    }
                    break;
                case PhysicsTypes.SphereCast:
                    if (Physics.SphereCast(GetPhysicsPos(), Radius, GetPhysicsDir(), out hit, Distance, PhysicsLayerMask, IgnoreTriggerColliders ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide))
                    {
                        VerifyObject(hit.collider.gameObject);
                    }
                    break;
                case PhysicsTypes.Overlap_Sphere:
                    _hitCount= Physics.OverlapSphereNonAlloc(GetPhysicsPos(), Radius, _3dCols,PhysicsLayerMask, IgnoreTriggerColliders ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
                    for (int i = 0; i < _hitCount; i++)
                    {
                        if (i < _3dCols.Length && _3dCols[i] != null) VerifyObject(_3dCols[i].gameObject);
                    }
                    break;
                case PhysicsTypes.Overlap_Capsule:
                    _hitCount = Physics.OverlapCapsuleNonAlloc(GetPhysicsPos(), GetPhysicsPos() + GetPhysicsDir() * Distance, Radius, _3dCols, PhysicsLayerMask, IgnoreTriggerColliders ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
                    for (int i = 0; i < _hitCount; i++)
                    {
                        if (i < _3dCols.Length && _3dCols[i] != null) VerifyObject(_3dCols[i].gameObject);
                    }
                    break;
            }
        }

        /// <summary>
        /// Perform a Physics2D detection for Raycast|SphereCast|Overlap_Sphere|Overlap_Capsule
        /// </summary>
        public void Physics2DCast()
        {
            if (!is2D) return;
            if (!PhysicsDetection || PhysicsType == PhysicsTypes.Trigger_Collider || PhysicsType == PhysicsTypes.Rigidbody_Collider || Time.frameCount % PhysicsInterval != 0 || !Enable) return;
            filter.useTriggers = !IgnoreTriggerColliders;
            filter.minDepth = min2dDepth;
            filter.maxDepth = max2dDepth;
            filter.layerMask = PhysicsLayerMask;
            filter.useLayerMask = true;
            filter.useDepth = true;
            filter.useTriggers = true;

            switch (PhysicsType)
            {
                case PhysicsTypes.Raycast:
                    if (Physics2D.Raycast(GetPhysics2dPos(), GetPhysics2dDir(),filter,_hits, Distance)>0)
                    {
                        VerifyObject(_hits[0].collider.gameObject);
                    }
                    break;
                case PhysicsTypes.SphereCast:
                    if (Physics2D.CircleCast(GetPhysics2dPos(), Radius, GetPhysics2dDir(), filter, _hits, Distance)>0)
                    {
                        VerifyObject(_hits[0].collider.gameObject);
                    }
                    break;
                case PhysicsTypes.Overlap_Sphere:
                    _hitCount = Physics2D.OverlapCircle(GetPhysics2dPos(), Radius, filter, _cols);
                    for (int i=0;i< _hitCount; i++)
                    {
                        if(i< _cols.Length && _cols[i]!=null) VerifyObject(_cols[i].gameObject);
                    }
                    break;
                case PhysicsTypes.Overlap_Capsule:
                    float _angle = Vector3.Angle(GetPhysics2dDir(), new Vector3(0F, 1F, 0F));
                    if (GetPhysics2dDir().y < 0F) _angle = -_angle;
                    _hitCount = Physics2D.OverlapCapsule(GetPhysics2dPos(),new Vector2(Distance, Radius*2F),CapsuleDirection2D.Horizontal,_angle, filter, _cols);
                    for (int i = 0; i < _hitCount; i++)
                    {
                        if (i < _cols.Length && _cols[i] != null) VerifyObject(_cols[i].gameObject);
                    }
                    break;
            }
        }

        /// <summary>
        /// Sets a dynamic variable in the visual graph object by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public override void SetDynamicValue(string _uid, float _value)
        {
            foreach (var obj in DynamicFloatList)
            {
                if (obj.uid == _uid) obj.value = _value;
            }
            base.SetDynamicValue(_uid, _value);
        }
        /// <summary>
        /// Manually triggers the damage calculation. Triggers are included in the calculation.
        /// </summary>
        public void DealDamage()
        {
            if (!PhysicsDetection && TargetEntity != null && GetTargetEntity() == null) SetTargetEntity(TargetEntity.mData);
            if(DealerEntity!=null && GetDealerEntity() == null) SetDealerEntity(DealerEntity.mData);
            if (GetDealerEntity()==null && DealerEntity==null) {
                if(GetComponentInParent<EntityComponent>()) SetDealerEntity(GetComponentInParent<EntityComponent>().mData);
            }
            if (GetTargetEntity() == null)
            {
                if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<CombatDamage> -> DealDamage() failed: Target Entity is not set.");
                return;
            }
            if (!GetTargetEntity().AvailableForInteraction) return;
            foreach (var obj in DynamicFloatList) SetDynamicValue(obj.uid, obj.value);
            if (projectileLaunched && ProjectileMode != ProjectileModes.Disable)
            {
                if (HitTargetEffectPrefab != null)
                {
                   GameObject _newEffect=  Instantiate(HitTargetEffectPrefab, transform.position, Quaternion.identity);
                    _newEffect.SetActive(true);
                }
                if(DestroyOnHitTarget) Destroy(gameObject,Mathf.Clamp(DamageCoolDown*0.8F,Time.fixedDeltaTime*2F,0.3F));
            }
            bool _success = IfTriggerThenPerformAction();
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<CombatDamage> -> DealDamage() > isTriggered:" + _success);
        }
        /// <summary>
        /// Manually deals damage to the provided target. Triggers are included in the calculation.
        /// </summary>
        /// <param name="_target"></param>
        public void DealDamage(Entity _target)
        {
            if (DealerEntity != null && GetDealerEntity() == null) SetDealerEntity(DealerEntity.mData);
            if (GetDealerEntity() == null && DealerEntity == null)
            {
                if (GetComponentInParent<EntityComponent>()) SetDealerEntity(GetComponentInParent<EntityComponent>().mData);
            }
            SetTargetEntity(_target);
            UpdateInstanceEntity();
            DealDamage();
        }
        /// <summary>
        /// Manually deals damage to the provided target with the provided dealer. Triggers are included in the calculation.
        /// </summary>
        /// <param name="_dealer"></param>
        /// <param name="_target"></param>
        public void DealDamage(Entity _dealer, Entity _target)
        {
            SetDealerEntity(_dealer);
            SetTargetEntity(_target);
            UpdateInstanceEntity();
            DealDamage();
        }

        /// <summary>
        /// Ignores all triggers and directly performs all actions. An integer hash is required for caching the result.
        /// </summary>
        /// <param name="_hash"></param>
        public void PerformAction(int _hash)
        {
            if (!inited) return;
            mRuntime.Hash = _hash;
            currentRootInstance.RunActions(this, mRuntime);
        }
        /// <summary>
        /// Returns whether all trigger nodes are triggered. An integer hash is required for caching the result.
        /// </summary>
        /// <param name="_hash"></param>
        /// <returns></returns>
        public bool isTriggered(int _hash)
        {
            if (!inited) return false;
            ResetRuntime();
            mRuntime.Hash = _hash;
            bool _result = true;
            
            foreach (var obj in currentRootInstance.ActiveTriggers)
            {
                if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<CombatDamage> -> isTriggered:" +  obj.Uid );
                if (!obj.isActive(mRuntime)) _result = false;
            }
            return _result;
        }
        /// <summary>
        /// Calculates the triggers. If all are triggered, it performs all actions and returns the trigger result.
        /// </summary>
        /// <returns></returns>
        public bool IfTriggerThenPerformAction()
        {
            int _hash = currentRootInstance.GenerateInstanceHash();
            mRuntime.Hash = _hash;
            bool _triggered = isTriggered(_hash);
            if (_triggered) PerformAction(_hash);
#if UNITY_EDITOR
            if (SGD_Settings.Instance.GraphDebugMode)
            {
                StartCoroutine(DebugCo(mRuntime, currentRootInstance.GetLatestDelay()));
                Debug.Log(currentRootInstance.GenerateGraphDebugText("****This is a debug text of [" + gameObject.name + "] DealDamage() with GraphObject[" + mGraph.name + "], copy this text and paste in the Debug panel of 'Graph View' window.****", mRuntime));
            }
#endif
            return _triggered;
        }
        #endregion

        IEnumerator DebugCo(GraphRuntimeState _runtime, float _delay)
        {
            yield return new WaitForSeconds(_delay);
            yield return 1;
            Debug.Log(currentRootInstance.GenerateGraphDebugText("****This is a debug text of [" + gameObject.name + "] DealDamage() with GraphObject[" + mGraph.name + "], copy this text and paste in the Debug panel of 'Graph View' window.****", _runtime));
        }
    }
}
