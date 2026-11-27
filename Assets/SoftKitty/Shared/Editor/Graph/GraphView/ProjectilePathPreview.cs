using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    internal static class ProjectilePathPreview
    {
        private const float ReferenceDistance = 8f;
        private static readonly ProjectilePathContext Path = new ProjectilePathContext(
            Vector3.zero, Vector3.forward * ReferenceDistance, Quaternion.identity);

        internal static void Draw(SkillProjectile shot)
        {
            EditorGUILayout.LabelField($"路径预览 · 目标距离 {ReferenceDistance} 米", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect top = GUILayoutUtility.GetRect(0, 128, GUILayout.ExpandWidth(true));
                Rect side = GUILayoutUtility.GetRect(0, 128, GUILayout.ExpandWidth(true));
                if (Event.current.type != EventType.Repaint) return;

                var trajectory = shot.CreateTrajectory();
                var samples = Sample(trajectory, shot.pathPoints);
                var markers = shot.pathPoints.Select(p => trajectory.EvaluatePosition(Path, p.progress)).ToArray();
                DrawProjection(top, samples, markers, true);
                DrawProjection(side, samples, markers, false);
            }
        }

        private static Vector3[] Sample(IProjectileTrajectory trajectory, List<ProjectilePathPoint> points)
        {
            // Include every authored progress, even when adjacent points are very close together.
            var stops = points.Select(p => p.progress).Where(t => t > 0 && t < 1).Distinct().OrderBy(t => t);
            var samples = new List<Vector3> { trajectory.EvaluatePosition(Path, 0) };
            float start = 0;
            foreach (float end in stops.Append(1f))
            {
                const int steps = 16;
                for (int i = 1; i <= steps; i++)
                    samples.Add(trajectory.EvaluatePosition(Path, Mathf.Lerp(start, end, i / (float)steps)));
                start = end;
            }
            return samples.ToArray();
        }

        private static void DrawProjection(Rect area, Vector3[] samples, Vector3[] markers, bool top)
        {
            if (area.width < 48) return;
            bool dark = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(area, dark ? new Color(.14f, .16f, .18f) : new Color(.88f, .90f, .92f));
            GUI.Label(new Rect(area.x + 5, area.y + 2, area.width - 10, 18),
                top ? "俯视 · 左偏向上" : "侧视 · 高度向上", EditorStyles.miniLabel);

            // Both axes use metres at the same scale. Include overshoots before or beyond the target.
            Vector2 Project(Vector3 p) => new Vector2(p.z, top ? p.x : -p.y);
            Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (var sample in samples)
            {
                var point = Project(sample);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            Rect plot = Rect.MinMaxRect(area.xMin + 15, area.yMin + 27, area.xMax - 15, area.yMax - 21);
            float scale = Mathf.Min(plot.width / (max.x - min.x), plot.height / Mathf.Max(.01f, max.y - min.y));
            Vector2 center = (min + max) * .5f;
            Vector2 Pixel(Vector3 p) => plot.center + (Project(p) - center) * scale;

            Vector2 origin = Pixel(samples[0]), target = Pixel(samples[samples.Length - 1]);
            using (new Handles.DrawingScope(dark ? Color.gray : Color.grey * .65f))
                Handles.DrawDottedLine(origin, target, 4);
            Color line = dark ? new Color(.31f, .84f, .77f) : new Color(.02f, .39f, .36f);
            using (new Handles.DrawingScope(line))
                Handles.DrawAAPolyLine(2.5f, System.Array.ConvertAll(samples, p => (Vector3)Pixel(p)));

            for (int i = 0; i < markers.Length; i++)
            {
                Vector2 p = Pixel(markers[i]);
                EditorGUI.DrawRect(new Rect(p.x - 2.5f, p.y - 2.5f, 5, 5), line);
                GUI.Label(new Rect(p.x - 4, p.y - 17, 28, 16), (i + 1).ToString(), EditorStyles.miniBoldLabel);
            }
            foreach (Vector2 p in new[] { origin, target })
                EditorGUI.DrawRect(new Rect(p.x - 2, p.y - 2, 4, 4), line);
            GUI.Label(new Rect(origin.x - 12, origin.y + 3, 28, 16), "起点", EditorStyles.miniLabel);
            GUI.Label(new Rect(target.x - 12, target.y + 3, 28, 16), "目标", EditorStyles.miniLabel);
        }
    }
}
