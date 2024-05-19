namespace Google.XR.ARCoreExtensions.Samples.PersistentCloudAnchors
{
    using System;
    using System.Collections.Generic;
#if ARCORE_USE_ARF_5 // use ARF 5
    using Unity.XR.CoreUtils;
#endif
    using UnityEngine;
    using UnityEngine.XR.ARFoundation;

     
    /// Controller for Persistent Cloud Anchors sample.
    public class PersistentCloudAnchorsController : MonoBehaviour
    {
        [Header("AR Foundation")]

#if ARCORE_USE_ARF_5 // use ARF 5
 
        /// The active XROrigin used in the example.
        public XROrigin Origin;
#else // use ARF 4
 
        /// The active ARSessionOrigin used in the example.
        public ARSessionOrigin SessionOrigin;
#endif

 
        /// The ARSession used in the example.
 
        public ARSession SessionCore;

        /// The ARCoreExtensions used in the example.
        public ARCoreExtensions Extensions;

        /// The active ARAnchorManager used in the example.
        public ARAnchorManager AnchorManager;

        /// The active ARPlaneManager used in the example.
        public ARPlaneManager PlaneManager;

        /// The active ARRaycastManager used in the example.
        public ARRaycastManager RaycastManager;

        [Header("UI")]

        /// The home page to choose entering hosting or resolving work flow.
        public GameObject HomePage;

        /// The resolve screen that provides the options on which Cloud Anchors to be resolved.
        public GameObject ResolveMenu;

        /// The information screen that displays useful information about privacy prompt.
        public GameObject PrivacyPrompt;

        /// The AR screen which displays the AR view, hosts or resolves cloud anchors,
        /// and returns to home page.
        public GameObject ARView;

        /// The current application mode.
        [HideInInspector]
        public ApplicationMode Mode = ApplicationMode.Ready;

        /// A list of Cloud Anchors that will be used in resolving.
        public HashSet<string> ResolvingSet = new HashSet<string>();

        /// The key name used in PlayerPrefs which indicates whether the start info has displayed
        /// at least one time.
        private const string _hasDisplayedStartInfoKey = "HasDisplayedStartInfo";

        /// The key name used in PlayerPrefs which stores persistent Cloud Anchors history data.
        /// Expired data will be cleared at runtime.
        private const string _persistentCloudAnchorsStorageKey = "PersistentCloudAnchors";

        /// The limitation of how many Cloud Anchors can be stored in local storage.
        private const int _storageLimit = 40;

        /// Sample application modes.
        public enum ApplicationMode
        {
            /// Ready to host or resolve.
            Ready,

            /// Hosting Cloud Anchors.
            Hosting,

            /// Resolving Cloud Anchors.
            Resolving,
        }
 
        /// Gets the current main camera.
        public Camera MainCamera
        {
            get
            {
#if ARCORE_USE_ARF_5 // use ARF 5
                return Origin.Camera;
#else // use ARF 4
                return SessionOrigin.camera;
#endif
            }
        }

        /// Callback handling "Begin to host" button click event in Home Page.
        public void OnHostButtonClicked()
        {
            Mode = ApplicationMode.Hosting;
            SwitchToPrivacyPrompt();
        }

        /// Callback handling "Begin to resolve" button click event in Home Page.
        public void OnResolveButtonClicked()
        {
            Mode = ApplicationMode.Resolving;
            SwitchToResolveMenu();
        }

        /// Callback handling "Learn More" Button click event in Privacy Prompt.
        public void OnLearnMoreButtonClicked()
        {
            Application.OpenURL(
                "https://developers.google.com/ar/data-privacy");
        }

        /// Switch to home page, and disable all other screens.
        public void SwitchToHomePage()
        {
            ResetAllViews();
            Mode = ApplicationMode.Ready;
            ResolvingSet.Clear();
            HomePage.SetActive(true);
        }
  
        /// Switch to resolve menu, and disable all other screens.
        public void SwitchToResolveMenu()
        {
            ResetAllViews();
            ResolveMenu.SetActive(true);
        }

        /// Switch to privacy prompt, and disable all other screens.
        public void SwitchToPrivacyPrompt()
        {
            if (PlayerPrefs.HasKey(_hasDisplayedStartInfoKey))
            {
                SwitchToARView();
                return;
            }

            ResetAllViews();
            PrivacyPrompt.SetActive(true);
        }

        /// Switch to AR view, and disable all other screens.
        public void SwitchToARView()
        {
            ResetAllViews();
            PlayerPrefs.SetInt(_hasDisplayedStartInfoKey, 1);
            ARView.SetActive(true);
            SetPlatformActive(true);
        }

        /// Load the persistent Cloud Anchors history from local storage,
        /// also remove outdated records and update local history data. 
        /// <returns>A collection of persistent Cloud Anchors history data.</returns>
        public CloudAnchorHistoryCollection LoadCloudAnchorHistory()
        {
            if (PlayerPrefs.HasKey(_persistentCloudAnchorsStorageKey))
            {
                var history = JsonUtility.FromJson<CloudAnchorHistoryCollection>(
                    PlayerPrefs.GetString(_persistentCloudAnchorsStorageKey));

                // Remove all records created more than 24 hours and update stored history.
                DateTime current = DateTime.Now;
                history.Collection.RemoveAll(
                    data => current.Subtract(data.CreatedTime).Days > 0);
                PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey,
                    JsonUtility.ToJson(history));
                return history;
            }

            return new CloudAnchorHistoryCollection();
        }

         
        /// Save the persistent Cloud Anchors history to local storage,
        /// also remove the oldest data if current storage has met maximal capacity.>
        public void SaveCloudAnchorHistory(CloudAnchorHistory data)
        {
            var history = LoadCloudAnchorHistory();

            // Sort the data from latest record to oldest record which affects the option order in
            // multiselection dropdown.
            history.Collection.Add(data);
            history.Collection.Sort((left, right) => right.CreatedTime.CompareTo(left.CreatedTime));

            // Remove the oldest data if the capacity exceeds storage limit.
            if (history.Collection.Count > _storageLimit)
            {
                history.Collection.RemoveRange(
                    _storageLimit, history.Collection.Count - _storageLimit);
            }

            PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey, JsonUtility.ToJson(history));
        }

        /// The Unity Awake() method.
        public void Awake()
        {
            // Lock screen to portrait.
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.Portrait;

            // Enable Persistent Cloud Anchors sample to target 60fps camera capture frame rate
            // on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;
            SwitchToHomePage();
        }

        /// The Unity Update() method.
        public void Update()
        {
            // On home page, pressing 'back' button quits the app.
            // Otherwise, returns to home page.
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                if (HomePage.activeSelf)
                {
                    Application.Quit();
                }
                else
                {
                    SwitchToHomePage();
                }
            }
        }

        private void ResetAllViews()
        {
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            SetPlatformActive(false);
            ARView.SetActive(false);
            PrivacyPrompt.SetActive(false);
            ResolveMenu.SetActive(false);
            HomePage.SetActive(false);
        }

        private void SetPlatformActive(bool active)
        {
#if ARCORE_USE_ARF_5 // use ARF 5
            Origin.gameObject.SetActive(active);
#else // use ARF 4
            SessionOrigin.gameObject.SetActive(active);
#endif
            SessionCore.gameObject.SetActive(active);
            Extensions.gameObject.SetActive(active);
        }
    }
}
