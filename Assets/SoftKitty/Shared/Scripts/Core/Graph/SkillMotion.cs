using System;
using UnityEngine;

namespace SoftKitty
{
    [Serializable]
    public sealed class SkillMotion
    {
        public enum Source { AnimatorState, AnimationClip }
        public Source source;
        public string layer = "Base Layer";
        public string state = "";
        public AnimationClip clip;
        public float transitionSeconds = .04f;

        public SkillMotion Copy() => (SkillMotion)MemberwiseClone();

        public string StatePath => string.IsNullOrEmpty(state) ? "" : layer + "." + state;

        public static SkillMotion For(Action action, OriginalSkillGraph graph)
        {
            // Older graphs stored one castMotion. Keep their original state and transition time.
            return action.motion ?? new SkillMotion {
                state = graph.castMotion ? graph.castMotion.name : "", clip = graph.castMotion,
                transitionSeconds = graph.castMotion ? 1f / Mathf.Max(1f, graph.castMotion.frameRate) : 0f
            };
        }

        public string Check(Animator animator)
        {
            if (!animator) return "角色缺少 Animator";
            if (source == Source.AnimationClip)
                return !clip ? "请选择 Animation Clip" : clip.legacy ? "Animator 无法播放 Legacy 片段 " + clip.name : null;
            if (string.IsNullOrEmpty(state)) return "请选择 Animator State";
            if (!animator.runtimeAnimatorController) return "角色的 Animator 缺少 Controller";
            int index = animator.GetLayerIndex(layer);
            return index >= 0 && animator.HasState(index, Animator.StringToHash(StatePath)) ? null : "角色缺少 Animator State " + StatePath;
        }

        public bool Play(Animator animator, GraphRuntimeState cast, out string error)
        {
            error = Check(animator);
            if (error != null || cast.CastInterrupted) return false;
            var player = animator.GetComponent<SkillClipPlayer>();
            if (source == Source.AnimationClip)
            {
                if (!player) player = animator.gameObject.AddComponent<SkillClipPlayer>();
                player.Play(animator, clip, cast);
            }
            else
            {
                if (player) player.StopClip();
                animator.CrossFadeInFixedTime(Animator.StringToHash(StatePath),
                    Mathf.Max(0f, transitionSeconds), animator.GetLayerIndex(layer), 0f);
            }
            return true;
        }
    }
}
