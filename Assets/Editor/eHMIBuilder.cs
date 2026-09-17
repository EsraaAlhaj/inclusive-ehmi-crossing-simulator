using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace InclusiveEHMI.EditorTools
{
    // Step 6 build tool: creates the eHMI visual ("STOPPING" world-space
    // TextMeshPro over a dark high-contrast backing panel) and audio
    // (AudioSource) hierarchy on the AutomatedVehicle prefab, and attaches
    // eHMIController. Idempotent -- destroys and rebuilds the "EHMI" child
    // each run, safe to re-run.
    //
    // Placement/orientation: mounted at the front of the vehicle, near
    // FrontBumper's Z but offset slightly further forward to avoid
    // z-fighting with the Body mesh. The AutomatedVehicle root carries a
    // Y=180 rotation (see VehicleBodyBuilder) so its local forward points
    // toward world -Z. TextMeshPro/Quad geometry is authored to be readable
    // from world -Z by default (world rotation identity). A *local* Y=180
    // on "EHMI" cancels the root's Y=180 back out to world rotation
    // identity, so the text/panel face the fixed camera/pedestrian at
    // world -Z instead of facing away, toward the vehicle's direction of
    // travel. Confirmed correct orientation by manual test.
    //
    // Sizing: the "thin red line at 30m" legibility bug (fixed fontSize
    // far too small) is fixed by switching to TMP auto-sizing: the text
    // box width is fixed to fit within the ~1.9m vehicle body width, and
    // TMP grows the font as large as that width allows (single line, no
    // wrapping), maximizing legible size without guessing an absolute
    // point size.
    //
    // Long-distance (~36m) legibility pass: readable at ~15.8m but still
    // too small at ~36.4m through the fixed Game camera. Since the font is
    // already auto-sized to fill the width box, the only ways to grow
    // letter height further while staying on one line within the vehicle
    // width are (a) use more of the ~1.9m width budget itself, and
    // (b) tighten character spacing so the same 8-character word needs
    // less width per glyph, letting auto-size scale the font up further
    // (letter height scales with the font size the width-fill settles on).
    // Also switched text color to white (from red) for higher luminance
    // contrast against the dark panel, per instruction that this is an
    // acceptable trade-off if it materially improves readability.
    public static class EHMIBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/AutomatedVehicle.prefab";
        private const string MaterialsFolder = "Assets/Materials";

        private const string EHMIRootName = "EHMI";
        private const string VisualRootName = "VisualRoot";
        private const string EHMITextName = "EHMIText";
        private const string EHMIPanelName = "EHMIPanel";

        private const float BodyLength = 4.5f;         // must match VehicleBodyBuilder
        private const float ForwardClearance = 0.05f;  // meters in front of the Body mesh, avoids z-fighting
        private const float TextMountHeight = 0.9f;    // meters above ground, roughly windshield height

        // Text box: constrains the auto-sized font's width to use nearly
        // the full ~1.9m vehicle body width (small margin on each side
        // inside the panel, per the long-distance legibility pass -- was
        // 1.7m, widened to 1.8m). Height is generous so width remains the
        // binding constraint, per the requirement to maximize size "within
        // the ~1.9m vehicle width".
        private const float TextBoxWidth = 1.8f;
        private const float TextBoxHeight = 1f;
        private const float FontSizeMin = 0.1f;
        private const float FontSizeMax = 500f;

        // Negative character spacing tightens the gaps between the 8
        // letters of "STOPPING", freeing up width budget so auto-sizing
        // settles on a larger font (taller letters) while still fitting
        // TextBoxWidth on one line.
        private const float CharacterSpacing = -4f;

        // Backing panel: slightly larger than the text box so there's a
        // visible dark margin around the word, for a clean high-contrast
        // "sign" look. Grown alongside the text box (was 1.85 x 1.0) --
        // 1.2m height still fits within BodyHeight (1.5m) given
        // TextMountHeight = 0.9m (spans 0.3m to 1.5m).
        private const float PanelWidth = 1.88f;
        private const float PanelHeight = 1.2f;
        private const float PanelLocalZOffset = 0.03f; // behind the text, still clear of the Body mesh face

        [MenuItem("Tools/Inclusive eHMI/Step 6 - Build eHMI (Visual + Audio)")]
        public static void BuildEHMI()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Prefab Not Found",
                    $"Could not find {PrefabPath}. Run Step 2 (Tools > Inclusive eHMI > Place Vehicle Body) first.",
                    "OK");
                return;
            }

            using (var editScope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                GameObject root = editScope.prefabContentsRoot;

                Transform existingEHMI = root.transform.Find(EHMIRootName);
                if (existingEHMI != null)
                {
                    Object.DestroyImmediate(existingEHMI.gameObject);
                }

                GameObject ehmiRoot = new GameObject(EHMIRootName);
                ehmiRoot.transform.SetParent(root.transform);
                ehmiRoot.transform.localPosition = new Vector3(0f, TextMountHeight, BodyLength * 0.5f + ForwardClearance);
                ehmiRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                ehmiRoot.transform.localScale = Vector3.one;

                AudioSource audioSource = ehmiRoot.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D: always audible regardless of vehicle distance from the fixed camera/listener

                // Single toggle point for the whole visual (panel + text)
                // so they always show/hide together. Deliberately a sibling
                // of AudioSource (not a parent of it) so audio stays
                // independently controllable regardless of visual state.
                GameObject visualRoot = new GameObject(VisualRootName);
                visualRoot.transform.SetParent(ehmiRoot.transform);
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;
                visualRoot.transform.localScale = Vector3.one;

                // Dark high-contrast backing panel, sits just behind the text.
                GameObject panelGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panelGo.name = EHMIPanelName;
                Object.DestroyImmediate(panelGo.GetComponent<Collider>());
                panelGo.transform.SetParent(visualRoot.transform);
                panelGo.transform.localPosition = new Vector3(0f, 0f, PanelLocalZOffset);
                panelGo.transform.localRotation = Quaternion.identity;
                panelGo.transform.localScale = new Vector3(PanelWidth, PanelHeight, 1f);
                ApplyUnlitMaterial(panelGo, "M_EHMIPanel", new Color(0.05f, 0.05f, 0.05f));

                GameObject textGo = new GameObject(EHMITextName);
                textGo.transform.SetParent(visualRoot.transform);

                TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
                textGo.transform.localPosition = Vector3.zero;
                textGo.transform.localRotation = Quaternion.identity;
                textGo.transform.localScale = Vector3.one;

                tmp.text = "STOPPING";
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white; // higher luminance contrast against the dark panel than red, for long-distance legibility
                tmp.characterSpacing = CharacterSpacing; // tighten letter spacing to free width budget for a larger auto-sized font
                tmp.textWrappingMode = TextWrappingModes.NoWrap; // keep "STOPPING" on a single line
                tmp.enableAutoSizing = true;    // grow font to fill TextBoxWidth as much as possible
                tmp.fontSizeMin = FontSizeMin;
                tmp.fontSizeMax = FontSizeMax;
                tmp.rectTransform.sizeDelta = new Vector2(TextBoxWidth, TextBoxHeight);

                visualRoot.SetActive(false); // "no eHMI" is the default state -- hides panel + text together

                if (root.GetComponent<eHMIController>() == null)
                {
                    root.AddComponent<eHMIController>();
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[EHMIBuilder] Built '{EHMIRootName}' ('{VisualRootName}' containing auto-sized STOPPING TextMeshPro " +
                      $"over a dark panel, toggled as one unit + a sibling AudioSource) on {PrefabPath} and attached " +
                      "eHMIController. IMPORTANT: assign a spoken \"Vehicle stopping\" AudioClip to the eHMIController " +
                      "component's 'Vehicle Stopping Clip' field before testing audio -- no audio asset is created by this tool.");
        }

        private static void ApplyUnlitMaterial(GameObject go, string materialName, Color color)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                mat = new Material(shader) { name = materialName };
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", color);
                }
                Directory.CreateDirectory(MaterialsFolder);
                AssetDatabase.CreateAsset(mat, path);
            }

            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
