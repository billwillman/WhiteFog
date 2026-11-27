using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace SoftKitty
{
    public sealed class SkillPlayback : MonoBehaviour
    {
        // Capture this handler per launch so an in-flight projectile keeps its own cast result.
        public System.Action<Entity, Entity> OriginalProjectileImpact { get; set; }

        public static void ApplyIncomingDamage(Entity source, Entity target, float amount)
        {
            if (target == null || !target.AvailableForInteraction || amount <= 0) return;
            var health = target.GetAttributeData("chp");
            if (health == null) return;
            ApplySettledDamage(source, target, health, SkillDefence.Incoming(health, amount));
        }

        internal static void ApplySettledDamage(Entity source, Entity target, AttributeData health, float amount)
        {
            float hp = target.GetAttributeFloat("chp");
            float loss = Mathf.Clamp(amount, 0, Mathf.Max(0, hp));
            if (loss <= 0) return;
            health.SetValue(hp - loss, source);
            target.OnEntityEvent("Damage", loss, source, false);
        }

        // Preserve the values used by previously authored graphs.
        public enum Style { Direct = 0, Sword = 1, Orb = 2, Mist = 3, Projectile = 4 }
        private int running;
        private float retireAt = float.PositiveInfinity;
        private static Material traceMaterial;
        public bool Busy => running > 0;

        public static SkillPlayback On(GraphInstance instance)
        {
            var host = instance.GetComponent<SkillPlayback>();
            return host ? host : instance.gameObject.AddComponent<SkillPlayback>();
        }

        private static readonly Dictionary<Scene, SkillPlayback> projectileHosts = new Dictionary<Scene, SkillPlayback>();
        private Scene projectileScene;

#if UNITY_EDITOR
        public static event System.Action<GameObject> TemporaryObjectCreated;
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void TrackTemporaryObject(GameObject obj)
        {
#if UNITY_EDITOR
            TemporaryObjectCreated?.Invoke(obj);
#endif
        }

        // A launched projectile lives in its scene, independently of the graph or actor that fired it.
        public SkillPlayback ProjectileHost()
        {
            var scene = gameObject.scene;
            if (projectileHosts.TryGetValue(scene, out var host) && host && host.isActiveAndEnabled) return host;
            var go = new GameObject("在途技能投射物");
            SceneManager.MoveGameObjectToScene(go, scene);
            host = go.AddComponent<SkillPlayback>();
            host.projectileScene = scene;
            projectileHosts[scene] = host;
            TrackTemporaryObject(go);
            return host;
        }

        private void OnDestroy()
        {
            if (projectileHosts.TryGetValue(projectileScene, out var host) && host == this)
                projectileHosts.Remove(projectileScene);
        }

        public void ScheduleReleases(GraphRuntimeState cast, Entity caster, float wait, int count, float interval, System.Action release)
        {
            Schedule(WaitForRelease(cast, caster, wait, count, interval, release));
        }

        private static IEnumerator WaitForRelease(GraphRuntimeState cast, Entity caster, float wait, int count,
            float interval, System.Action release)
        {
            float due = Time.time + Mathf.Max(0f, wait);
            for (int i = 0; i < count; i++)
            {
                // Keep the initial frame boundary of the graph's action scheduler.
                do
                {
                    yield return null;
                    if (cast.CastInterrupted || !caster.AvailableForInteraction) yield break;
                } while (Time.time < due);
                release();
                due = Time.time + Mathf.Max(.01f, interval);
            }
        }

        public static Entity ReadEntity(Action action, int port, GraphRuntimeState state)
        {
            var sources = action.inputPort[port].connectedNodes;
            return sources.Count == 0 ? null : sources[0].GetEntity(state);
        }

        public static EntityComponent FindActor(Entity entity)
            => GameManager.FindEntityInstance(entity);

        public static Vector3 Position(Entity entity, EntityComponent actor)
            => (actor ? actor.transform.position : entity.Position) + Vector3.up * 1.1f;

        public void RetireAfter(float seconds)
        {
            retireAt = Time.time + seconds;
            TrackTemporaryObject(gameObject);
        }
        private void LateUpdate()
        {
            if (!Busy && Time.time >= retireAt) Destroy(gameObject);
        }

        public void Schedule(IEnumerator routine) => StartCoroutine(Track(routine));
        private IEnumerator Track(IEnumerator routine)
        {
            running++;
            try { yield return routine; }
            finally { running--; }
        }

        private LineRenderer Trace(string label, Color color, float width, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = width;
            if (!traceMaterial) traceMaterial = Resources.Load<Material>("MccSkillTrace");
            line.sharedMaterial = traceMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            line.SetPropertyBlock(properties);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Vector3[] Ring(float radius, bool vertical)
        {
            var points = new Vector3[40];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / points.Length;
                float a = Mathf.Cos(angle) * radius, b = Mathf.Sin(angle) * radius;
                points[i] = vertical ? new Vector3(a, b, 0) : new Vector3(a, 0, b);
            }
            return points;
        }

        public void Signal(EntityComponent actor, Vector3 point, string label, float seconds, Color color)
            => Schedule(Pulse(actor, point, label, Mathf.Max(.05f, seconds), color));

        private IEnumerator Pulse(EntityComponent actor, Vector3 point, string label, float seconds, Color color)
        {
            var line = Trace(label, color, .06f, true);
            bool shield = label == "护盾";
            var shape = Ring(shield ? 1f : .75f, shield);
            line.positionCount = shape.Length;
            line.SetPositions(shape);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                line.transform.position = actor ? actor.transform.position + Vector3.up * (shield ? 1f : .22f) : point;
                line.transform.localScale = Vector3.one * (shield ? 1 : .65f + t / seconds);
                if (shield) line.transform.rotation = Quaternion.Euler(0, t * 170, 0);
                yield return null;
            }
            Destroy(line.gameObject);
        }

        internal static bool CanSettle(Style style, Entity recipient, EntityComponent actor)
        {
            if (style == Style.Direct) return recipient.AvailableForInteraction && (!actor || actor.isActiveAndEnabled);
            return SkillProjectile.IsLive(actor) && actor.mData == recipient;
        }

        public IEnumerator Flight(Style style, Vector3 start, Entity target, EntityComponent actor, float seconds, float arc)
            => Flight(style, start, target, actor, seconds, arc, new SkillProjectile(), null, null);

        internal IEnumerator Flight(Style style, Vector3 start, Entity target, EntityComponent actor, float seconds, float arc,
            SkillProjectile hitBox, Entity source, System.Action<EntityComponent> onContact)
        {
            Color color = style switch
            {
                Style.Sword => Color.cyan,
                Style.Orb => new Color(1,.55f,.12f),
                Style.Projectile => Color.white,
                _ => new Color(.55f,1,.25f)
            };
            var line = Trace(style.ToString(), color, style == Style.Mist ? .17f : .09f, true);
            var shape = style == Style.Sword
                ? new[] { new Vector3(0,0,.75f), new Vector3(.12f,0,-.24f), new Vector3(.22f,0,-.31f), new Vector3(0,0,-.5f), new Vector3(-.22f,0,-.31f), new Vector3(-.12f,0,-.24f) }
                : Ring(style == Style.Mist ? .36f : .25f, true);
            line.positionCount = shape.Length;
            line.SetPositions(shape);
            Vector3 point = start;
            line.transform.position = start;
            float time = 0;
            var physics = gameObject.scene.GetPhysicsScene();
            var query = new SkillProjectile.HitQuery(hitBox, source, actor, physics);
            EntityComponent contact = null;
            Vector3 end = Position(target, actor);
            try
            {
                while (!query.designated || query.AimIsLive)
                {
                    float nextTime = Mathf.Min(seconds, time + Time.deltaTime);
                    float u = seconds > 0 ? nextTime / seconds : 1;
                    if (query.AimIsLive) end = Position(target, actor);
                    var next = Vector3.Lerp(start, end, u) + Vector3.up * (4 * arc * u * (1 - u));
                    Vector3 direction = next - point;
                    var facing = direction.sqrMagnitude > .000001f ? Quaternion.LookRotation(direction) : line.transform.rotation;
                    contact = hitBox.Contact(query, point, next, facing, time, nextTime, out var hitPoint);
                    point = contact ? hitPoint : next;
                    line.transform.SetPositionAndRotation(point, facing);
                    if (style != Style.Sword && style != Style.Projectile)
                        line.transform.Rotate(Vector3.up, nextTime * 260, Space.Self);
                    if (contact || nextTime >= seconds) break;
                    time = nextTime;
                    yield return null;
                }
                onContact?.Invoke(contact);
            }
            finally { Destroy(line.gameObject); }
        }
    }
}
