using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SoftKitty
{
    [DisallowMultipleComponent]
    public sealed class SkillClipPlayer : MonoBehaviour
    {
        private PlayableGraph playback;
        private AnimationClipPlayable motion;
        private GraphRuntimeState cast;
        private Animator target;

        public void Play(Animator animator, AnimationClip clip, GraphRuntimeState owner)
        {
            StopClip();
            target = animator;
            cast = owner;
            motion = AnimationPlayableUtilities.PlayClip(animator, clip, out playback);
            motion.SetDuration(clip.length);
            enabled = true;
        }

        private void Update()
        {
            if (!target || !target.isActiveAndEnabled || cast.CastInterrupted || !playback.IsValid() ||
                motion.GetTime() >= motion.GetDuration()) StopClip();
        }

        public void StopClip()
        {
            enabled = false;
        }

        private void OnDisable()
        {
            if (playback.IsValid()) playback.Destroy();
            playback = default;
            cast = default;
            target = null;
        }
    }
}
