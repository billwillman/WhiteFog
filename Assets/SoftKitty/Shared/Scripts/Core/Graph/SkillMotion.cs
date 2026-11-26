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

        [NonSerialized] private string cachedLayer;
        [NonSerialized] private string cachedState;
        [NonSerialized] private string cachedPath;
        [NonSerialized] private int stateHash;
        [NonSerialized] private RuntimeAnimatorController cachedController;
        [NonSerialized] private int layerIndex = -1;

        public SkillMotion Copy()
        {
            var copy = (SkillMotion)MemberwiseClone();
            if (source == Source.AnimatorState) copy.PrepareStatePath();
            return copy;
        }

        public string StatePath => string.IsNullOrEmpty(state) ? "" : layer + "." + state;

        private void PrepareStatePath()
        {
            if (cachedPath != null && layer == cachedLayer && state == cachedState) return;
            if (layer != cachedLayer) cachedController = null;
            cachedLayer = layer;
            cachedState = state;
            cachedPath = StatePath;
            stateHash = Animator.StringToHash(cachedPath);
        }

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
            var controller = animator.runtimeAnimatorController;
            if (!controller) return "角色的 Animator 缺少 Controller";
            PrepareStatePath();
            if (controller != cachedController || layerIndex < 0)
            {
                layerIndex = animator.GetLayerIndex(layer);
                cachedController = controller;
            }
            return layerIndex >= 0 && animator.HasState(layerIndex, stateHash) ? null : "角色缺少 Animator State " + cachedPath;
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
                animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, transitionSeconds), layerIndex, 0f);
            }
            return true;
        }
    }
}
