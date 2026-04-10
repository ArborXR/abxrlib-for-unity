#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AbxrLib.Runtime.UI.Keyboard;

namespace AbxrLib.Runtime.Core
{
    /// <summary>
    /// Shared world-space scan panel used by both Quest and PICO QR scanners.
    /// Built entirely in code so no prefab setup is required.
    /// </summary>
    public class QrScanPanel : MonoBehaviour
    {
        private const float DistanceFromCamera = 0.9f;
        private const float HorizontalOffset = 0.0f;
        private const float VerticalOffset = 0.0f;

        private GameObject _root;
        private RawImage _previewImage;
        private Image _frameImage;
        private TMP_Text _statusText;
        private Button _cancelButton;
        private Action _onCancel;
        private Camera _mainCamera;
        private Canvas _canvas;

        public static QrScanPanel CreateRuntimePanel(Transform parent, Action onCancel)
        {
            var go = new GameObject("QrScanPanel");
            go.transform.SetParent(parent, false);
            QrScanPanel panel = go.AddComponent<QrScanPanel>();
            panel.Build(onCancel);
            panel.Hide();
            return panel;
        }

        public void Show()
        {
            EnsureUiInputReady();

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (_cancelButton != null) _cancelButton.interactable = true;

            if (_root != null) _root.SetActive(true);

            // Bounce the XR ray interactors for one frame so XRI clears its hover state and re-fires OnPointerEnter.
            // Without this, if the laser is already pointing at the cancel button when the panel appears, the hover highlight never shows.
            StartCoroutine(LaserPointerManager.RefreshInteractorHover());

            // Block input briefly so the same gesture that opened the scanner can't immediately trigger the Cancel button
            KeyboardHandler.StartInputGuard();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text;
        }

        public void SetPreviewTexture(Texture texture)
        {
            if (_previewImage != null) _previewImage.texture = texture;
        }

        public void SetPreviewUvRect(Rect uvRect)
        {
            if (_previewImage != null) _previewImage.uvRect = uvRect;
        }

        private void Build(Action onCancel)
        {
            _onCancel = onCancel;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = Camera.main;
            _canvas.planeDistance = 0.5f;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(720f, 620f);
            canvasRect.localScale = Vector3.one * 0.00125f;

            _root = canvasGo;

            Image background = CreateImage("Background", canvasGo.transform, new Vector2(720f, 620f), new Color32(28, 34, 38, 255), true);
            Sprite roundedPanelSprite = Resources.Load<Sprite>("UI/RoundedPanel");
            background.sprite = roundedPanelSprite;
            background.type = Image.Type.Sliced;
            background.rectTransform.anchoredPosition = Vector2.zero;
            background.raycastTarget = true;

            _previewImage = CreateRawImage("Preview", canvasGo.transform, new Vector2(640f, 480f));
            _previewImage.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            _previewImage.color = Color.white;
            _previewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            _previewImage.raycastTarget = false;

            _frameImage = CreateImage("Frame", canvasGo.transform, new Vector2(646f, 486f), new Color(1f, 1f, 1f, 0.0f), true);
            _frameImage.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            _frameImage.raycastTarget = false;

            _statusText = CreateText("Status", canvasGo.transform, "Look at QR Code", 28f);
            _statusText.rectTransform.anchoredPosition = new Vector2(0f, 270f);
            _statusText.raycastTarget = false;

            _cancelButton = CreateButton("CancelButton", canvasGo.transform, "Cancel", new Vector2(0f, -278f));
            PressDownButtonTrigger cancelPressTrigger = _cancelButton.gameObject.AddComponent<PressDownButtonTrigger>();
            cancelPressTrigger.TargetButton = _cancelButton;
            cancelPressTrigger.OnPressed = HandleCancelPressed;

            EnsureUiInputReady();
        }

        private void EnsureUiInputReady()
        {
            if (_canvas != null) _canvas.worldCamera = Camera.main;

            if (_canvas != null) LaserPointerManager.EnsureTrackedDeviceGraphicRaycasterOnCanvases(_canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (_root == null || !_root.activeSelf) return;

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            if (_canvas != null && _canvas.worldCamera != _mainCamera) _canvas.worldCamera = _mainCamera;

            Transform cam = _mainCamera.transform;
            Vector3 targetPos = cam.position + cam.forward * DistanceFromCamera + cam.right * HorizontalOffset + cam.up * VerticalOffset;
            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.position, cam.up);
        }

        private void HandleCancelPressed()
        {
            if (_cancelButton == null || !_cancelButton.interactable) return;
            _onCancel?.Invoke();
        }

        private static Image CreateImage(string name, Transform parent, Vector2 size, Color color, bool preserveAspect = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.preserveAspect = preserveAspect;
            RectTransform rt = image.rectTransform;
            rt.sizeDelta = size;
            return image;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RawImage image = go.AddComponent<RawImage>();
            RectTransform rt = image.rectTransform;
            rt.sizeDelta = size;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            RectTransform rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(660f, 42f);
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string text, Vector2 anchoredPosition)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);

            Image image = buttonGo.AddComponent<Image>();
            Sprite roundedButtonSprite = Resources.Load<Sprite>("UI/RoundedButton");
            image.sprite = roundedButtonSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.14f);

            Outline outline = buttonGo.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.10f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            colors.normalColor = new Color(1f, 1f, 1f, 0.14f);
            colors.highlightedColor = new Color(0.35f, 0.65f, 1f, 0.35f);
            colors.pressedColor = new Color(0.20f, 0.50f, 1f, 0.55f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.06f);
            button.colors = colors;

            RectTransform rt = button.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 52f);
            rt.anchoredPosition = anchoredPosition;

            TMP_Text label = CreateText("Label", buttonGo.transform, text, 24f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.color = Color.white;
            label.raycastTarget = false;
            return button;
        }

        private sealed class PressDownButtonTrigger : MonoBehaviour, IPointerDownHandler
        {
            public Button TargetButton;
            public Action OnPressed;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (KeyboardHandler.IsInputGuarded) return;
                if (TargetButton == null || !TargetButton.IsActive() || !TargetButton.IsInteractable()) return;
                OnPressed?.Invoke();
            }
        }
    }
}
#endif
