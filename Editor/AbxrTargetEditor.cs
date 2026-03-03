/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 *
 * AbxrLib for Unity - AbxrTarget Editor
 *
 * Custom editor for AbxrTarget component that draws gizmos in the Scene view.
 */

using AbxrLib.Runtime.Services.Telemetry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbxrLib.Editor
{
    [CustomEditor(typeof(AbxrTarget))]
    [CanEditMultipleObjects]
    public class AbxrTargetEditor : UnityEditor.Editor
    {
        private Transform lastKnownParent;
        private Vector3 lastKnownLocalPosition;

        private void OnEnable()
        {
            AbxrTarget target = (AbxrTarget)this.target;
            if (target != null && target.transform != null)
            {
                lastKnownParent = target.transform.parent;
                lastKnownLocalPosition = target.transform.localPosition;
            }
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            AbxrTarget target = (AbxrTarget)this.target;
            if (target == null || target.transform == null) return;

            Transform currentParent = target.transform.parent;
            Vector3 currentLocalPos = target.transform.localPosition;

            if (currentParent != lastKnownParent)
            {
                lastKnownParent = currentParent;
                lastKnownLocalPosition = currentLocalPos;

                if (target.autoCenterOnParent && currentParent != null)
                {
                    Vector3 targetLocalPosition = target.GetTargetLocalPosition();
                    if (Vector3.Distance(currentLocalPos, targetLocalPosition) > 0.01f)
                    {
                        Undo.RecordObject(target.transform, "Auto-center AbxrTarget on parent (hierarchy change)");
                        target.transform.localPosition = targetLocalPosition;
                        target.transform.localRotation = Quaternion.identity;
                        EditorUtility.SetDirty(target.gameObject);
                        EditorUtility.SetDirty(target.transform);
                        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                    }
                    EditorApplication.delayCall += () =>
                    {
                        if (target != null && target.transform != null && target.gameObject != null)
                        {
                            target.UpdateDebugVisualization();
                            EditorUtility.SetDirty(target);
                            EditorUtility.SetDirty(target.transform);
                            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        }
                    };
                }
            }
            else if (currentParent != null && currentLocalPos != lastKnownLocalPosition && target.autoCenterOnParent)
            {
                if (currentLocalPos.magnitude > 0.01f)
                    lastKnownLocalPosition = currentLocalPos;
            }
        }

        public override void OnInspectorGUI()
        {
            AbxrTarget target = (AbxrTarget)this.target;
            if (target != null && target.transform != null)
            {
                if (target.transform.parent != lastKnownParent)
                    OnHierarchyChanged();
                lastKnownParent = target.transform.parent;
                lastKnownLocalPosition = target.transform.localPosition;
            }
            DrawDefaultInspector();
        }
    }
}
