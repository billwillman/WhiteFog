using UnityEngine;

namespace SoftKitty.MasterCombatCore.Demo
{
    public class PlayerControl : CharacterControl
    {
        public CameraControl mCamera;
        public AudioClip LevelUpSound;
        public GameObject EnergyShield;
        public GameObject Projectile;
        public Light BuffLight;
        private float BuffTimer = 0F;
        private float h = 0F;
        private float v = 0F;
        private bool sprint = false;
        private float AttackTimer = 0F;


        public override void OnDeath(Entity _attacker)
        {
            DemoManager.instance.ToggleGameOver();
        }
        public void AddBuff(Color _color)
        {
            BuffLight.color = _color;
            BuffTimer = 20F;
        }

   
        public override void Damage(float _value, Entity _dealer, bool _critical)
        {
            base.Damage(_value, _dealer, _critical);
            mCamera.Shake = _critical?Random.Range(1F, 1.2F): Random.Range(0.5F, 0.7F);
        }

        public override void OnSuccessfulAttack(Entity _victim)
        {
            base.OnSuccessfulAttack(_victim);
            mCamera.Shake = Mathf.Clamp01(4F- Vector3.Distance(_victim.Position, transform.position))* Random.Range(0.5F, 0.7F);
        }

        public override void OnAttackBlock(Entity _victim)
        {
            base.OnAttackBlock(_victim);
            mCamera.Shake = Mathf.Clamp01(4F - Vector3.Distance(_victim.Position, transform.position)) * Random.Range(1F, 1.2F);
        }

        public override void HeavyLand()
        {
            base.HeavyLand();
            mCamera.Shake = Random.Range(0.5F, 1F);
        }

        public override void AttackVFX(int _id)
        {
            base.AttackVFX(_id);
            if (mAnimator.IsInTransition(0)) return;
            if (EntityInstance.GetCustomInt("specskill")==1)
            {
                var volley = CombatRandom.Event("volley", EntityInstance.mData.CombatRandomId, null, null).With((uint)_id);
                for (int i = 0; i < _id + 1; i++)
                {
                    var shot = volley.With((uint)i);
                    var origin = SwordDamageScript.transform.position;
                    origin.y = Projectile.transform.position.y;
                    var offset = new Vector3(shot.With("x").Range(-.5f, .5f), shot.With("y").Range(-.1f, .5f), shot.With("z").Range(-.5f, .5f));
                    GameObject _newProjectile = Instantiate(Projectile, origin + offset, Projectile.transform.rotation);
                    _newProjectile.SetActive(true);
                    _newProjectile.GetComponent<CombatDamage>().SetDynamicValue("TargetOTE", EntityInstance.GetCustomInt("hit_dot"));
                    _newProjectile.GetComponent<CombatDamage>().SetDynamicValue("DealerOTE", EntityInstance.GetCustomInt("hit_buff"));
                }
            }
            mCamera.Shake = Random.Range(0.4F, 0.7F)*(0.5F+ _id*0.2F);
        }

        public override void Update()
        {
            base.Update();
            BuffTimer = Mathf.MoveTowards(BuffTimer,0F,Time.deltaTime);
            if (BuffLight.gameObject.activeSelf != (BuffTimer > 0F)) BuffLight.gameObject.SetActive(BuffTimer > 0F);
            EnergyShield.SetActive(EntityInstance.GetAttributeFloat("shield")>0F);
            if (EnergyShield.activeSelf) EnergyShield.transform.localEulerAngles += transform.forward * Time.deltaTime;
            if (Stun || Forzen) return;
            if (InputProxy.GetKey(KeyCode.A))
            {
                h = Mathf.MoveTowards(h, -1F, Time.deltaTime * 2.5F);
            }
            else if (InputProxy.GetKey(KeyCode.D))
            {
                h = Mathf.MoveTowards(h, 1F, Time.deltaTime * 2.5F);
            }
            else
            {
                h = Mathf.MoveTowards(h, 0F, Time.deltaTime * 5F);
            }
            if (InputProxy.GetKey(KeyCode.S))
            {
                v = Mathf.MoveTowards(v, -1F, Time.deltaTime * 2.5F);
            }
            else if (InputProxy.GetKey(KeyCode.W))
            {
                v = Mathf.MoveTowards(v, 1F, Time.deltaTime * 2.5F);
            }
            else
            {
                v = Mathf.MoveTowards(v, 0F, Time.deltaTime * 5F);
            }
            AttackTimer = Mathf.MoveTowards(AttackTimer, 0F, Time.deltaTime);
            sprint = InputProxy.GetKey(KeyCode.LeftShift);
            if(!isJumping)mCamera.fovAdd =isBlocking?-20F:( sprint ? 10F : 0F);
            if (InputProxy.GetKeyDown(KeyCode.Space)) {
                if(DoJump() && isMoving) mCamera.fovAdd=20F;
            }
            if (InputProxy.GetMouseButton(0)) AttackTimer = 0.1F;
            Attack = (AttackTimer>0F);
            Block= (InputProxy.GetMouseButton(1) && !Attack);
            forward = (mCamera.Cam.transform.forward * v+ mCamera.Cam.transform.right*h).normalized;
            MovingSpeed =isMoving ? (sprint ? 6F : 2F) : 0F;
            if (EntityInstance.GetAttributeInt("xp") >= EntityInstance.GetAttributeInt("mxp"))
            {
                int _newLevel= EntityInstance.AddAttributeValue("lvl", 1);
                EntityInstance.AddAttributeValue("xp", -EntityInstance.GetAttributeInt("mxp"));
                EntityInstance.SetAttributeValue("mxp", 100 * _newLevel);
                EntityInstance.mData.AttributesUpgradeLevel++;
                EntityInstance.SetAttributeValue("chp", EntityInstance.GetAttributeInt("hp"));
                GameManager.PlaySound2D(LevelUpSound,1F);
                DemoUi.instance.PlayLevelUpFullScreen(_newLevel);
            }
        }

        private bool isMoving
        {
            get
            {
                return (h != 0F || v != 0F);
            }
        }
    }
}
