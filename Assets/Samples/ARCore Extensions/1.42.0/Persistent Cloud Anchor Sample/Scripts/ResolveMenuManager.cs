namespace Google.XR.ARCoreExtensions.Samples.PersistentCloudAnchors
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using UnityEngine.UI;

    /// A manager component that helps to populate and handle the options of resolving anchors.
    public class ResolveMenuManager : MonoBehaviour
    {    
        /// The main controller for Persistent Cloud Anchors sample.
        public PersistentCloudAnchorsController Controller;
   
        /// A multiselection dropdown component that contains all available resolving options.
        public MultiselectionDropdown Multiselection;
    
        /// An input field for manually typing Cloud Anchor Id(s).
        public InputField InputField;
   
        /// The warning text that appears when invalid characters are filled in.
        public GameObject InvalidInputWarning;
    
        /// The resolve button which leads to AR view screen.
        public Button ResolveButton;
   
        /// Cached Cloud Anchor history data used to fetch the Cloud Anchor Id using
        /// the index given by multi-selection dropdown.
        private CloudAnchorHistoryCollection _history = new CloudAnchorHistoryCollection();
   
        /// Cached active color for interactable buttons.
        private Color _activeColor;

        public ARViewManager arViewManager;


        /// Callback handling the validation of the input field.
        public void OnInputFieldValueChanged(string inputString)
        {
            // Input should only contain:
            // letters, numbers, hyphen(-), underscore(_), and comma(,).
            // Note: the valid character set is controlled by the validation rule of
            // the naming field in AR View.
            var regex = new Regex("^[a-zA-Z0-9-_,]*$");
            InvalidInputWarning.SetActive(!regex.IsMatch(inputString));
        }


        //public void OnAnchorCreated(CloudAnchor anchor)
        //{
        //    // Сохраняй имя и ID якоря в внешнем хранилище данных
        //    SaveAnchorToDatabase(anchor.Name, anchor.Id);
        //}

        /// Callback handling the end edit event of the input field.
        public void OnInputFieldEndEdit(string inputString)
        {
            //if (InvalidInputWarning.activeSelf)
            //{
            //    return;
            //}

            OnResolvingSelectionChanged();
        }

        /// Callback handling the selection values changed in multiselection dropdown and
        /// input field.
        public void OnResolvingSelectionChanged()
        {
            Controller.ResolvingSet.Clear();

            // Добавь идентификаторы облачных якорей из выпадающего списка.
            List<int> selectedIndex = Multiselection.SelectedValues;
            if (selectedIndex.Count > 0)
            {
                foreach (int index in selectedIndex)
                {
                    string anchorId = _history.Collection[index].Id;
                    Controller.ResolvingSet.Add(anchorId);

                    if (selectedIndex.Count == 1)
                    {
                        string anchorName = _history.Collection[selectedIndex[0]].Name;
                        if (arViewManager.anchorNameToId.ContainsKey(anchorName))
                        {
                            anchorId = arViewManager.anchorNameToId[anchorName];
                            InputField.text = anchorId;
                        }
                    }
                }
            }

            // Добавь идентификаторы облачных якорей из поля ввода.
            if (!InvalidInputWarning.activeSelf && InputField.text.Length > 0)
            {
                string[] inputIds = InputField.text.Split(',');
                if (inputIds.Length > 0)
                {
                    Controller.ResolvingSet.UnionWith(inputIds);
                }
            }

            // Обнови кнопку разрешения.
            SetButtonActive(ResolveButton, Controller.ResolvingSet.Count > 0);
        }

        /// The Unity Awake() method.
        public void Awake()
        {
            _activeColor = ResolveButton.GetComponent<Image>().color;
        }
   
        /// The Unity OnEnable() method.
        public void OnEnable()
        {
            SetButtonActive(ResolveButton, false);
            InvalidInputWarning.SetActive(false);
            InputField.text = string.Empty;
            _history = Controller.LoadCloudAnchorHistory();

            Multiselection.OnValueChanged += OnResolvingSelectionChanged;
            var options = new List<MultiselectionDropdown.OptionData>();
            foreach (var data in _history.Collection)
            {
                options.Add(new MultiselectionDropdown.OptionData(
                    data.Name, FormatDateTime(data.CreatedTime)));
            }

            Multiselection.Options = options;
        }

        /// The Unity OnDisable() method.
        public void OnDisable()
        {
            Multiselection.OnValueChanged -= OnResolvingSelectionChanged;
            Multiselection.Deselect();
            Multiselection.Options.Clear();
            _history.Collection.Clear();
        }

        private string FormatDateTime(DateTime time)
        {
            TimeSpan span = DateTime.Now.Subtract(time);
            return span.Hours == 0 ? span.Minutes == 0 ? "Just now" :
                string.Format("{0}m ago", span.Minutes) : string.Format("{0}h ago", span.Hours);
        }

        private void SetButtonActive(Button button, bool active)
        {
            button.GetComponent<Image>().color = active ? _activeColor : Color.grey;
            button.enabled = active;
        }
    }
}
