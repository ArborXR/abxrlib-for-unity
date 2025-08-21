using UnityEngine;

namespace Abxr.Runtime.UI.DebugWindow
{
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