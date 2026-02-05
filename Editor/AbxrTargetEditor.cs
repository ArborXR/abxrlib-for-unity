/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - AbxrTarget Editor
 * 
 * Custom editor for AbxrTarget component that draws gizmos in the Scene view.
 */

using AbxrLib.Runtime.Telemetry;
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

            // Subscribe to hierarchy changes
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from hierarchy changes
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            AbxrTarget target = (AbxrTarget)this.target;
            if (target == null || target.transform == null) return;

            Transform currentParent = target.transform.parent;
            Vector3 currentLocalPos = target.transform.localPosition;

            // Check if parent changed
            if (currentParent != lastKnownParent)
            {
                lastKnownParent = currentParent;
                lastKnownLocalPosition = currentLocalPos;

                // If auto-center is enabled and we have a parent, center it at the bounds center
                if (target.autoCenterOnParent && currentParent != null)
                {
                    // Get the target local position (bounds center or parent pivot)
                    Vector3 targetLocalPosition = target.GetTargetLocalPosition();
                    
                    // Only move if significantly different from target position
                    if (Vector3.Distance(currentLocalPos, targetLocalPosition) > 0.01f)
                    {
                        Undo.RecordObject(target.transform, "Auto-center AbxrTarget on parent (hierarchy change)");
                        target.transform.localPosition = targetLocalPosition;
                        target.transform.localRotation = Quaternion.identity;

                        EditorUtility.SetDirty(target.gameObject);
                        EditorUtility.SetDirty(target.transform);
                        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                    }
                    
                    // Update visualization to match new parent's size
                    // Use delayCall to ensure it happens after Unity processes the hierarchy change
                    EditorApplication.delayCall += () =>
                    {
                        if (target != null && target.transform != null && target.gameObject != null)
                        {
                            // Explicitly update the visualization to recalculate size from new parent's bounds
                            target.UpdateDebugVisualization();
                            EditorUtility.SetDirty(target);
                            EditorUtility.SetDirty(target.transform);
                            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        }
                    };
                }
            }
            // Check if local position changed while parent stayed the same (user manually moved it)
            else if (currentParent != null && currentLocalPos != lastKnownLocalPosition && target.autoCenterOnParent)
            {
                // Only auto-center if it's significantly off-center (more than 0.01 units)
                // This prevents fighting with the user if they're trying to position it manually
                if (currentLocalPos.magnitude > 0.01f)
                {
                    // Don't auto-center here - let the user position it manually if they want
                    // But we'll update our tracking
                    lastKnownLocalPosition = currentLocalPos;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            // Track parent changes during inspector updates
            AbxrTarget target = (AbxrTarget)this.target;
            if (target != null && target.transform != null)
            {
                if (target.transform.parent != lastKnownParent)
                {
                    OnHierarchyChanged();
                }
                lastKnownParent = target.transform.parent;
                lastKnownLocalPosition = target.transform.localPosition;
            }

            // Draw default inspector
            DrawDefaultInspector();
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
        private static void DrawAbxrTargetGizmo(AbxrTarget target, GizmoType gizmoType)
        {
            if (target == null) return;
            
            // Use the consolidated gizmo drawing method from AbxrTarget
            bool isSelected = (gizmoType & GizmoType.Selected) != 0;
            AbxrTarget.DrawGizmoInternal(target, isSelected);
        }
    }
}
