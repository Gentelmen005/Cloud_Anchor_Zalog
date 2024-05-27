namespace Google.XR.ARCoreExtensions.Samples.PersistentCloudAnchors
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;

    /// A manager component that helps with hosting and resolving Cloud Anchors.
    public class ARViewManager : MonoBehaviour
    {
        /// The main controller for Persistent Cloud Anchors sample.
        public PersistentCloudAnchorsController Controller;



        /// The 3D object that represents a Cloud Anchor.
        [SerializeField] public GameObject[] CloudAnchorPrefab;

        public int _spawnedObjectType = -1;
        public GameObject UI_objects;
        public GameObject SizeController;
        /// The game object that includes <see cref="MapQualityIndicator"/> to visualize
        /// map quality result.
        public GameObject MapQualityIndicatorPrefab;

        /// The UI element that displays the instructions to guide hosting experience.
        public GameObject InstructionBar;

        /// The UI panel that allows the user to name the Cloud Anchor.
        public GameObject NamePanel;

         
        /// The UI element that displays warning message for invalid input name.  
        public GameObject InputFieldWarning;

        /// The input field for naming Cloud Anchor.
        public InputField NameField;

        /// The instruction text in the top instruction bar.
        public Text InstructionText;

        /// Display the tracking helper text when the session in not tracking.
        //public Text TrackingHelperText;

        /// The debug text in bottom snack bar.
        //public Text DebugText;

        /// The button to save the typed name.
        public Button SaveButton;

        /// The button to save current cloud anchor id into clipboard.
        public Button ShareButton;

        /// Helper message for <see cref="NotTrackingReason.Initializing">.</see>
        private const string _initializingMessage = "Отслеживание инициализируется.";

        /// Helper message for <see cref="NotTrackingReason.Relocalizing">.</see>
        private const string _relocalizingMessage = "Отслеживание возобновляется после прерывания.";

        /// Helper message for <see cref="NotTrackingReason.InsufficientLight">.</see>
        private const string _insufficientLightMessage = "Слишком темно. Попробуйте переместиться в хорошо освещенное место.";

        /// Helper message for <see cref="NotTrackingReason.InsufficientLight">
        /// in Android S or above.</see>
        private const string _insufficientLightMessageAndroidS =
            "Слишком темно. Попробуйте переместиться в хорошо освещенное место " +
            "Кроме того, убедитесь, что в системных настройках для параметра Block Camera установлено значение off.";

        /// Helper message for <see cref="NotTrackingReason.InsufficientFeatures">.</see>
        private const string _insufficientFeatureMessage =
            "Не могу ничего найти. Направьте устройство на поверхность с большей текстурой или цветом.";
    
        /// Helper message for <see cref="NotTrackingReason.ExcessiveMotion">.</see>
        private const string _excessiveMotionMessage = "Двигаетесь слишком быстро. Притормозите.";

        /// Helper message for <see cref="NotTrackingReason.Unsupported">.</see>
        private const string _unsupportedMessage = "Отслеживание причин потери не поддерживается.";

        /// The time between enters AR View and ARCore session starts to host or resolve.
        private const float _startPrepareTime = 3.0f;

         
        /// Android 12 (S) SDK version.
        private const int _androidSSDKVesion = 31;

         
        private const string _pixelModel = "pixel";

         
        /// The timer to indicate whether the AR View has passed the start prepare time.
        private float _timeSinceStart;

        /// True if the app is in the process of returning to home page due to an invalid state,
        /// otherwise false.
        private bool _isReturning;

        /// The MapQualityIndicator that attaches to the placed object.
        private MapQualityIndicator _qualityIndicator = null;

        /// The history data that represents the current hosted Cloud Anchor.
        private CloudAnchorHistory _hostedCloudAnchor;

        /// An ARAnchor indicating the 3D object has been placed on a flat surface and
        /// is waiting for hosting.
        private ARAnchor _anchor = null;
  
        /// The promise for the async hosting operation, if any.
        private HostCloudAnchorPromise _hostPromise = null;

        /// The result of the hosting operation, if any.
        private HostCloudAnchorResult _hostResult = null;

        /// The coroutine for the hosting operation, if any.
        private IEnumerator _hostCoroutine = null;

        /// The promises for the async resolving operations, if any.
        private List<ResolveCloudAnchorPromise> _resolvePromises =
            new List<ResolveCloudAnchorPromise>();

        /// The results of the resolving operations, if any.
        private List<ResolveCloudAnchorResult> _resolveResults =
            new List<ResolveCloudAnchorResult>();

        /// The coroutines of the resolving operations, if any.
        private List<IEnumerator> _resolveCoroutines = new List<IEnumerator>();

        private Color _activeColor;
        private AndroidJavaClass _versionInfo;

        public Dictionary<string, string> anchorNameToId = new Dictionary<string, string>();

        public void SetSpawnedObjectType(int spawnedObjectType)
        {
            _spawnedObjectType = spawnedObjectType;
            UI_objects.SetActive(false);
            //SizeController.SetActive(true);
            //InstructionBar.SetActive(true);

        }

        /// Get the camera pose for the current frame.
        /// <returns>The camera pose of the current frame.</returns>
        public Pose GetCameraPose()
        {
            return new Pose(Controller.MainCamera.transform.position,
                Controller.MainCamera.transform.rotation);
        }
        
        //public void OnAnchorCreated(string anchor)
        //{
        //    // Сохраняй имя и ID якоря в словаре
        //    anchorNameToId[anchor.name] = anchor.cloudAnchorId;
        //}

        /// Callback handling the validation of the input field.
        /// <param name="inputString">The current value of the input field.</param>
        public void OnInputFieldValueChanged(string inputString)
        {
            // Cloud Anchor name should only contains: letters, numbers, hyphen(-), underscore(_).
            var regex = new Regex("^[a-zA-Z0-9-_]*$");
            InputFieldWarning.SetActive(!regex.IsMatch(inputString));
            SetSaveButtonActive(!InputFieldWarning.activeSelf && inputString.Length > 0);
        }



        /// Callback handling "Ok" button click event for input field.
        public void OnSaveButtonClicked()
        {
            _hostedCloudAnchor.Name = NameField.text;
            Controller.SaveCloudAnchorHistory(_hostedCloudAnchor);

            //DebugText.text = string.Format("Сохраненный облачный якорь:\n{0}.", _hostedCloudAnchor.Name);
            ShareButton.gameObject.SetActive(true);
            NamePanel.SetActive(false);
        }

        /// Callback handling "Share" button click event.
        public void OnShareButtonClicked()
        {
            GUIUtility.systemCopyBuffer = _hostedCloudAnchor.Id;
            //DebugText.text = "Идентификатор скопированного облака: " + _hostedCloudAnchor.Id;
        }

        /// The Unity Awake() method.
        public void Awake()
        {
            _activeColor = SaveButton.GetComponentInChildren<Text>().color;
            _versionInfo = new AndroidJavaClass("android.os.Build$VERSION");
        }

        /// The Unity OnEnable() method.
        public void OnEnable()
        {
            _timeSinceStart = 0.0f;
            _isReturning = false;
            _anchor = null;
            _qualityIndicator = null;
            _hostPromise = null;
            _hostResult = null;
            _hostCoroutine = null;
            _resolvePromises.Clear();
            _resolveResults.Clear();
            _resolveCoroutines.Clear();

            UI_objects.SetActive(true);

            InstructionBar.SetActive(true);
            NamePanel.SetActive(false);
            InputFieldWarning.SetActive(false);
            ShareButton.gameObject.SetActive(false);
            UpdatePlaneVisibility(true);

            switch (Controller.Mode)
            {
                case PersistentCloudAnchorsController.ApplicationMode.Ready:
                    ReturnToHomePage("Неверный режим приложения, возврат на главную страницу...");
                    break;
                case PersistentCloudAnchorsController.ApplicationMode.Hosting:
                case PersistentCloudAnchorsController.ApplicationMode.Resolving:
                    InstructionText.text = "Обнаружение плоской поверхности...";
                    //DebugText.text = "ARCore готовится к " + Controller.Mode;
                    break;
            }
        }

        /// The Unity OnDisable() method.
        public void OnDisable()
        {
            if (_qualityIndicator != null)
            {
                Destroy(_qualityIndicator.gameObject);
                _qualityIndicator = null;
            }

            if (_anchor != null)
            {
                Destroy(_anchor.gameObject);
                _anchor = null;
            }

            if (_hostCoroutine != null)
            {
                StopCoroutine(_hostCoroutine);
            }

            _hostCoroutine = null;

            if (_hostPromise != null)
            {
                _hostPromise.Cancel();
                _hostPromise = null;
            }

            _hostResult = null;

            foreach (var coroutine in _resolveCoroutines)
            {
                StopCoroutine(coroutine);
            }

            _resolveCoroutines.Clear();

            foreach (var promise in _resolvePromises)
            {
                promise.Cancel();
            }

            _resolvePromises.Clear();

            foreach (var result in _resolveResults)
            {
                if (result.Anchor != null)
                {
                    Destroy(result.Anchor.gameObject);
                }
            }

            _resolveResults.Clear();
            UpdatePlaneVisibility(false);
            //_spawnedObjectType = -1;
        }
        
        /// The Unity Update() method.
        public void Update()
        {
            // Give ARCore some time to prepare for hosting or resolving.
            if (_timeSinceStart < _startPrepareTime)
            {
                _timeSinceStart += Time.deltaTime;
                if (_timeSinceStart >= _startPrepareTime)
                {
                    UpdateInitialInstruction();
                }

                return;
            }

            ARCoreLifecycleUpdate();
            if (_isReturning)
            {
                return;
            }

            if (_timeSinceStart >= _startPrepareTime)
            {
                //DisplayTrackingHelperMessage();
            }

            if (Controller.Mode == PersistentCloudAnchorsController.ApplicationMode.Resolving)
            {
                ResolvingCloudAnchors();
                UI_objects.SetActive(false);
            }
            else if (Controller.Mode == PersistentCloudAnchorsController.ApplicationMode.Hosting)
            {
                // Perform hit test and place an anchor on the hit test result.
                if (_anchor == null)
                {
                    // If the player has not touched the screen then the update is complete.
                    Touch touch;
                    if (Input.touchCount < 1 ||
                        (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                    {
                        return;
                    }

                    // Ignore the touch if it's pointing on UI objects.
                    if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    {
                        return;
                    }

                    // Perform hit test and place a pawn object.
                    PerformHitTest(touch.position);
                }

                HostingCloudAnchor();
            }
        }

        private void PerformHitTest(Vector2 touchPos)
        {
            List<ARRaycastHit> hitResults = new List<ARRaycastHit>();
            Controller.RaycastManager.Raycast(
                touchPos, hitResults, TrackableType.PlaneWithinPolygon);

            // If there was an anchor placed, then instantiate the corresponding object.
            var planeType = PlaneAlignment.HorizontalUp;
            if (hitResults.Count > 0)
            {
                ARPlane plane = Controller.PlaneManager.GetPlane(hitResults[0].trackableId);
                if (plane == null)
                {
                    Debug.LogWarningFormat("Failed to find the ARPlane with TrackableId {0}",
                        hitResults[0].trackableId);
                    return;
                }

                planeType = plane.alignment;
                var hitPose = hitResults[0].pose;
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    // Point the hitPose rotation roughly away from the raycast/camera
                    // to match ARCore.
                    hitPose.rotation.eulerAngles =
                        new Vector3(0.0f, Controller.MainCamera.transform.eulerAngles.y, 0.0f);
                }

                _anchor = Controller.AnchorManager.AttachAnchor(plane, hitPose);
            }

            if (_anchor != null)
            {
                Instantiate(CloudAnchorPrefab[_spawnedObjectType], _anchor.transform);

                // Attach map quality indicator to this anchor.
                var indicatorGO =
                    Instantiate(MapQualityIndicatorPrefab, _anchor.transform);
                _qualityIndicator = indicatorGO.GetComponent<MapQualityIndicator>();
                _qualityIndicator.DrawIndicator(planeType, Controller.MainCamera);

                InstructionText.text = " Чтобы сохранить это место, обойдите объект вокруг, чтобы " +
                    "снимайте его с разных ракурсов";
                //DebugText.text = "Ожидание достаточной четкости отображения...";

                // Hide plane generator so users can focus on the object they placed.
                UpdatePlaneVisibility(false);
            }
        }

        private void HostingCloudAnchor()
        {
            // There is no anchor for hosting.
            if (_anchor == null)
            {
                return;
            }

            // There is a pending or finished hosting task.
            if (_hostPromise != null || _hostResult != null)
            {
                return;
            }

            // Update map quality:
            int qualityState = 2;
            // Can pass in ANY valid camera pose to the mapping quality API.
            // Ideally, the pose should represent users’ expected perspectives.
            FeatureMapQuality quality =
                Controller.AnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());
            //DebugText.text = "Текущее качество картографии: " + quality;
            qualityState = (int)quality;
            _qualityIndicator.UpdateQualityState(qualityState);

            // Hosting instructions:
            var cameraDist = (_qualityIndicator.transform.position -
                Controller.MainCamera.transform.position).magnitude;
            if (cameraDist < _qualityIndicator.Radius * 1.5f)
            {
                InstructionText.text = "Вы слишком близко, отойдите назад.";
                return;
            }
            else if (cameraDist > 10.0f)
            {
                InstructionText.text = "Ты слишком далеко, подойди ближе..";
                return;
            }
            else if (_qualityIndicator.ReachTopviewAngle)
            {
                InstructionText.text =
                    "Вы смотрите сверху, перемещайтесь со всех сторон..";
                return;
            }
            else if (!_qualityIndicator.ReachQualityThreshold)
            {
                InstructionText.text = "Сохраните объект, захватив его со всех сторон..";
                return;
            }

            // Start hosting:
            InstructionText.text = "Обработка...";
            //DebugText.text = "Качество отображения достигло достаточного порога, " +
            //    "создание облачного якоря.";
            //DebugText.text = string.Format(
            //    "FeatureMapQuality has reached {0}, triggering CreateCloudAnchor.",
            //    Controller.AnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose()));

            // Creating a Cloud Anchor with lifetime = 1 day.
            // This is configurable up to 365 days when keyless authentication is used.
            var promise = Controller.AnchorManager.HostCloudAnchorAsync(_anchor, 1);
            if (promise.State == PromiseState.Done)
            {
                Debug.LogFormat("Не удалось разместить облачный якорь.");
                OnAnchorHostedFinished(false);
            }
            else
            {
                _hostPromise = promise;
                _hostCoroutine = HostAnchor();
                StartCoroutine(_hostCoroutine);
            }
        }

        private IEnumerator HostAnchor()
        {
            yield return _hostPromise;
            _hostResult = _hostPromise.Result;
            _hostPromise = null;

            if (_hostResult.CloudAnchorState == CloudAnchorState.Success)
            {
                int count = Controller.LoadCloudAnchorHistory().Collection.Count;
                _hostedCloudAnchor =
                    new CloudAnchorHistory("CloudAnchor" + count, _hostResult.CloudAnchorId);
                OnAnchorHostedFinished(true, _hostResult.CloudAnchorId);

                // Добавь эту строку для сохранения имени и ID якоря в словаре
                anchorNameToId[_hostedCloudAnchor.Name] = _hostedCloudAnchor.Id;
            }
            else
            {
                OnAnchorHostedFinished(false, _hostResult.CloudAnchorState.ToString());
            }
        }

        private void ResolvingCloudAnchors()
        {
            // No Cloud Anchor for resolving.
            if (Controller.ResolvingSet.Count == 0)
            {
                return;
            }

            // There are pending or finished resolving tasks.
            if (_resolvePromises.Count > 0 || _resolveResults.Count > 0)
            {
                return;
            }

            // ARCore session is not ready for resolving.
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                return;
            }

            Debug.LogFormat("Attempting to resolve {0} Cloud Anchor(s): {1}",
                Controller.ResolvingSet.Count,
                string.Join(",", new List<string>(Controller.ResolvingSet).ToArray()));
            foreach (string cloudId in Controller.ResolvingSet)
            {
                var promise = Controller.AnchorManager.ResolveCloudAnchorAsync(cloudId);
                if (promise.State == PromiseState.Done)
                {
                    Debug.LogFormat("Не удается найти облачный якорь " + cloudId);
                    OnAnchorResolvedFinished(false, cloudId);
                }
                else
                {
                    _resolvePromises.Add(promise);
                    var coroutine = ResolveAnchor(cloudId, promise);
                    StartCoroutine(coroutine);
                }
            }

            Controller.ResolvingSet.Clear();
        }

        private IEnumerator ResolveAnchor(string cloudId, ResolveCloudAnchorPromise promise)
        {
            yield return promise;
            var result = promise.Result;
            _resolvePromises.Remove(promise);
            _resolveResults.Add(result);

            if (result.CloudAnchorState == CloudAnchorState.Success)
            {
                OnAnchorResolvedFinished(true, cloudId);
                Instantiate(CloudAnchorPrefab[_spawnedObjectType], result.Anchor.transform);
            }
            else
            {
                OnAnchorResolvedFinished(false, cloudId, result.CloudAnchorState.ToString());
            }
        }

        private void OnAnchorHostedFinished(bool success, string response = null)
        {
            if (success)
            {
                InstructionText.text = "Финиш!";
                Invoke("DoHideInstructionBar", 1.5f);
                //DebugText.text =
                //    string.Format("Succeed to host the Cloud Anchor: {0}.", response);

                // Display name panel and hide instruction bar.
                NameField.text = _hostedCloudAnchor.Name;
                NamePanel.SetActive(true);
                SetSaveButtonActive(true);
            }
            else
            {
                InstructionText.text = "Сбой в работе хоста.";
                //DebugText.text = "Не удалось разместить облачный якорь" + (response == null ? "." :
                //    "с ошибкой " + response + ".");
            }
        }

        private void OnAnchorResolvedFinished(bool success, string cloudId, string response = null)
        {
            if (success)
            {
                InstructionText.text = "Успешно!!";
                //DebugText.text =
                //    string.Format("Успех в решении проблемы облачного якоря: {0}.", cloudId);
                //OnAnchorCreated(response);
            }
            else
            {
                InstructionText.text = "Решить проблему не удалось.";
                //DebugText.text = "Не удалось разрешить облачный якорь: " + cloudId +
                //    (response == null ? "." : "с ошибкой " + response + ".");
            }
        }



        private void UpdateInitialInstruction()
        {
            switch (Controller.Mode)
            {
                case PersistentCloudAnchorsController.ApplicationMode.Hosting:
                    // Initial instruction for hosting flow:
                    InstructionText.text = "Нажмите, чтобы создать объект.";
                    //DebugText.text = "Коснитесь вертикальной или горизонтальной плоскости...";
                    return;
                case PersistentCloudAnchorsController.ApplicationMode.Resolving:
                    // Initial instruction for resolving flow:
                    InstructionText.text =
                        "Посмотрите на место, где вы ожидаете увидеть появление объекта..";
                    //DebugText.text = string.Format("Attempting to resolve {0} anchors...",
                    //    Controller.ResolvingSet.Count);
                    return;
                default:
                    return;
            }
        }

        private void UpdatePlaneVisibility(bool visible)
        {
            foreach (var plane in Controller.PlaneManager.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }

        private void ARCoreLifecycleUpdate()
        {
            // Only allow the screen to sleep when not tracking.
            var sleepTimeout = SleepTimeout.NeverSleep;
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                sleepTimeout = SleepTimeout.SystemSetting;
            }

            Screen.sleepTimeout = sleepTimeout;

            if (_isReturning)
            {
                return;
            }

            // Return to home page if ARSession is in error status.
            if (ARSession.state != ARSessionState.Ready &&
                ARSession.state != ARSessionState.SessionInitializing &&
                ARSession.state != ARSessionState.SessionTracking)
            {
                ReturnToHomePage(string.Format(
                    "ARCore encountered an error state {0}. Please start the app again.",
                    ARSession.state));
            }
        }

        //private void DisplayTrackingHelperMessage()
        //{
        //    if (_isReturning || ARSession.notTrackingReason == NotTrackingReason.None)
        //    {
        //        TrackingHelperText.gameObject.SetActive(false);
        //    }
        //    else
        //    {
        //        TrackingHelperText.gameObject.SetActive(true);
        //        switch (ARSession.notTrackingReason)
        //        {
        //            case NotTrackingReason.Initializing:
        //                TrackingHelperText.text = _initializingMessage;
        //                return;
        //            case NotTrackingReason.Relocalizing:
        //                TrackingHelperText.text = _relocalizingMessage;
        //                return;
        //            case NotTrackingReason.InsufficientLight:
        //                if (_versionInfo.GetStatic<int>("SDK_INT") < _androidSSDKVesion)
        //                {
        //                    TrackingHelperText.text = _insufficientLightMessage;
        //                }
        //                else
        //                {
        //                    TrackingHelperText.text = _insufficientLightMessageAndroidS;
        //                }

        //                return;
        //            case NotTrackingReason.InsufficientFeatures:
        //                TrackingHelperText.text = _insufficientFeatureMessage;
        //                return;
        //            case NotTrackingReason.ExcessiveMotion:
        //                TrackingHelperText.text = _excessiveMotionMessage;
        //                return;
        //            case NotTrackingReason.Unsupported:
        //                TrackingHelperText.text = _unsupportedMessage;
        //                return;
        //            default:
        //                TrackingHelperText.text =
        //                    string.Format("Not tracking reason: {0}", ARSession.notTrackingReason);
        //                return;
        //        }
        //    }
        //}

        private void ReturnToHomePage(string reason)
        {
            Debug.Log("Возвращение домой по причине: " + reason);
            if (_isReturning)
            {
                return;
            }

            _isReturning = true;
            //DebugText.text = reason;
            Invoke("DoReturnToHomePage", 3.0f);
        }

        private void DoReturnToHomePage()
        {
            Controller.SwitchToHomePage();
        }

        private void DoHideInstructionBar()
        {
            InstructionBar.SetActive(false);
        }

        private void SetSaveButtonActive(bool active)
        {
            SaveButton.enabled = active;
            SaveButton.GetComponentInChildren<Text>().color = active ? _activeColor : Color.gray;
        }
    }
}
