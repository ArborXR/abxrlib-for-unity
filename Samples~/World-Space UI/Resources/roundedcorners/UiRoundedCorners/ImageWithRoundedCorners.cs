using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace AbxrLib.UiRoundedCorners {
	[ExecuteInEditMode]                             //Required to check the OnEnable function
	[DisallowMultipleComponent]                     //You can only have one of these in every object.
	[RequireComponent(typeof(RectTransform))]
	[Preserve]                                      //Only instantiated from shipped prefabs; keep the linker from stripping it.
	public class ImageWithRoundedCorners : MonoBehaviour {
		private static readonly int Props = Shader.PropertyToID("_WidthHeightRadius");
		private static readonly int prop_OuterUV = Shader.PropertyToID("_OuterUV");

		public float radius = 40f;
		private Material material;
		private Vector4 outerUV = new Vector4(0, 0, 1, 1);

		[HideInInspector, SerializeField] private MaskableGraphic image;

		private void OnValidate() {
			Validate();
			Refresh();
		}

		private void OnDestroy() {
			if (image != null) {
				image.material = null;      //This makes so that when the component is removed, the UI material returns to null
			}

			DestroyHelper.Destroy(material);
			image = null;
			material = null;
		}

		private void OnEnable() {
			//You can only add either ImageWithRoundedCorners or ImageWithIndependentRoundedCorners
			//It will replace the other component when added into the object.
			var other = GetComponent<ImageWithIndependentRoundedCorners>();
			if (other != null) {
				radius = other.r.x;                 //When it does, transfer the radius value to this script
				DestroyHelper.Destroy(other);
			}

			Validate();
			Refresh();
		}

		private void OnRectTransformDimensionsChange() {
			if (enabled && material != null) {
				Refresh();
			}
		}

		public void Validate() {
			if (material == null) {
				var shader = FindShader();
				if (shader == null) {
					// A miss here is expected on a fresh install: the prefabs carrying this component can be
					// imported before the shader asset is, and OnValidate runs during that import. Retry once the
					// import queue drains, and only report a problem if the shader is still missing then.
					ReportOrRetryMissingShader();
					return;
				}

				material = new Material(shader);
			}

			if (image == null) {
				TryGetComponent(out image);
			}

			if (image != null) {
				image.material = material;
			}

			if (image is Image uiImage && uiImage.sprite != null) {
				outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(uiImage.sprite);
			}
		}

		/// <summary>The shader, by its current name and by the name it shipped under before AbxrLib prefixed it.</summary>
		private static Shader FindShader() =>
			Shader.Find("AbxrLib/UI/RoundedCorners/RoundedCorners") ??
			Shader.Find("UI/RoundedCorners/RoundedCorners");

		/// <summary>
		/// Handles a shader lookup that came back empty. In the Editor this is usually just import ordering, so it
		/// schedules one retry and stays quiet; outside the Editor a miss is real and is reported immediately.
		/// </summary>
		private void ReportOrRetryMissingShader() {
#if UNITY_EDITOR
			if (retryScheduled) return;

			retryScheduled = true;
			UnityEditor.EditorApplication.delayCall += RetryValidateAfterImport;
#else
			Debug.LogError("[AbxrLib] Could not find the rounded corners shader, so rounded UI corners will not render.");
#endif
		}

#if UNITY_EDITOR
		private bool retryScheduled;

		private void RetryValidateAfterImport() {
			retryScheduled = false;

			// The component can be destroyed between scheduling and running (the import replaced the object, or the
			// user deleted it). Unity's null check covers that case for a destroyed MonoBehaviour.
			if (this == null) return;

			if (FindShader() == null) {
				Debug.LogError("[AbxrLib] Could not find the rounded corners shader, so rounded UI corners will not " +
				               "render.\nWHAT TO DO: check that RoundedCorners.shader imported without errors " +
				               "(Resources/roundedcorners/UiRoundedCorners/). Reimporting the AbxrLib package fixes a " +
				               "partial import.");
				return;
			}

			Validate();
			Refresh();
		}
#endif

		public void Refresh() {
			if (material == null) return;

			var rect = ((RectTransform)transform).rect;

			//Multiply radius value by 2 to make the radius value appear consistent with ImageWithIndependentRoundedCorners script.
			//Right now, the ImageWithIndependentRoundedCorners appears to have double the radius than this.
			material.SetVector(Props, new Vector4(rect.width, rect.height, radius * 2, 0));
			material.SetVector(prop_OuterUV, outerUV);
		}
	}
}