using UnityEngine;
using UnityEngine.Scripting;

namespace AbxrLib.Runtime.UI.DebugWindow
{
    // Only ever instantiated by loading a shipped prefab, so the managed linker cannot see it referenced.
    [Preserve]
    public class ScrollViewToggle : MonoBehaviour
    {
        public GameObject scrollView; // Reference to the Scroll View GameObject

        public void ShowScrollView()
        {
            scrollView.SetActive(true);
        }
    
        public void HideScrollView()
        {
            scrollView.SetActive(false);
        }
    }
}