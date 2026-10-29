using UnityEngine;

namespace ThroneSiege.Combat
{
    // Tracks the evaluated state, so clip trimming and Animator speed changes
    // cannot leave a separate seconds-based combat timer running afterwards.
    public sealed class AnimatorActionProgress
    {
        private string stateName;
        private bool entered;
        private float entryDeadline;

        public void Begin(string name)
        {
            stateName = name;
            entered = false;
            entryDeadline = Time.time + 0.75f;
        }

        public bool HasFinished(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                return true;

            bool transitioning = animator.IsInTransition(0);
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = transitioning ? animator.GetNextAnimatorStateInfo(0) : default;
            if (current.IsName(stateName))
            {
                entered = true;
                return (transitioning && !next.IsName(stateName)) || current.normalizedTime >= 1f;
            }
            if (transitioning && next.IsName(stateName))
            {
                entered = true;
                return false;
            }

            // Also recover if a trigger was interrupted or its state is missing.
            return entered || Time.time >= entryDeadline;
        }
    }
}
