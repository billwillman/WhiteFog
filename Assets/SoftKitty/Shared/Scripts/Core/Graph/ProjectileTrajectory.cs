using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    public interface IProjectileTrajectory
    {
        Vector3 EvaluatePosition(ProjectilePathContext path, float progress);
    }

    public readonly struct ProjectilePathContext
    {
        public readonly Vector3 Start, Aim;
        private readonly Quaternion frame;

        public ProjectilePathContext(Vector3 start, Vector3 aim, Quaternion launchFacing)
        {
            Start = start;
            Aim = aim;
            var direction = aim - start;
            if (direction.sqrMagnitude < .000001f)
            {
                frame = launchFacing;
                return;
            }
            direction.Normalize();
            var up = Mathf.Abs(direction.y) < .999f ? Vector3.up : launchFacing * Vector3.forward;
            if (Vector3.Cross(up, direction).sqrMagnitude < .000001f) up = Vector3.right;
            frame = Quaternion.LookRotation(direction, up);
        }

        public Vector3 Position(float progress, Vector3 offset) => Vector3.Lerp(Start, Aim, progress) + frame * offset;
    }

    [Serializable]
    public struct ProjectilePathPoint
    {
        [Range(0, 1)] public float progress;
        public Vector3 offset;
    }

    internal sealed class StraightProjectileTrajectory : IProjectileTrajectory
    {
        internal static readonly StraightProjectileTrajectory Instance = new StraightProjectileTrajectory();
        public Vector3 EvaluatePosition(ProjectilePathContext path, float progress) => Vector3.Lerp(path.Start, path.Aim, progress);
    }

    internal sealed class SineProjectileTrajectory : IProjectileTrajectory
    {
        private readonly Vector3 amplitude;
        private readonly int halfWaves;

        internal SineProjectileTrajectory(Vector3 amplitude, int halfWaves)
        {
            this.amplitude = amplitude;
            this.halfWaves = Mathf.Clamp(halfWaves, 1, 16);
        }

        public Vector3 EvaluatePosition(ProjectilePathContext path, float progress)
        {
            if (progress <= 0) return path.Start;
            if (progress >= 1) return path.Aim;
            return path.Position(progress, amplitude * Mathf.Sin(progress * Mathf.PI * halfWaves));
        }
    }

    internal sealed class WaypointProjectileTrajectory : IProjectileTrajectory
    {
        private readonly List<ProjectilePathPoint> points;

        internal WaypointProjectileTrajectory(List<ProjectilePathPoint> points)
        {
            this.points = points;
        }

        public Vector3 EvaluatePosition(ProjectilePathContext path, float progress)
        {
            if (progress <= 0) return path.Start;
            if (progress >= 1) return path.Aim;
            float start = 0, end = 1;
            Vector3 from = Vector3.zero, to = Vector3.zero;
            if (points != null)
                foreach (var point in points)
                {
                    if (point.progress <= 0 || point.progress >= 1) continue;
                    if (point.progress <= progress && point.progress >= start)
                    {
                        start = point.progress;
                        from = point.offset;
                    }
                    else if (point.progress > progress && point.progress < end)
                    {
                        end = point.progress;
                        to = point.offset;
                    }
                }
            float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, progress));
            return path.Position(progress, Vector3.Lerp(from, to, t));
        }
    }
}
