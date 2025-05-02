using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class HapticFeedback : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    //public float hoverFeedBackForce = 0.4f;
    //public float hoverDuration = 0.02f;

    public float clickFeedBackForce = 0.6f;
    public float clickDuration = 0.02f;


    private Button _button;

    private static XRUIInputModule GetXRInputModule() => EventSystem.current.currentInputModule as XRUIInputModule;

    private static bool TryGetXRRayInteractor(int pointerID, out XRRayInteractor rayInteractor)
    {
        var inputModule = GetXRInputModule();
        if (inputModule == null)
        {
            rayInteractor = null;
            return false;
        }

        rayInteractor = inputModule.GetInteractor(pointerID) as XRRayInteractor;
        return rayInteractor != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //if (TryGetXRRayInteractor(eventData.pointerId, out var rayInteractor))
        {
            //rayInteractor.SendHapticImpulse(hoverFeedBackForce, hoverDuration);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (TryGetXRRayInteractor(eventData.pointerId, out var rayInteractor))
        {
            rayInteractor.SendHapticImpulse(clickFeedBackForce, clickDuration);
        }
    }
}