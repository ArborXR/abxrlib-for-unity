/*
 * Copyright (c) 2026 ArborXR. All rights reserved.
 *
 * Points at the label a panel prefab shows its message in, such as AbxrDarkPanelWithText's DynamicMessage.
 *
 * A panel has other labels too (its header's "ArborXR", its "Powered by" footer), so looking the message up with
 * GetComponentInChildren<TextMeshProUGUI>() finds whichever comes first. On the exit poll's backdrop that was the
 * header, which then showed the question in place of the brand name.
 */

using TMPro;
using UnityEngine;
using UnityEngine.Scripting;

namespace AbxrLib.Runtime.UI
{
    // Only ever reached through a shipped prefab, so the managed linker cannot see it referenced.
    [Preserve]
    public class PanelMessage : MonoBehaviour
    {
        [Tooltip("The label that shows the panel's message")]
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>The panel's message.</summary>
        public string Text
        {
            get => label.text;
            set => label.text = value;
        }
    }
}
