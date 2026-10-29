using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ThroneSiege.Editor
{
    public static class AnimationTimingReview
    {
        [MenuItem("ThroneSiege/Use Single Hit Reaction")]
        public static void UseSingleHitReaction()
        {
            if (EditorApplication.isPlaying) return;
            AssetDatabase.SaveAssets();
            TrimHit("Assets/Animations/NecromancerPlayerClips/NecromancerPlayer.controller", 16f / 24f);
            TrimHit("Assets/Animations/CrusaderTank/CrusaderTankAI.controller", 17f / 24f);
            AssetDatabase.SaveAssets();
            Debug.Log("Single hit reactions applied: player 0.667s, enemy 0.708s.");
        }

        private static void TrimHit(string controllerPath, float end)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Hit").state;
            var clip = (AnimationClip)state.motion;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curves = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                var source = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                float value = source.Evaluate(end);
                float slope = (source.Evaluate(end) - source.Evaluate(end - 0.0001f)) / 0.0001f;
                var keys = source.keys.Where(k => k.time < end - 0.00001f).ToList();
                keys.Add(new Keyframe(end, value, slope, 0f));
                curves[i] = new AnimationCurve(keys.ToArray())
                { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
            }
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(clip)
                .Where(e => e.time < end).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = end;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            foreach (var transition in state.transitions)
            {
                if (!transition.hasExitTime) continue;
                transition.exitTime = 1f;
                transition.hasFixedDuration = true;
                transition.duration = 0.08f;
            }
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(controller);
            Debug.Log(controller.name + " Hit: " + clip.length.ToString("F3") + "s, first reaction only.");
        }

        [MenuItem("ThroneSiege/Sync Animation Timing")]
        public static void Sync()
        {
            if (EditorApplication.isPlaying) return;
            AssetDatabase.SaveAssets();
            var report = new List<string>();
            string[] paths = {
                "Assets/Animations/NecromancerPlayerClips/NecromancerPlayer.controller",
                "Assets/Animations/CrusaderTank/CrusaderTankAI.controller"
            };
            foreach (string path in paths)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                {
                    AnimatorState state = child.state;
                    AnimationClip clip = state.motion as AnimationClip;
                    if (clip == null) continue;
                    float exit = state.transitions.Where(t => t.hasExitTime && t.destinationState != state)
                        .Select(t => Mathf.Clamp01(t.exitTime)).DefaultIfEmpty(1f).Min();
                    // An old event can itself extend AnimationClip.length beyond
                    // the final remaining pose key. Use the motion curves as the
                    // source of truth after trimming, not that extended length.
                    float motionLength = MotionLength(clip);
                    float playableEnd = motionLength * exit;
                    report.Add(controller.name + "/" + state.name + ": motion=" + motionLength.ToString("F3") +
                        "s, clip=" + clip.length.ToString("F3") +
                        "s, stateSpeed=" + state.speed.ToString("F2") + ", exit=" + exit.ToString("F2") +
                        ", playable=" + (playableEnd / Mathf.Max(0.001f, state.speed)).ToString("F3") + "s");

                    // Speed changes already scale animation events. Repair only events
                    // left outside the playable window by an actual trim/early exit.
                    if (!AssetDatabase.GetAssetPath(clip).StartsWith("Assets/Animations/")) continue;
                    var eventList = AnimationUtility.GetAnimationEvents(clip).ToList();
                    bool changed = false;
                    bool enemyAttack = controller.name == "CrusaderTankAI" &&
                        (state.name == "Attack1" || state.name == "Attack2" || state.name == "ShieldAttack");
                    bool playerAttack = controller.name == "NecromancerPlayer" && state.name == "Attack1";
                    if (enemyAttack || playerAttack)
                    {
                        string[] required = enemyAttack
                            ? new[] { "LockAttackFacing", "AttackImpact", "AttackFinished" }
                            : new[] { "BasicAttackImpact", "BasicAttackFinished" };
                        float[] times = enemyAttack
                            ? new[] { playableEnd * 0.3f, playableEnd * 0.5f, playableEnd - 0.02f }
                            : new[] { Mathf.Min(PlayerBasicAttackSetup.FindSwingTime(clip, motionLength), playableEnd * 0.9f), playableEnd - 0.02f };
                        for (int i = 0; i < required.Length; i++)
                            if (!eventList.Any(e => e.functionName == required[i]))
                            {
                                eventList.Add(new AnimationEvent { functionName = required[i], time = Mathf.Max(0f, times[i]) });
                                changed = true;
                                report.Add("  Restored " + required[i] + " at " + times[i].ToString("F3") + "s");
                            }
                    }
                    AnimationEvent[] events = eventList.ToArray();
                    AnimationEvent[] combatEvents = events.Where(e => e.functionName == "BasicAttackImpact" ||
                        e.functionName == "BasicAttackFinished" || e.functionName == "LockAttackFacing" ||
                        e.functionName == "AttackImpact" || e.functionName == "AttackFinished").ToArray();
                    if (combatEvents.Length > 0 && combatEvents.Any(e => e.time >= playableEnd || e.time < 0f))
                    {
                        float previousEnd = combatEvents.Max(e => e.time);
                        if (previousEnd > 0f && playableEnd > 0.04f)
                        {
                            float factor = (playableEnd - 0.02f) / previousEnd;
                            foreach (AnimationEvent animationEvent in combatEvents)
                                animationEvent.time = Mathf.Max(0f, animationEvent.time * factor);
                            if (playerAttack)
                                foreach (AnimationEvent animationEvent in combatEvents)
                                    if (animationEvent.functionName == "BasicAttackImpact")
                                        animationEvent.time = Mathf.Min(PlayerBasicAttackSetup.FindSwingTime(clip, motionLength), playableEnd * 0.9f);
                            changed = true;
                            report.Add("  Repositioned combat events within trimmed clip; scale=" + factor.ToString("F3"));
                        }
                    }
                    if (!changed) continue;
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.stopTime = motionLength;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());
                    EditorUtility.SetDirty(clip);
                }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp");
            File.WriteAllLines("Temp/AnimationTimingReview.txt", report);
            Debug.Log("Animation timing reviewed. " + string.Join("\n", report));
        }

        private static float MotionLength(AnimationClip clip)
        {
            float end = 0f;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null && curve.length > 0) end = Mathf.Max(end, curve.keys[curve.length - 1].time);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length > 0) end = Mathf.Max(end, keys[keys.Length - 1].time);
            }
            return end > 0f ? end : clip.length;
        }
    }
}
