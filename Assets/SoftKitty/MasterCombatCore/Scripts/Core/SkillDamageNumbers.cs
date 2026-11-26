using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SoftKitty
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityComponent))]
    public sealed class SkillDamageNumbers : MonoBehaviour
    {
        private const float Lifetime = .85f;
        private const int Capacity = 8;
        private readonly List<Label> labels = new List<Label>(Capacity);
        private Entity observed;
        private Canvas canvas;
        private int next;

        private sealed class Label
        {
            public Text text;
            public float start;
            public float lane;
            public bool visible;
        }

        public static void Observe(EntityComponent actor)
        {
            if (!actor.TryGetComponent<SkillDamageNumbers>(out var numbers))
                numbers = actor.gameObject.AddComponent<SkillDamageNumbers>();
            numbers.Bind(actor.mData);
        }

        private void Bind(Entity data)
        {
            if (observed == data) return;
            observed?.UnRegisterAttributeChangeCallback(OnAttributeChange);
            observed = data;
            observed?.RegisterAttributeChangeCallback(OnAttributeChange);
        }

        private void OnEnable()
        {
            Bind(GetComponent<EntityComponent>().mData);
            RenderPipelineManager.beginCameraRendering += BeforeRender;
            Camera.onPreCull += FaceCamera;
        }

        private void OnDisable()
        {
            Bind(null);
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            Camera.onPreCull -= FaceCamera;
            if (canvas) canvas.enabled = false;
            foreach (var label in labels)
            {
                label.visible = false;
                label.text.enabled = false;
            }
        }

        private void OnAttributeChange(string attribute, float delta, Entity dealer)
        {
            if (attribute != "chp" || delta >= 0f) return;
            if (!canvas)
            {
                var root = new GameObject("伤害飘字", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                root.transform.localScale = Vector3.one * .012f;
                canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
            }
            canvas.enabled = true;
            if (next == labels.Count)
            {
                var go = new GameObject("伤害数值", typeof(RectTransform), typeof(Text), typeof(Outline));
                go.transform.SetParent(canvas.transform, false);
                var text = go.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 36;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(200f, 60f);
                var outline = go.GetComponent<Outline>();
                outline.effectColor = new Color(.18f, .10f, .04f, .95f);
                outline.effectDistance = new Vector2(1.8f, -1.8f);
                labels.Add(new Label { text = text });
            }

            var label = labels[next];
            label.lane = next % 3 == 0 ? 0 : next % 3 == 1 ? -24 : 24;
            next = (next + 1) % Capacity;
            label.start = Time.time;
            label.visible = true;
            float amount = -delta;
            label.text.text = "-" + amount.ToString(amount < .01f ? "G2" : "0.##", CultureInfo.InvariantCulture);
            label.text.rectTransform.anchoredPosition = new Vector2(label.lane, 0);
            label.text.color = new Color(1f, .87f, .49f);
            label.text.enabled = true;
        }

        private void LateUpdate()
        {
            if (!canvas || !canvas.enabled) return;
            canvas.transform.position = transform.position + Vector3.up * 1.9f;
            bool any = false;
            foreach (var label in labels)
            {
                if (!label.visible) continue;
                float age = (Time.time - label.start) / Lifetime;
                if (age >= 1f)
                {
                    label.visible = false;
                    label.text.enabled = false;
                    continue;
                }
                any = true;
                label.text.rectTransform.anchoredPosition = new Vector2(label.lane, age * 58);
                label.text.color = new Color(1f, .87f, .49f, 1f - Mathf.SmoothStep(0, 1, (age - .48f) / .52f));
            }
            canvas.enabled = any;
        }

        private void BeforeRender(ScriptableRenderContext context, Camera camera) => FaceCamera(camera);

        private void FaceCamera(Camera camera)
        {
            if (canvas && canvas.enabled && camera.cameraType == CameraType.Game)
                canvas.transform.rotation = camera.transform.rotation;
        }
    }
}
