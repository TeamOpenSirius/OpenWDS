using System;
using System.Collections.Generic;
using System.Reflection;
using Sirius;
using Sirius.Scarab;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenWDS.Editor
{
    // Focused fixtures for recovered support code, not character presentation.
    internal static class ValidateCharacterSupport
    {
        internal static int Run(List<AssetBundle> bundles)
        {
            var before = new HashSet<ExampleStateBehaviour>(Resources.FindObjectsOfTypeAll<ExampleStateBehaviour>());
            foreach (var bundle in bundles)
                bundle.LoadAllAssets<RuntimeAnimatorController>();
            var count = 0;
            foreach (var state in Resources.FindObjectsOfTypeAll<ExampleStateBehaviour>())
                if (!before.Contains(state)) count++;
            Require(count == 115, "Original ExampleStateBehaviour count=" + count);
            CheckBones();
            CheckStateBranches();
            ValidateCharacterTimeline.Run();
            return count;
        }

        private static void CheckBones()
        {
            var root = new GameObject("character-support-fixture");
            try
            {
                var branch = Child(root.transform, "branch");
                var first = Child(branch, "NAJ_karada_c2");
                var later = Child(root.transform, "NAJ_karada_c2");
                var head = Child(first, "NAJ_kubi_C");
                var left = Child(head, "CJ_Eye_L");
                var right = Child(head, "CJ_Eye_R");
                Require(ModelUtility.FindTransformByName(root.transform, first.name) == first,
                    "Bone search must use depth-first child order.");
                Require(ModelUtility.FindTransformByName(first, first.name) == first,
                    "Bone search must include root.");
                Require(ModelUtility.FindTransformByName(null, first.name) == null &&
                    ModelUtility.FindTransformByName(root.transform, "missing") == null, "Missing bone result");
                var settings = root.AddComponent<HeightSettings>();
                settings._bodyScale = 1.25f;
                settings._headScale = 0.75f;
                settings._shoeOffset = 0.2f;
                settings.Apply();
                Require(first.localScale == Vector3.one * 1.25f &&
                    first.localPosition == new Vector3(0, 0.25f, 0) &&
                    head.localScale == Vector3.one * 0.75f && later.localScale == Vector3.one,
                    "Apply bone scaling, shoe position, or duplicate selection");
                settings.SetScale(2, 3, 0.5f);
                Require(settings._bodyScale == 1.25f && settings._headScale == 0.75f &&
                    settings._shoeOffset == 0.2f, "SetScale must not replace serialized fields.");
                var source = Child(root.transform, "source").gameObject.AddComponent<HeightSettings>();
                source._bodyScale = 1.5f;
                source._headScale = 0.5f;
                source._shoeOffset = 0.25f;
                Child(source.transform, "CJ_Eye_L").localPosition = new Vector3(1, 2, 3);
                Child(source.transform, "CJ_Eye_R").localPosition = new Vector3(4, 5, 6);
                settings.CopyFrom(source);
                settings.CopyFromEyePosition(source);
                Require(first.localPosition == new Vector3(0, 0.375f, 0) &&
                    left.localPosition == new Vector3(1, 2, 3) && right.localPosition == new Vector3(4, 5, 6),
                    "Copy height and eye positions");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CheckStateBranches()
        {
            // A synthetic one-second clip gives real Unity AnimatorStateInfo;
            // no original character/controller is modified by this fixture.
            var controller = new AnimatorController();
            var clip = new AnimationClip();
            var host = new GameObject("state-host-fixture");
            var slave = new GameObject("state-slave-fixture");
            var behaviour = ScriptableObject.CreateInstance<ExampleStateBehaviour>();
            var randomState = UnityEngine.Random.state;
            try
            {
                controller.AddParameter("GoToNext", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("NextBranch", AnimatorControllerParameterType.Int);
                controller.AddLayer("Base Layer");
                var state = controller.layers[0].stateMachine.AddState("fixture");
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
                state.motion = clip;
                var animator = host.AddComponent<Animator>();
                var other = slave.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                other.runtimeAnimatorController = controller;
                animator.Rebind();
                other.Rebind();
                animator.Play("Base Layer.fixture", 0, 2f);
                animator.Update(0);
                var completed = animator.GetCurrentAnimatorStateInfo(0);
                Require(completed.normalizedTime >= 1, "Fixture did not reach the loop boundary.");
                Set(behaviour, "_loopTimes", 1);
                Set(behaviour, "_branchesNum", 1);
                behaviour.OnStateUpdate(animator, completed, 0);
                Require((int)Get(behaviour, "_loopTimes") == 1 && animator.GetBool("GoToNext"),
                    "No-slave branch must retain loop count and trigger host.");
                var slaves = host.AddComponent<SlaveAnimators>();
                slaves.Append(other);
                Set(behaviour, "_slaveAnimators", slaves);
                animator.ResetTrigger("GoToNext");
                other.ResetTrigger("GoToNext");
                behaviour.OnStateUpdate(animator, default, 0);
                Require(!animator.GetBool("GoToNext") && other.GetBool("GoToNext"),
                    "Pre-boundary branch must trigger slave only.");
                other.ResetTrigger("GoToNext");
                behaviour.OnStateUpdate(animator, completed, 0);
                Require((int)Get(behaviour, "_loopTimes") == 0 && animator.GetBool("GoToNext") &&
                    other.GetBool("GoToNext") && other.GetInteger("NextBranch") == 0,
                    "Completed branch must forward both parameters and reset count with slave.");
                Set(behaviour, "_loopMin", 3);
                Set(behaviour, "_loopMax", 3);
                behaviour.OnStateEnter(animator, default, 0);
                Require((int)Get(behaviour, "_loopTimes") == 3, "Inclusive loop maximum");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(slave);
                Object.DestroyImmediate(behaviour);
                foreach (var layer in controller.layers)
                {
                    foreach (var state in layer.stateMachine.states) Object.DestroyImmediate(state.state);
                    Object.DestroyImmediate(layer.stateMachine);
                }
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(clip);
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
        private static object Get(object obj, string field) =>
            obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Set(object obj, string field, object value) =>
            obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
