using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class NpcControl : CharacterControl
    {
        public bool DebugGizmos = true;
        [Range(0F,1F)]
        public float Intelligence = 0F;
        public List<string> AlliesTags = new List<string>();
        public List<string> EnemiesTags = new List<string>();
        public LayerMask CharacterLayerMask;
        public float SightRange = 30F;
        public bool SightBlockCheck = true;
        public float AttackDistance = 3F;
        public List<EntityComponent> trackingEntities = new List<EntityComponent>();
        private float distanceFromPlayer = 100F;
        private float jumpTimeStamp = 0F;
        private int randomDecision=0;
        private Vector3 patrolTarget=new Vector3(0F,0F,0F);
        private float BlockTimer = 0F;

        public override void Update()
        {
           
            base.Update();
            if (!EntityInstance.AvailableForInteraction) return;
            BlockTimer = Mathf.MoveTowards(BlockTimer,0F,Time.deltaTime);
            if (Stun || Forzen) return;
            if (GameManager.GetPlayer() != null) {
                distanceFromPlayer = Vector3.Distance(GameManager.GetPlayer().Position, transform.position);
            }
            int _decisionInterval = 30;
            if (distanceFromPlayer > 200F)//Very far from player, lazy decision every 5 seconds
            {
                _decisionInterval = 300;
            }
            else if (distanceFromPlayer > 100F)//Middle range distance from player, lazy decision every 2 seconds
            {
                _decisionInterval = 120;
            }
            else if (distanceFromPlayer > 50F)//Close from player, active decision every second
            {
                _decisionInterval = 60;
            }

            if (Time.frameCount % _decisionInterval == 0 )
            {
                if(!EntityInstance.Engage) SearchForBattle();//Pay attention for possible battles.
                randomDecision = Random.Range(0, 100);//Set a random number to add some randomness for decisions.
                if (!EntityInstance.Engage)
                {
                    if (trackingEntities.Count > 0)
                    {
                        DealWithNoticedEnemies();
                    }
                    else//When no possible battles around, randomly walk around
                    {
                        Patrol();
                    }
                }
            }
            
            if (EntityInstance.Engage)
            {
                if (EntityInstance.EngagedEntity != null && EntityInstance.EngagedEntity.AvailableForInteraction)
                {
                    float _dis = Vector3.Distance(EntityInstance.EngagedEntity.transform.position, transform.position);
                    if (_dis < SightRange + 15F)
                    {
                        EnageWithTarget(EntityInstance.EngagedEntity);
                    }
                    else
                    {
                        EntityInstance.Engage = false;
                        EntityInstance.EngagedEntity = null;
                    }
                }
                else
                {
                    Block = false;
                    Attack = false;
                    EntityInstance.Engage = false;
                    EntityInstance.EngagedEntity = null;
                }
            }
        }

        public virtual void Patrol()
        {
 
            float _dis = Vector3.Distance(transform.position, new Vector3(patrolTarget.x,transform.position.y, patrolTarget.z));
            if (patrolTarget == Vector3.zero || _dis<=10F) PickNewPatrolPoint();
            if (_dis > 10F)
            {
                Vector3 _dir = patrolTarget - transform.position;
                _dir.y = 0F;
                _dir.Normalize();
                forward = _dir;
                MovingSpeed = 2F;
                Attack = false;
                Block = false;
                RaycastHit _terrainHit;
                bool _terrainJump = false;
                if (MovingSpeed >= 2F && Physics.Raycast(transform.position + Vector3.up * mCharacterController.stepOffset, forward, out _terrainHit, 0.5F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                {
                    if (_terrainHit.normal.y < (1F - (mCharacterController.slopeLimit - 15) / 90F)) _terrainJump = true;
                }
                if (Time.time > jumpTimeStamp + 1F && !isJumping && _terrainJump)
                {
                    MovingSpeed = 6F;
                    if (DoJump()) jumpTimeStamp = Time.time;
                }
            }
            else
            {
                MovingSpeed = 0F;
                Attack = false;
                Block = false;
            }
        }

        private void PickNewPatrolPoint() {
            if (randomDecision < 60) return;
            Vector3 _point = transform.position + new Vector3(Random.Range(15F, 30F) * (Random.Range(0, 2) == 0 ? 1 : -1), 100F, Random.Range(15F, 30F) * (Random.Range(0, 2) == 0 ? 1 : -1));
            RaycastHit _patrolHit;
            if (Physics.Raycast(_point,Vector3.down,out _patrolHit, 200F,SGD_Settings.Instance.GroundLayer,QueryTriggerInteraction.Ignore)) {
                if(Mathf.Abs(transform.position.y- _patrolHit.point.y)<5F && _patrolHit.normal.y>0.8F) patrolTarget = _patrolHit.point;
            }
        }

        public virtual void DealWithNoticedEnemies()
        {
            for (int i = trackingEntities.Count - 1; i >= 0; i--)//Start with the newest
            {//Deal with noticed enemies
                if (trackingEntities[i] == null || !trackingEntities[i].AvailableForInteraction)//Enemy is already Dead.
                {
                    trackingEntities.RemoveAt(i);
                }
                else
                {
                    float _dis = Vector3.Distance(trackingEntities[i].transform.position, transform.position);
                    if (_dis > SightRange + 5F)//Enemy runs too far from us
                    {
                        trackingEntities.RemoveAt(i);
                    }
                    else//pursue and attack
                    {
                        EnageWithTarget(trackingEntities[i]);
                    }
                }
            }
        }

        private float GetAngle(Vector3 _target,Vector3 _pos, Vector3 _dir) {
            Vector3 _d = _target - _pos;
            _d.y = 0F;
            _d.Normalize();
            return Vector3.Angle(_dir, _d);
        }

        private void EnageWithTarget(EntityComponent _target)
        {
            Vector3 _targetPos = _target.transform.position;
            bool _targetBlocking = false;
            bool _targetAttacking= false;
            if (_target.GetComponent<CharacterControl>()) {
                if (_target.GetComponent<CharacterControl>().isBlocking && GetAngle(transform.position, _target.transform.position, _target.transform.forward) < 40F) _targetBlocking = true;
                if (_target.GetComponent<CharacterControl>().isAttacking) _targetAttacking = true;
            }
            if (_targetBlocking && randomDecision > 30- Intelligence*30) _targetPos = _target.transform.position - _target.transform.forward * 2F+ (randomDecision < 50? _target.transform.right: -_target.transform.right) *2F;//When target is blocking, run to his back
            float _dis = Vector3.Distance(_targetPos, transform.position);
            Vector3 _dir = _targetPos - transform.position;
            _dir.y = 0F;
            _dir.Normalize();
            float _heightDiff = _target.transform.position.y - transform.position.y;
            float _mAngle = Vector3.Angle(transform.forward, _dir);
            float _tAngle= Vector3.Angle(_target.transform.forward, -_dir);

            if (randomDecision>20+ Intelligence*50 && !_targetBlocking && isBlocking && (_mAngle > 45F || _dis>5F)) Block = false;//Cancel block if target is too far away or run to our side.

            if (BlockTimer>0F || ( randomDecision < 10+ Intelligence*80 && !_targetBlocking && _targetAttacking && _tAngle < 15F && Grounded && _dis < AttackDistance + 1F && _mAngle < 15F && Mathf.Abs(_heightDiff) < 2F))
            {//When target is attacking, block the attack
                MovingSpeed = 0F;
                Attack = false;
                Block = true;
                if(BlockTimer<=0F) BlockTimer = Random.Range(2F,3F);
                EntityInstance.Engage = true;
                EntityInstance.EngagedEntity = _target;
            }
            else if (randomDecision < 30+ Intelligence*60 && !_targetBlocking && _dis < AttackDistance && _mAngle < 15F && Mathf.Abs(_heightDiff) < 2F)//Attack when within attack distance
            {
                MovingSpeed = 0F;
                forward = _dir;
                Attack = true;
                EntityInstance.Engage = true;
                EntityInstance.EngagedEntity = _target;
            }
            else if (_dis > AttackDistance - 1F || _targetBlocking)//Run to enemy
            {
                MovingSpeed = 6F;
                forward = _dir;
                Attack = false;
                Block = false;
            }
            else if (_dis > 1F)//Walk to enemy
            {
                MovingSpeed = 2F;
                forward = _dir;
                Attack = false;
                Block = false;
            }
            else
            {
                MovingSpeed = 0F;
                forward = _dir;
                Attack = false;
                Block = false;
            }
            RaycastHit _terrainHit;
            bool _terrainJump = false;
            if (MovingSpeed>=2F && Physics.Raycast(transform.position+Vector3.up* mCharacterController.stepOffset, forward, out _terrainHit,0.5F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                if (_terrainHit.normal.y < (1F-(mCharacterController.slopeLimit-15)/90F)) _terrainJump = true;
            }
            if (Time.time> jumpTimeStamp+1F && !isJumping && !isAttacking && ((_heightDiff >= 2F && _target.GetComponent<CharacterControl>().Grounded) || _terrainJump)) {
                MovingSpeed = 6F;
                if (DoJump()) jumpTimeStamp= Time.time;
            }
        }

        public virtual void SearchForBattle()
        {
            Collider [] _cols = Physics.OverlapSphere(transform.position, SightRange, CharacterLayerMask, QueryTriggerInteraction.Collide);
            if (_cols.Length > 0)//Overlap a Sphere with radius of SightRange to get all character colliders in sight.
            {
                foreach (var obj in _cols) {
                    if (!obj.transform.IsChildOf(transform) && obj.GetComponent<DamageReciever>() && obj.GetComponent<DamageReciever>().Entity!=null)
                    {
                        float _dis = Vector3.Distance(obj.GetComponent<DamageReciever>().Entity.transform.position, transform.position);
                        float _sightAngle = GetAngle(obj.GetComponent<DamageReciever>().Entity.transform.position, transform.position, transform.forward);
                        //If SightBlockCheck is checked, Do a raycast check for anything between the target and the head bone.
                        try
                        {
                            if ((_sightAngle < 60F + Mathf.Clamp((10F - _dis) * 5F, 0F, 50F) || _dis < 3F) && (!SightBlockCheck || !Physics.Linecast(HeadBone.position, obj.GetComponent<DamageReciever>().Entity.transform.position + Vector3.up * 1.5F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore)))
                            {
                                bool _enemy = obj.GetComponent<DamageReciever>().Entity.hasMatchingTag(EnemiesTags);
                                bool _ally = obj.GetComponent<DamageReciever>().Entity.hasMatchingTag(AlliesTags);
                                //Debug.Log(obj.name+ "_enemy:"+ _enemy+ "  _ally:"+ _ally);
                                if (_enemy && !trackingEntities.Contains(obj.GetComponent<DamageReciever>().Entity))
                                {//Found an enemy
                                    trackingEntities.Add(obj.GetComponent<DamageReciever>().Entity);
                                }
                                else if (_ally && obj.GetComponent<DamageReciever>().Entity.Engage && obj.GetComponent<DamageReciever>().Entity.EngagedEntity != null)
                                {//Found an alley engaging with an enemy
                                    if (trackingEntities.Contains(obj.GetComponent<DamageReciever>().Entity.EngagedEntity)) trackingEntities.Add(obj.GetComponent<DamageReciever>().Entity.EngagedEntity);
                                }
                            }
                        }
                        catch
                        {

                        }
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!DebugGizmos) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, SightRange);
            if (patrolTarget!=Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(patrolTarget+ Vector3.up * 50F, patrolTarget-Vector3.up*150F);
            }
        }

    }
}
