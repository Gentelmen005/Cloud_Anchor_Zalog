

namespace Google.XR.ARCoreExtensions.Samples.PersistentCloudAnchors
{
    using UnityEngine;

    /// 
    /// A helper component that scale the UI rect to the same size as the safe area.
    ///
    public class SafeAreaScaler : MonoBehaviour
    {
        private Rect _screenSafeArea = new Rect(0, 0, 0, 0);

        /// 
        /// Unity's Awake() method.
        /// 
        public void Update()
        {
            Rect safeArea;
            safeArea = Screen.safeArea;

            if (_screenSafeArea != safeArea)
            {
                _screenSafeArea = safeArea;
                MatchRectTransformToSafeArea();
            }
        }

        private void MatchRectTransformToSafeArea()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();

            // lower left corner offset
            Vector2 offsetMin = new Vector2(_screenSafeArea.xMin,
                Screen.height - _screenSafeArea.yMax);

            // upper right corner offset
            Vector2 offsetMax = new Vector2(
                _screenSafeArea.xMax - Screen.width,
                -_screenSafeArea.yMin);

            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }
    }
}
