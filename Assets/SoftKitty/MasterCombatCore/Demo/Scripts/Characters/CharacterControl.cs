using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore.Demo
{
    
    public class CharacterControl : MonoBehaviour
    {
        #region Variables
        public float MotionSpeed = 1F;
        public CombatDamage SwordDamageScript;
        public CombatDamage ShieldDamageScript;
        public AudioClip[] FootstepSounds;
        public AudioClip LandSound;
        public AudioClip HeavyLandSound;
        public AudioClip ShieldSound;
        public AudioClip[] AttackSounds;
        public AudioClip DeathSound;
        public AudioClip[] HitSounds;
        public AudioClip[] BlockSounds;
        public AudioClip CriticalSound;
        public ParticleSystem VFX_HeavyLand;
        public ParticleSystem [] VFX_Attack;
        public ParticleSystem FootDust;
        public ParticleSystem HitEffect;
        public ParticleSystem CriticalHitEffect;
        public ParticleSystem BlockEffect;
        public ParticleSystem DeathSmoke;
        public GameObject FrostModel;
        public Collider[] DisassemblyBody;
        public Collider[] DisassemblyArmL;
        public Collider[] DisassemblyArmR;
        public Collider[] DisassemblyLegL;
        public Collider[] DisassemblyLegR;
        public Collider[] DisassemblyWeapon;
        public Collider[] DisassemblyShield;
        public EntityComponent EntityInstance
        {
            get
            {
                if (mEntityInstance == null) mEntityInstance = GetComponent<EntityComponent>();
                return mEntityInstance;
            }
        }
        public Animator mAnimator
        {
            get
            {
                if (mAnim == null) mAnim = GetComponent<Animator>();
                return mAnim;
            }
        }

        public CharacterController mCharacterController
        {
            get
            {
                if (mCharCtrl == null) mCharCtrl = GetComponent<CharacterController>();
                return mCharCtrl;
            }
        }
        public bool Grounded = false;
        protected bool FreeFall = false;
        protected bool Jump = false;
        protected bool Attack = false;
        protected bool Block = false;
        protected float MovingSpeed = 0F;
        protected float Height = 0F;
        protected float RotationSpeed = 5F;
        protected float JumpPower = 0F;
        protected Vector3 forward = new Vector3(0F, 0F, 1F);
        protected AnimatorStateInfo st;
        protected AnimatorStateInfo st_next;
        protected Vector3 pushPower;
        protected float hitTimer = 0F;
        protected float hitLag = 0F;
        private RaycastHit groundHit;
        private Animator mAnim;
        private CharacterController mCharCtrl;
        private EntityComponent mEntityInstance;
        private Vector3 gravity;
        private Vector3 hitAngle;
        private Entity lastAttacker;
        protected bool Forzen = false;
        protected bool Chaos = false;
        protected bool Stun = false;
        private Dictionary<string, GameObject> mOteDic = new Dictionary<string, GameObject>();
        public Transform SpineBone
        {
            get
            {
                return mAnimator.GetBoneTransform(HumanBodyBones.Spine);
            }
        }
        public Transform ChestBone
        {
            get
            {
                return mAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }
        }
        public Transform HeadBone
        {
            get
            {
                return mAnimator.GetBoneTransform(HumanBodyBones.Head);
            }
        }
        public bool isJumping
        {
            get
            {
                return JumpPower > 0F || Jump;
            }
        }

        public bool isLocomotion
        {
            get
            {
                return st.IsTag("Locomotion");
            }
        }

        public bool isIdle
        {
            get
            {
                return st.IsTag("Locomotion") && MovingSpeed == 0F;
            }
        }

        public bool isAttacking
        {
            get
            {
                return st.IsTag("Attacking") || (mAnimator.IsInTransition(0) && st_next.IsTag("Attacking"));
            }
        }

        public bool isBlocking
        {
            get
            {
                return st.IsTag("Blocking") || (mAnimator.IsInTransition(0) && st_next.IsTag("Blocking"));
            }
        }

        public bool isAction
        {
            get
            {
                return st.IsTag("Action") || (mAnimator.IsInTransition(0) && st_next.IsTag("Action"));
            }
        }

        public bool isLanding
        {
            get
            {
                return st.IsTag("Land") || (mAnimator.IsInTransition(0) && st_next.IsTag("Land"));
            }
        }

        public bool isHeavyLanding
        {
            get
            {
                return st.IsTag("HeavyLand") || (mAnimator.IsInTransition(0) && st_next.IsTag("HeavyLand"));
            }
        }
        #endregion

        #region MonoBehaviour

        public virtual void Start()
        {
            HealthBarManager.CreateBar(EntityInstance);
            EntityInstance.RegisterEntityEventCallback(OnEntityEvent);
            EntityInstance.RegisterOverTimeChangeCallback(OnOverTimeChange);
            MovementSpeedBuff.Observe(EntityInstance.mData);
        }
        
        public virtual void FixedUpdate()
        {
            if (!EntityInstance.AvailableForInteraction) return;
            Movement();
        }
        public virtual void Update()
        {
            if (!EntityInstance.AvailableForInteraction) return;
            pushPower = Vector3.Lerp(pushPower, Vector3.zero, Time.deltaTime * 20F);
            if (Physics.SphereCast(transform.position + Vector3.up * 1.6F, mCharacterController.radius + mCharacterController.skinWidth, Vector3.down, out groundHit, 50F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                Height = groundHit.distance - 1.6F + mCharacterController.radius + mCharacterController.skinWidth;
            }
            else
            {
                Height = 50F;
            }
            Grounded = (Height < 0.1F);
            FreeFall = (Height > 5F);
            if (st.IsTag("InAir"))
            {
                Jump = false;
                JumpPower = Mathf.Lerp(JumpPower, 0F, Time.deltaTime);
            }
            else if ((st.IsTag("Land") || isHeavyLanding) && !Jump && !mAnimator.IsInTransition(0))
            {
                JumpPower = 0F;
            }

            AnimatorSet();
            StatUpdate();

        }
        public virtual void LateUpdate()
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (!isBlocking && !isAction)
                Rotation();
            else
                ResetRotation();
            hitLag = Mathf.MoveTowards(hitLag, 0F, Time.deltaTime * 6F);
            hitTimer = Mathf.Min(hitTimer, 0.7F);
            if (hitTimer > 0F)
            {
                hitTimer = Mathf.MoveTowards(hitTimer, 0F, Time.deltaTime);
                SpineBone.localEulerAngles += Vector3.Lerp(Vector3.zero, hitAngle * 0.3F, Mathf.Min(hitTimer, 0.5F) * 2F);
                ChestBone.localEulerAngles += Vector3.Lerp(Vector3.zero, hitAngle * 0.7F, Mathf.Min(hitTimer, 0.5F) * 2F);
            }
        }
        private void OnAnimatorMove()
        {
            if (!isAttacking && !isBlocking && !isAction) return;
            mCharacterController.Move(mAnimator.deltaPosition);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (isBlocking || isIdle)
            {
                float _hipY = mAnimator.GetBoneTransform(HumanBodyBones.Hips).position.y;
                float _feetLPos = FootIK(mAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), AvatarIKGoal.LeftFoot);
                float _feetRPos = FootIK(mAnimator.GetBoneTransform(HumanBodyBones.RightFoot), AvatarIKGoal.RightFoot);
            }
            else
            {
                mAnimator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0F);
                mAnimator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0F);
            }
        }
        private void OnDestroy()
        {
            EntityInstance.UnRegisterEntityEventCallback(OnEntityEvent);
            EntityInstance.UnRegisterOverTimeChangeCallback(OnOverTimeChange);
        }
        #endregion

        #region Animation Events
        void OnFootstep()
        {
            if (FootstepSounds.Length > 0) GameManager.PlaySound3D(FootstepSounds[Random.Range(0, FootstepSounds.Length)], 0.7F, transform.position);
            FootDust.transform.position = new Vector3(transform.position.x, groundHit.point.y, transform.position.z);
            if (!isAttacking) FootDust.Emit(Mathf.FloorToInt((MovingSpeed + 3F) / 11F * Random.Range(8, 10)));
        }

        void OnLand()
        {
            if (LandSound) GameManager.PlaySound3D(LandSound, 0.7F, transform.position);
            FootDust.Emit(Mathf.FloorToInt((MovingSpeed + 3F) / 11F * Random.Range(18, 25)));
        }
        public virtual void HeavyLand()
        {
            VFX_HeavyLand.transform.position = new Vector3(VFX_HeavyLand.transform.position.x, groundHit.point.y, VFX_HeavyLand.transform.position.z);
            VFX_HeavyLand.Play();
            GameManager.PlaySound3D(HeavyLandSound, 1F, transform.position);
        }

        public virtual void AttackVFX(int _id)
        {
            if (mAnimator.IsInTransition(0)) return;
            VFX_Attack[_id].Stop();
            VFX_Attack[_id].Play();
        }

        public void ShiledOn()
        {
            GameManager.PlaySound3D(ShieldSound, 1F, transform.position);
        }

        private void AttackSound(int _id)
        {
            if (!mAnimator.IsInTransition(0) || _id == 0)
                GameManager.PlaySound3D(AttackSounds[_id], 1F, transform.position + Vector3.up);
        }
        private void SwordDamageEnable(float _multiplier)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (SwordDamageScript && isAttacking && !mAnimator.IsInTransition(0))
            {
                SwordDamageScript.SetDynamicValue("TargetOTE", EntityInstance.GetCustomInt("hit_dot"));
                SwordDamageScript.SetDynamicValue("DealerOTE", EntityInstance.GetCustomInt("hit_buff"));
                SwordDamageScript.SetDynamicValue("dmgMulti", _multiplier);
                SwordDamageScript.SetEnable(0.1F);
            }
        }

        private void ShieldDamageEnable(float _multiplier)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (ShieldDamageScript && (isAttacking || isHeavyLanding) && !mAnimator.IsInTransition(0))
            {
                ShieldDamageScript.SetDynamicValue("TargetOTE", EntityInstance.GetCustomInt("hit_dot"));
                ShieldDamageScript.SetDynamicValue("DealerOTE", EntityInstance.GetCustomInt("hit_buff"));
                ShieldDamageScript.SetDynamicValue("dmgMulti", _multiplier);
                ShieldDamageScript.SetEnable(0.1F);
            }
        }

        #endregion

        #region Callbacks
        public void OnOverTimeChange(string _uid, OverTimeEffectData _data, OverTimeEffectEventType _eventType)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (_eventType == OverTimeEffectEventType.Add || _eventType == OverTimeEffectEventType.Update)
            {
                if (!mOteDic.ContainsKey(_uid))
                {
                    GameObject _prefab = GameManager.GetOverTimeEffect(_uid).GetCustomObject<GameObject>("vfx");
                    if (_prefab != null)
                    {
                        GameObject _newOte = Instantiate(_prefab, mAnimator.GetBoneTransform(HumanBodyBones.UpperChest).transform);
                        _newOte.transform.localPosition = Vector3.zero;
                        _newOte.transform.localEulerAngles = Vector3.zero;
                        _newOte.transform.localScale = Vector3.one;
                        mOteDic.Add(_uid, _newOte);
                    }
                }
            }
            else
            {
                GameObject _ote = null;
                mOteDic.TryGetValue(_uid, out _ote);
                if (_ote != null)
                {
                    _ote.GetComponent<ParticleSystem>().Stop();
                    Destroy(_ote,1F);
                    mOteDic.Remove(_uid);
                }
            }
        }

        public void OnEntityEvent(string _eventName, float _floatArg, Entity _entityArg, bool _boolArg)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (_eventName == "Damage")
            {
                Damage(_floatArg, _entityArg, _boolArg);
            }
        }
        #endregion

        #region Graph Events
        public virtual void Damage(float _value, Entity _dealer, bool _critical)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (_dealer != null) lastAttacker = _dealer;
            if (_value <= 0F) return;
            if (_critical)
            {
                CriticalHitEffect.Play();
                GameManager.PlaySound3D(CriticalSound, 1F, transform.position + Vector3.up);
            }
            Vector3 _dir = -transform.forward;
            if (_dealer != null)
            {
                _dir = transform.position - _dealer.Position;
                if (GameManager.GetEntityInstance(_dealer.uid) != null) GameManager.GetEntityInstance(_dealer.uid).SendMessage("OnSuccessfulAttack", EntityInstance.mData, SendMessageOptions.DontRequireReceiver);
            }
            _dir.y = 0F;
            Vector3 targetDirection = SpineBone.InverseTransformDirection(Vector3.Lerp(_dir.normalized, Vector3.up,  (_dealer != null)? 0.4F:0.6F));
            hitAngle.x = Mathf.Atan2(targetDirection.z, targetDirection.y) * Mathf.Rad2Deg;
            hitAngle.z = -Mathf.Atan2(targetDirection.x, targetDirection.y) * Mathf.Rad2Deg;
            hitAngle.y = 0F;
            hitTimer = (_dealer != null)?0.5F:0.2F;
            HitEffect.transform.forward = -_dir.normalized;
            if (_dealer != null)
            {
                float _power= Mathf.Clamp(Mathf.Abs(_value) / EntityInstance.GetAttributeFloat("hp") * 150F,0F,15F);
                pushPower = _dir.normalized * _power;
                HitEffect.Stop();
                HitEffect.Play();
            }
            else
            {
                HitEffect.Emit(5);
            }
            GameManager.PlaySound3D(HitSounds[Random.Range(0, HitSounds.Length)], (_dealer != null)? 1F:0.3F, transform.position + Vector3.up);

            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<CharacterControl> Entity<" + EntityInstance.uid+">Take Damage: "+_value);
            
        }
        #endregion

        #region Graph Message (by SendMessage Action node)
        public virtual void BlockAttack(Entity _dealer)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<CharacterControl> Entity<" + EntityInstance.uid + "> BlockAttack");
            if (isBlocking)mAnimator.SetTrigger("Hit");
            if (_dealer != null)
            {
                if (GameManager.GetEntityInstance(_dealer.uid) != null) GameManager.GetEntityInstance(_dealer.uid).SendMessage("OnAttackBlock", EntityInstance.mData, SendMessageOptions.DontRequireReceiver);
            }
            BlockEffect.Stop();
            BlockEffect.Play();
            GameManager.PlaySound3D(BlockSounds[Random.Range(0, BlockSounds.Length)], 1F, transform.position + Vector3.up);
        }


        public virtual void OnSuccessfulAttack(Entity _victim)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if(Vector3.Distance(_victim.Position,transform.position)<3F)hitLag = 2F;
        }

        public virtual void OnAttackBlock(Entity _victim)
        {
            if (!EntityInstance.AvailableForInteraction) return;
            if (Vector3.Distance(_victim.Position, transform.position) > 3F) return;
            Vector3 _dir = transform.position- _victim.Position;
            _dir.y = 0F;
            pushPower = _dir * 5F;
            hitLag = 0F;
            hitTimer = 0F;
            EntityInstance.InterruptCast();
            mAnimator.SetTrigger("AttackFail");
        }
        #endregion

        #region Internal Methods
        public virtual void StatUpdate()
        {
            float _hp = EntityInstance.GetAttributeFloat("chp");
            float _mhp = EntityInstance.GetAttributeFloat("hp");
            if(_hp> _mhp) EntityInstance.SetAttributeValue("chp", _mhp);
            if (EntityInstance.AvailableForInteraction && _hp <= 0)//Death
            {
                EntityInstance.InterruptCast();
                OnDeath(lastAttacker);
                EntityInstance.AvailableForInteraction = false;
                EntityInstance.SetAttributeValue("chp", 0F);
                mAnimator.enabled = false;
                mCharacterController.enabled = false;
                SwordDamageScript.Enable = false;
                ShieldDamageScript.Enable = false;
                
                Vector3 _force = -transform.forward;
                if (lastAttacker != null) _force = (transform.position - lastAttacker.Position).normalized * 12F;
                Disassemble(DisassemblyWeapon, _force);
                Disassemble(DisassemblyShield, _force);
                Disassemble(DisassemblyArmL, _force);
                Disassemble(DisassemblyArmR, _force);
                Disassemble(DisassemblyLegL, _force);
                Disassemble(DisassemblyLegR, _force);
                Disassemble(DisassemblyBody, _force);
                DeathSmoke.Stop();
                DeathSmoke.Play();
                Destroy(gameObject, 5.5F);
            }
            EntityInstance.SetAttributeValue("block", isBlocking?60F:0F);//Blocking
            Stun = EntityInstance.GetAttributeFloat("stun") > 0F;//Stun
            Forzen = EntityInstance.GetAttributeFloat("forzen") > 0F;//Forzen
            Chaos = EntityInstance.GetAttributeFloat("chaos") > 0F;//Chaos
            FrostModel.SetActive(Forzen);
            EntityInstance.mData.SetCustomInt("animation", mAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash);
            EntityInstance.mData.SetCustomFloat("ani_time",Mathf.Clamp01(mAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime));
        }

        public virtual void OnDeath(Entity _attacker)
        {

        }

        private void LoadAnimation()
        {
            int _hash = EntityInstance.mData.GetCustomInt("animation");
            float _time= EntityInstance.mData.GetCustomFloat("ani_time");
            if (_hash != -1)
            {
                mAnimator.Play(_hash,0, _time);
            }
        }


        private void Disassemble(Collider [] _colliders, Vector3 _force)
        {
            foreach (var obj in _colliders)
            {
                obj.isTrigger = false;
                obj.gameObject.layer = gameObject.layer;
                if (obj.GetComponent<DamageReciever>()) obj.GetComponent<DamageReciever>().Enable = false;
                if (obj.GetComponent<CombatDamage>()) obj.GetComponent<CombatDamage>().Enable = false;
            }
            
            _colliders[0].transform.SetParent(null, true);
            if (_colliders[0].gameObject.GetComponent<CombatDamage>()) DestroyImmediate(_colliders[0].gameObject.GetComponent<CombatDamage>());
            _colliders[0].isTrigger = false;
            _colliders[0].enabled = true;
            Rigidbody _rigi= _colliders[0].gameObject.GetComponent<Rigidbody>();
            if (_rigi == null)
                _rigi = _colliders[0].gameObject.AddComponent<Rigidbody>();
            else
            {
                _rigi.isKinematic = false;
                _rigi.useGravity = true;
            }
            _rigi.AddTorque(new Vector3(Random.Range(-5F,5F), Random.Range(-5F, 5F), Random.Range(-5F, 5F)), ForceMode.Impulse);
            _rigi.AddForce(_force * Random.Range(0.3F, 1F) + (_colliders[0].transform.position-(transform.position+Vector3.up)) *Random.Range(0.5F,1F), ForceMode.Impulse);
            Destroy(_colliders[0].gameObject, 5F);
        }

       


        private float FootIK(Transform foot, AvatarIKGoal _ik)
        {
            RaycastHit hit;
            float _groundY = transform.position.y;

            if (Physics.Raycast(foot.position + Vector3.up * 1F, -Vector3.up, out hit, 2F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore)) {
                _groundY = hit.point.y + 0.1F;
            }
            bool _isOnGround = foot.position.y - transform.position.y < 0.2F;
            if ((_isOnGround && foot.position.y + 0.1F - 0.05F > _groundY) || _groundY > foot.position.y + 0.15F)
            {
                Vector3 _fixedPos = new Vector3(foot.position.x, _groundY, foot.position.z);
                mAnimator.SetIKPosition(_ik, _fixedPos);
                mAnimator.SetIKPositionWeight(_ik, 1F);
            }
            else
            {
                mAnimator.SetIKPositionWeight(_ik, Mathf.MoveTowards(mAnimator.GetIKPositionWeight(_ik), 0F, Time.deltaTime * 10F));
            }
            return _groundY;
        }
        #endregion

        #region Movement & Rotate
        protected bool DoJump()
        {
            if (!EntityInstance.mData.AvailableForInteraction || !isLocomotion || mAnimator.IsInTransition(0) || isJumping) return false;
            Jump = true;
            gravity = Vector3.zero;
            JumpPower = 12F;
            FootDust.Emit(Mathf.FloorToInt((MovingSpeed + 3F) / 11F * Random.Range(18, 25)));
            return true;
        }
        public virtual void Rotation()
        {
            if (Forzen || Stun) return;
            if (forward == Vector3.zero) forward = transform.forward;
            forward.y = 0F;
            float _angleDiff =  Vector3.Angle(transform.forward, forward);
            Vector3 _cross  = Vector3.Cross(forward, transform.forward);
            if (_cross.y < 0) _angleDiff = -_angleDiff;
            Vector3 _up = Vector3.up;
            if (!isAttacking && !isLanding && Grounded && !isHeavyLanding)
            {
                if (_angleDiff < 0F)
                {
                    _up = Vector3.Lerp(Vector3.up, transform.right, Mathf.Abs(_angleDiff) / 90F * MovingSpeed / 8F);
                }
                else
                {
                    _up = Vector3.Lerp(Vector3.up, -transform.right, Mathf.Abs(_angleDiff) / 90F * MovingSpeed / 8F);
                }
            }
            Quaternion _rot = Quaternion.LookRotation(forward.normalized, _up);
            transform.rotation = Quaternion.Slerp(transform.rotation, _rot, Time.deltaTime* RotationSpeed);
        }

        public virtual void ResetRotation()
        {
            Vector3 _forward = transform.forward;
            _forward.y = 0F;
            Quaternion _rot = Quaternion.LookRotation(_forward.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, _rot, Time.deltaTime * RotationSpeed*2F);
        }

       

        public virtual void Movement()
        {
            if (Forzen || Stun) return;
            if (!Grounded)
            {     
                gravity +=  (JumpPower - 9.8F) * Time.fixedDeltaTime* Vector3.up;
            }
            else
            {
                gravity = (JumpPower - 9.8F) * Time.fixedDeltaTime * Vector3.up;
            }
            float _speed = mAnimator.GetFloat("Speed");
            if (!EntityInstance.mData.AvailableForInteraction || isAttacking || isBlocking || (!isLocomotion && !isLanding && Grounded))
            {
                MovingSpeed = 0F;
                _speed = 0F;
            }
            if (isJumping) _speed *= 1.5F;
            mCharacterController.Move(transform.forward * _speed * Mathf.Max(1F,JumpPower/8F)* mAnimator.speed* MotionSpeed * EntityInstance.GetAttributeFloat("speed") * 0.01F * Time.fixedDeltaTime+ gravity+ pushPower*Time.fixedDeltaTime);
        }
        #endregion

        #region Animator Variables
        public virtual void AnimatorSet()
        {
            mAnimator.SetBool("Stun", Stun);
            mAnimator.SetFloat("MotionSpeed", MotionSpeed* EntityInstance.GetAttributeFloat("speed")*0.01F);
            mAnimator.SetFloat("Speed",MovingSpeed,0.2F,Time.deltaTime);
            mAnimator.SetBool("Grounded", Grounded);
            mAnimator.SetBool("FreeFall", FreeFall);
            mAnimator.SetBool("Jump", Jump);
            mAnimator.SetBool("Attack", Grounded && Attack && EntityInstance.mData.AvailableForInteraction);
            mAnimator.SetBool("Block", Block);
            mAnimator.speed = Forzen ? 0F: Mathf.Clamp( 1F - Mathf.Min(hitTimer,0.5F) * 2F- hitLag,0.1F,1F)* (1F+EntityInstance.GetAttributeFloat("agi")*0.01F);
            st = mAnimator.GetCurrentAnimatorStateInfo(0);
            st_next = mAnimator.GetNextAnimatorStateInfo(0);
        }
        #endregion

    }
}
