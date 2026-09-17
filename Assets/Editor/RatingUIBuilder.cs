using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace InclusiveEHMI.EditorTools
{
    // Step 9 build tool: creates a screen-space "RatingCanvas" (question text
    // + 5 numbered buttons over a dim backing panel) in the currently open
    // ExperimentScene, attaches RatingUI, and wires up the scene's
    // ExperimentManager to reference it. Also ensures an EventSystem exists
    // using InputSystemUIInputModule -- this project's Active Input Handling
    // is "Input System Package (New)" only, under which the legacy
    // StandaloneInputModule does not receive input.
    //
    // Idempotent -- destroys and rebuilds "RatingCanvas" each run, safe to re-run.
    public static class RatingUIBuilder
    {
        private const string CanvasName = "RatingCanvas";
        private const string PanelName = "Panel";
        private const string QuestionTextName = "QuestionText";
        private const string ButtonRowName = "ButtonRow";

        [MenuItem("Tools/Inclusive eHMI/Step 9 - Build Rating UI")]
        public static void Build()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "ExperimentScene")
            {
                EditorUtility.DisplayDialog(
                    "Wrong Scene",
                    "Open Assets/Scenes/ExperimentScene.unity first (Tools > Inclusive eHMI > Build Experiment Scene, or double-click the scene asset), then run this again.",
                    "OK");
                return;
            }

            EnsureEventSystem();

            GameObject existingCanvas = GameObject.Find(CanvasName);
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas);
            }

            GameObject canvasGo = new GameObject(CanvasName, typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject panelGo = new GameObject(PanelName, typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 60f);
            panelRect.sizeDelta = new Vector2(900f, 220f);
            Image panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);

            GameObject questionGo = new GameObject(QuestionTextName, typeof(RectTransform));
            questionGo.transform.SetParent(panelGo.transform, false);
            RectTransform questionRect = questionGo.GetComponent<RectTransform>();
            questionRect.anchorMin = new Vector2(0f, 1f);
            questionRect.anchorMax = new Vector2(1f, 1f);
            questionRect.pivot = new Vector2(0.5f, 1f);
            questionRect.anchoredPosition = new Vector2(0f, -20f);
            questionRect.sizeDelta = new Vector2(-40f, 80f);
            TextMeshProUGUI questionText = questionGo.AddComponent<TextMeshProUGUI>();
            questionText.text = "Question";
            questionText.fontSize = 32f;
            questionText.alignment = TextAlignmentOptions.Center;
            questionText.color = Color.white;

            GameObject rowGo = new GameObject(ButtonRowName, typeof(RectTransform));
            rowGo.transform.SetParent(panelGo.transform, false);
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0f);
            rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, 30f);
            rowRect.sizeDelta = new Vector2(760f, 80f);
            HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var ratingButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                int rating = i + 1;
                ratingButtons[i] = BuildRatingButton(rowGo.transform, rating);
            }

            // Hide the panel BEFORE AddComponent<RatingUI>() below, since that call
            // triggers RatingUI.Awake() synchronously (Editor, outside Play Mode) --
            // any HideImmediate() it runs would otherwise act on a still-null
            // panelRoot and no-op, leaving the panel visibly active in Edit Mode.
            panelGo.SetActive(false);

            RatingUI ratingUI = canvasGo.AddComponent<RatingUI>();
            var so = new SerializedObject(ratingUI);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("questionText").objectReferenceValue = questionText;
            SerializedProperty buttonsProp = so.FindProperty("ratingButtons");
            buttonsProp.arraySize = ratingButtons.Length;
            for (int i = 0; i < ratingButtons.Length; i++)
            {
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = ratingButtons[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject experimentManagerGo = GameObject.Find("ExperimentManager");
            if (experimentManagerGo != null)
            {
                ExperimentManager experimentManager = experimentManagerGo.GetComponent<ExperimentManager>();
                if (experimentManager != null)
                {
                    var emso = new SerializedObject(experimentManager);
                    emso.FindProperty("ratingUI").objectReferenceValue = ratingUI;
                    emso.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.Refresh();

            Debug.Log($"[RatingUIBuilder] Built '{CanvasName}' (question text + 5 numbered buttons over a dim panel), " +
                      "attached RatingUI, and wired it into the scene's ExperimentManager (if present). Panel starts hidden.");

            Selection.activeGameObject = canvasGo;
        }

        private static Button BuildRatingButton(Transform parent, int rating)
        {
            GameObject buttonGo = new GameObject($"Button_{rating}", typeof(RectTransform));
            buttonGo.transform.SetParent(parent, false);

            Image image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.85f, 0.85f, 0.85f);

            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = rating.ToString();
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;

            return button;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem");
                eventSystem = eventSystemGo.AddComponent<EventSystem>();
            }

            // Legacy StandaloneInputModule relies on UnityEngine.Input, which
            // throws under this project's "Input System Package (New)" only
            // setting -- use InputSystemUIInputModule instead.
            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
    }
}
