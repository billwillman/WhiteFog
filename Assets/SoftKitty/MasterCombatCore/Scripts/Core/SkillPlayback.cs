using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace SoftKitty
{
    public sealed class SkillPlayback : MonoBehaviour
    {
        public enum Style { Direct, Sword, Orb, Mist }
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

        // A launched projectile lives in its scene, independently of the graph or actor that fired it.
        public SkillPlayback ProjectileHost()
        {
            var scene = gameObject.scene;
            if (projectileHosts.TryGetValue(scene, out var host) && host) return host;
            var go = new GameObject("在途技能投射物");
            SceneManager.MoveGameObjectToScene(go, scene);
            host = go.AddComponent<SkillPlayback>();
            host.projectileScene = scene;
            projectileHosts[scene] = host;
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

        public void RetireAfter(float seconds) => retireAt = Time.time + seconds;
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

        public IEnumerator Flight(Style style, Vector3 start, Entity target, EntityComponent actor, float seconds, float arc)
        {
            Color color = style == Style.Sword ? Color.cyan : style == Style.Orb ? new Color(1,.55f,.12f) : new Color(.55f,1,.25f);
            var line = Trace(style.ToString(), color, style == Style.Mist ? .17f : .09f, true);
            var shape = style == Style.Sword
                ? new[] { new Vector3(0,0,.75f), new Vector3(.12f,0,-.24f), new Vector3(.22f,0,-.31f), new Vector3(0,0,-.5f), new Vector3(-.22f,0,-.31f), new Vector3(-.12f,0,-.24f) }
                : Ring(style == Style.Mist ? .36f : .25f, true);
            line.positionCount = shape.Length;
            line.SetPositions(shape);
            for (float time = 0; time < seconds; time += Time.deltaTime)
            {
                if (!target.AvailableForInteraction) break;
                var end = Position(target, actor);
                float u = time / seconds;
                line.transform.position = Vector3.Lerp(start, end, u) + Vector3.up * (4 * arc * u * (1 - u));
                var direction = end - start + Vector3.up * (4 * arc * (1 - 2 * u));
                if (direction.sqrMagnitude > .001f) line.transform.rotation = Quaternion.LookRotation(direction);
                if (style != Style.Sword) line.transform.Rotate(Vector3.up, time * 260, Space.Self);
                yield return null;
            }
            Destroy(line.gameObject);
        }
    }
}
