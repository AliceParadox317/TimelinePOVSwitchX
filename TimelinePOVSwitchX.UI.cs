using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI.Utilities;
using RuntimeUnityEditor.Core.Utils;
using Studio;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Timeline;
using UnityEngine;
using UnityEngine.UI;

namespace TimelinePOVSwitchX
{
    public partial class TimelinePOVSwitchX
    {
        private void Update()
        {
            // =====================================================
            // GET TIMELINE INSTANCE
            // =====================================================

            if (_timeline == null)
            {
                _timeline =
                    Singleton<Timeline.Timeline>.Instance;


                if (_timeline == null)
                    return;
            }


            // Unity creates a separate runtime dropdown object when the
            // list is opened. Stretch that LIVE copy, not only the template.
            FixLivePovDropdownWidth();

            // F1 master switch + dependency detection.
            //
            // The toolbar button exists only when:
            // 1. Enable Mirror Function = true
            // 2. Az.Studio.PlanarReflection is actually detected.
            if (!IsMirrorFunctionAvailable())
            {
                if (permanentMirrorToolbarButton != null)
                {
                    UnityEngine.Object.Destroy(
                        permanentMirrorToolbarButton.gameObject
                    );

                    permanentMirrorToolbarButton =
                        null;
                }

                // Temporary Timeline mirror belongs to this feature, so remove
                // it immediately if the feature is disabled/unavailable.
                DeleteActivePovMirror();

                toolbarStableFrames = 0;
                lastToolbarLayoutSignature = "";
            }
            else
            {
                TryAddPermanentMirrorToolbarButtonWhenStable();
            }



            // =====================================================
            // CHANGE POV SWITCH TOOLTIP
            //
            // Timeline normally displays:
            //
            // T: 00:05
            // V: 108
            //
            // We change ONLY our POV keyframes to:
            //
            // T: 00:05
            // V: Alice
            //
            // The actual stored value stays "108".
            // =====================================================

            if (timelineTooltip == null)
            {
                timelineTooltip =
                    (Text)_timeline.GetPrivate(
                        "_tooltip"
                    );
            }


            if (
                timelineTooltip != null &&
                timelineTooltip.transform.parent != null &&
                timelineTooltip.transform.parent.gameObject.activeInHierarchy
            )
            {
                Dictionary<int, Interpolable> interpolables =
                    (Dictionary<int, Interpolable>)
                    _timeline.GetPrivate(
                        "_interpolables"
                    );


                foreach (
                    Interpolable interpolable
                    in interpolables.Values
                )
                {
                    if (
                        interpolable.id !=
                        "POVSwitch"
                    )
                    {
                        continue;
                    }


                    foreach (
                        KeyValuePair<float, Timeline.Keyframe> pair
                        in interpolable.keyframes
                    )
                    {
                        string tooltipStoredValue =
      pair.Value.value as string;


                        if (tooltipStoredValue == null)
                            continue;


                        string[] tooltipParts =
                            tooltipStoredValue.Split('|');


                        int sceneId;


                        if (
                            !int.TryParse(
                                tooltipParts[0],
                                out sceneId
                            )
                        )
                        {
                            continue;
                        }


                        float keyframeTime =
                            pair.Key;


                        // Build exactly the same tooltip
                        // Timeline normally builds.
                        string normalTooltip =
        string.Format(
            "T: {0:00}:{1:00.########}\nV: {2}",
            Mathf.FloorToInt(
                keyframeTime / 60f
            ),
            keyframeTime % 60f,
            tooltipStoredValue
        );


                        // If this isn't the currently hovered
                        // POV keyframe, ignore it.
                        if (
                            timelineTooltip.text !=
                            normalTooltip
                        )
                        {
                            continue;
                        }



                        // =========================================
                        // FIND CHARACTER NAME FOR THIS SCENE ID
                        // =========================================

                        List<KeyValuePair<int, OCIChar>> tooltipCharacters =
                            new List<KeyValuePair<int, OCIChar>>();


                        foreach (
                            KeyValuePair<int, ObjectCtrlInfo> characterPair
                            in Studio.Studio.Instance.dicObjectCtrl
                        )
                        {
                            OCIChar character =
                                characterPair.Value as OCIChar;


                            if (character == null)
                                continue;


                            tooltipCharacters.Add(
                                new KeyValuePair<int, OCIChar>(
                                    characterPair.Key,
                                    character
                                )
                            );
                        }


                        tooltipCharacters.Sort(
                            (a, b) =>
                                a.Key.CompareTo(
                                    b.Key
                                )
                        );


                        Dictionary<string, int> tooltipDuplicateCounts =
                            new Dictionary<string, int>();


                        string characterDisplayName =
                            sceneId == -1
                                ? "Disable POV"
                                : null;


                        foreach (
                            KeyValuePair<int, OCIChar> characterPair
                            in tooltipCharacters
                        )
                        {
                            if (sceneId == -1)
                                break;


                            string baseName =
                                characterPair.Value
                                    .treeNodeObject
                                    .textName;


                            int count;

                            string displayName;


                            if (
                                !tooltipDuplicateCounts.TryGetValue(
                                    baseName,
                                    out count
                                )
                            )
                            {
                                tooltipDuplicateCounts[baseName] =
                                    0;


                                displayName =
                                    baseName;
                            }
                            else
                            {
                                count++;


                                tooltipDuplicateCounts[baseName] =
                                    count;


                                displayName =
                                    baseName +
                                    "(" +
                                    count +
                                    ")";
                            }


                            if (
                                characterPair.Key ==
                                sceneId
                            )
                            {
                                characterDisplayName =
                                    displayName;

                                break;
                            }
                        }


                        if (
                            characterDisplayName != null
                        )
                        {
                            timelineTooltip.text =
                                string.Format(
                                    "T: {0:00}:{1:00.########}\nV: {2}",
                                    Mathf.FloorToInt(
                                        keyframeTime / 60f
                                    ),
                                    keyframeTime % 60f,
                                    characterDisplayName
                                );
                        }


                        break;
                    }


                    break;
                }
            }



            // =====================================================
            // CREATE POV DROPDOWN ONCE
            // =====================================================

            if (povDropdown == null)
            {
                timelineValueText =
                    (Text)_timeline.GetPrivate(
                        "_keyframeValueText"
                    );


                if (timelineValueText == null)
                    return;


                // Create our own Unity UI Dropdown.
                // Do NOT clone ComponentUtil / RuntimeUnityEditor UI here:
                // TimelinePOVSwitchX must work when those plugins are absent.
                povDropdown =
                    CreatePovDropdown(
                        timelineValueText.transform.parent,
                        timelineValueText
                    );


                if (povDropdown == null)
                    return;


                povDropdown.name =
                    "POVSwitchDropdown";


                povDropdown.ClearOptions();


                // =============================================
                // USER CHANGED CHARACTER
                // =============================================

                povDropdown.onValueChanged.AddListener(
                    index =>
                    {
                        if (updatingDropdown)
                            return;


                        List<KeyValuePair<float, Timeline.Keyframe>> selected =
                            (List<KeyValuePair<float, Timeline.Keyframe>>)
                            _timeline.GetPrivate(
                                "_selectedKeyframes"
                            );


                        if (
                            selected.Count !=
                            1
                        )
                        {
                            return;
                        }


                        if (
                            selected[0].Value.parent.id !=
                            "POVSwitch"
                        )
                        {
                            return;
                        }


                        if (
                            index < 0 ||
                            index >= povCharacterIds.Count
                        )
                        {
                            return;
                        }


                        int sceneId =
                            povCharacterIds[index];


                        // Change ONLY the character ID.
                        // Preserve this keyframe's view mode and any
                        // stored Custom settings.
                        string oldStoredValue =
                            selected[0].Value.value as string;


                        if (string.IsNullOrEmpty(oldStoredValue))
                        {
                            selected[0].Value.value =
                                sceneId.ToString() + "|none|force1|mirror0";
                        }
                        else
                        {
                            string[] oldParts =
                                oldStoredValue.Split('|');


                            oldParts[0] =
                                sceneId.ToString();


                            selected[0].Value.value =
                                string.Join("|", oldParts);
                        }
                    }
                );


                povDropdown.gameObject.SetActive(
                    false
                );


                return;
            }



            // =====================================================
            // GET SELECTED TIMELINE KEYFRAME
            // =====================================================

            List<KeyValuePair<float, Timeline.Keyframe>> selectedKeyframes =
                (List<KeyValuePair<float, Timeline.Keyframe>>)
                _timeline.GetPrivate(
                    "_selectedKeyframes"
                );


            if (
                selectedKeyframes.Count !=
                1
            )
            {
                povDropdown.gameObject.SetActive(
                    false
                );


                timelineValueText.gameObject.SetActive(
                    true
                );


                return;
            }


            Timeline.Keyframe selectedKeyframe =
                selectedKeyframes[0].Value;



            // =====================================================
            // NOT A POV SWITCH KEYFRAME
            // =====================================================

            if (
                selectedKeyframe.parent.id !=
                "POVSwitch"
            )
            {
                povDropdown.gameObject.SetActive(
                    false
                );


                timelineValueText.gameObject.SetActive(
                    true
                );


                return;
            }



            // =====================================================
            // POV SWITCH KEYFRAME SELECTED
            // =====================================================

            timelineValueText.gameObject.SetActive(
                false
            );


            povDropdown.gameObject.SetActive(
                true
            );



            // =====================================================
            // GET CURRENT CHARACTERS
            // =====================================================

            List<KeyValuePair<int, OCIChar>> characters =
                new List<KeyValuePair<int, OCIChar>>();


            foreach (
                KeyValuePair<int, ObjectCtrlInfo> pair
                in Studio.Studio.Instance.dicObjectCtrl
            )
            {
                OCIChar character =
                    pair.Value as OCIChar;


                if (character == null)
                    continue;


                characters.Add(
                    new KeyValuePair<int, OCIChar>(
                        pair.Key,
                        character
                    )
                );
            }



            // =====================================================
            // SORT BY STUDIO OBJECT ID
            // =====================================================

            characters.Sort(
                (a, b) =>
                    a.Key.CompareTo(
                        b.Key
                    )
            );



            // =====================================================
            // BUILD CHARACTER NAMES
            //
            // Alice
            // Alice(1)
            // Alice(2)
            // Bob
            // =====================================================

            List<string> characterNames =
                new List<string>();


            List<int> currentCharacterIds =
                new List<int>();


            Dictionary<string, int> duplicateCounts =
                new Dictionary<string, int>();


            foreach (
                KeyValuePair<int, OCIChar> pair
                in characters
            )
            {
                int sceneId =
                    pair.Key;


                OCIChar character =
                    pair.Value;


                string baseName =
                    character.treeNodeObject.textName;


                int count;

                string displayName;


                if (
                    !duplicateCounts.TryGetValue(
                        baseName,
                        out count
                    )
                )
                {
                    duplicateCounts[baseName] =
                        0;


                    displayName =
                        baseName;
                }
                else
                {
                    count++;


                    duplicateCounts[baseName] =
                        count;


                    displayName =
                        baseName +
                        "(" +
                        count +
                        ")";
                }


                characterNames.Add(
                    displayName
                );


                currentCharacterIds.Add(
                    sceneId
                );
            }


            // Special Timeline action. -1 is reserved by this plugin
            // and is not a Studio character ID.
            characterNames.Add(
                "Disable POV"
            );

            currentCharacterIds.Add(
                -1
            );



            // =====================================================
            // CHECK IF CHARACTER LIST CHANGED
            // =====================================================

            bool listChanged =
                povDropdown.options.Count !=
                characterNames.Count;


            if (!listChanged)
            {
                for (
                    int i = 0;
                    i < characterNames.Count;
                    i++
                )
                {
                    if (
                        povDropdown.options[i].text !=
                        characterNames[i]
                    )
                    {
                        listChanged =
                            true;

                        break;
                    }


                    if (
                        i >= povCharacterIds.Count ||
                        povCharacterIds[i] !=
                        currentCharacterIds[i]
                    )
                    {
                        listChanged =
                            true;

                        break;
                    }
                }
            }



            // =====================================================
            // UPDATE DROPDOWN CHARACTER LIST
            // =====================================================

            if (listChanged)
            {
                updatingDropdown =
                    true;


                povDropdown.ClearOptions();


                povDropdown.AddOptions(
                    characterNames
                );


                povCharacterIds.Clear();


                povCharacterIds.AddRange(
                    currentCharacterIds
                );


                updatingDropdown =
                    false;
            }



            // =====================================================
            // GET SCENE ID STORED IN KEYFRAME
            // =====================================================

            string storedValue =
                selectedKeyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] storedParts =
                storedValue.Split('|');


            int storedSceneId;


            if (
                !int.TryParse(
                    storedParts[0],
                    out storedSceneId
                )
            )
            {
                return;
            }



            // =====================================================
            // FIND CORRESPONDING DROPDOWN OPTION
            // =====================================================

            int dropdownValue =
                -1;


            for (
                int i = 0;
                i < povCharacterIds.Count;
                i++
            )
            {
                if (
                    povCharacterIds[i] ==
                    storedSceneId
                )
                {
                    dropdownValue =
                        i;

                    break;
                }
            }



            // =====================================================
            // DISPLAY SELECTED CHARACTER
            // =====================================================

            if (
                dropdownValue != -1 &&
                povDropdown.value != dropdownValue
            )
            {
                updatingDropdown =
                    true;


                povDropdown.value =
                    dropdownValue;


                updatingDropdown =
                    false;
            }


        }


        // =========================================================
        // CREATE CHARACTER DROPDOWN
        //
        // Built entirely from UnityEngine.UI components so the plugin does
        // not depend on ComponentUtil merely to obtain a Dropdown template.
        // =========================================================

        private static Dropdown CreatePovDropdown(
            Transform parent,
            Text sourceText
        )
        {
            if (parent == null || sourceText == null)
                return null;


            Font font = sourceText.font;

            GameObject root =
                new GameObject(
                    "POVSwitchDropdown",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Dropdown)
                );

            RectTransform rootRect =
                root.GetComponent<RectTransform>();

            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.82f, 0.82f, 0.82f, 1f);

            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = rootImage;


            // Caption shown while the dropdown is closed.
            GameObject labelObject =
                new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text)
                );

            RectTransform labelRect =
                labelObject.GetComponent<RectTransform>();

            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);

            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 18;
            label.fontStyle = sourceText.fontStyle;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.38f, 0.38f, 0.38f, 1f);
            label.raycastTarget = false;
            label.supportRichText = false;

            dropdown.captionText = label;


            // Small arrow so it is visually obvious that this is a dropdown.
            GameObject arrowObject =
                new GameObject(
                    "Arrow",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text)
                );

            RectTransform arrowRect =
                arrowObject.GetComponent<RectTransform>();

            arrowRect.SetParent(rootRect, false);
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(18f, 0f);
            arrowRect.anchoredPosition = Vector2.zero;

            Text arrow = arrowObject.GetComponent<Text>();
            arrow.font = font;
            arrow.fontSize = 11;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.color = new Color(0.38f, 0.38f, 0.38f, 1f);
            arrow.text = "▼";
            arrow.raycastTarget = false;


            // Popup template.
            GameObject templateObject =
                new GameObject(
                    "Template",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ScrollRect)
                );

            RectTransform templateRect =
                templateObject.GetComponent<RectTransform>();

            templateRect.SetParent(rootRect, false);
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0f, 160f);

            Image templateImage = templateObject.GetComponent<Image>();
            templateImage.color = new Color(0.82f, 0.82f, 0.82f, 1f);


            // Viewport clips the option list to the popup rectangle.
            GameObject viewportObject =
                new GameObject(
                    "Viewport",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(RectMask2D)
                );

            RectTransform viewportRect =
                viewportObject.GetComponent<RectTransform>();

            viewportRect.SetParent(templateRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);


            // Content grows vertically as options are added.
            GameObject contentObject =
                new GameObject(
                    "Content",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter)
                );

            RectTransform contentRect =
                contentObject.GetComponent<RectTransform>();

            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();

            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentObject.GetComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;


            // One option template. Unity Dropdown clones this Toggle for every
            // character entry.
            GameObject itemObject =
                new GameObject(
                    "Item",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Toggle),
                    typeof(LayoutElement)
                );

            RectTransform itemRect =
                itemObject.GetComponent<RectTransform>();

            itemRect.SetParent(contentRect, false);
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(0f, 30f);

            Image itemBackground = itemObject.GetComponent<Image>();
            itemBackground.color = new Color(0.82f, 0.82f, 0.82f, 1f);

            LayoutElement itemLayout = itemObject.GetComponent<LayoutElement>();
            itemLayout.minHeight = 30f;
            itemLayout.preferredHeight = 30f;
            itemLayout.flexibleHeight = 0f;

            Toggle itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;

            ColorBlock toggleColors = itemToggle.colors;
            toggleColors.normalColor = Color.white;
            toggleColors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            toggleColors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            toggleColors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            itemToggle.colors = toggleColors;


            GameObject itemLabelObject =
                new GameObject(
                    "Item Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text)
                );

            RectTransform itemLabelRect =
                itemLabelObject.GetComponent<RectTransform>();

            itemLabelRect.SetParent(itemRect, false);
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(6f, 0f);
            itemLabelRect.offsetMax = new Vector2(-6f, 0f);

            Text itemLabel = itemLabelObject.GetComponent<Text>();
            itemLabel.font = font;
            itemLabel.fontSize = 18;
            itemLabel.fontStyle = sourceText.fontStyle;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.color = new Color(0.38f, 0.38f, 0.38f, 1f);
            itemLabel.raycastTarget = false;
            itemLabel.supportRichText = false;


            ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 60f;

            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;

            // Unity requires the template to be disabled until Show() clones it.
            templateObject.SetActive(false);

            return dropdown;
        }



        // =========================================================
        // FIX LIVE CHARACTER DROPDOWN WIDTH
        // =========================================================

        private static void FixLivePovDropdownWidth()
        {
            if (povDropdown == null)
                return;

            System.Reflection.FieldInfo dropdownField =
                AccessTools.Field(typeof(Dropdown), "m_Dropdown");

            if (dropdownField == null)
                return;

            GameObject liveDropdown =
                dropdownField.GetValue(povDropdown) as GameObject;

            if (liveDropdown == null)
                return;

            RectTransform sourceRect =
                povDropdown.GetComponent<RectTransform>();

            RectTransform liveRect =
                liveDropdown.GetComponent<RectTransform>();

            if (sourceRect == null || liveRect == null)
                return;

            float fullWidth = sourceRect.rect.width;

            if (fullWidth <= 0f)
                return;

            int optionCount = povDropdown.options.Count;

            if (optionCount <= 0)
                return;

            const float rowHeight = 30f;
            const int maxVisibleRows = 6;

            int visibleRows = Mathf.Min(optionCount, maxVisibleRows);
            float visibleHeight = visibleRows * rowHeight;

            // Only resize the LIVE popup. Do NOT reposition the option Toggles.
            // Unity Dropdown owns their ordering and click -> option-index mapping.
            // Manually moving those Toggles was what made selecting Disable POV
            // leave the other character effectively unreachable.
            liveRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                fullWidth
            );

            liveRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                visibleHeight
            );

            ScrollRect liveScrollRect =
                liveDropdown.GetComponentInChildren<ScrollRect>(true);

            if (liveScrollRect != null)
            {
                liveScrollRect.scrollSensitivity = 60f;
                liveScrollRect.horizontal = false;
                liveScrollRect.vertical = optionCount > maxVisibleRows;
                liveScrollRect.movementType = ScrollRect.MovementType.Clamped;

                RectTransform viewportRect = liveScrollRect.viewport;

                if (viewportRect != null)
                {
                    viewportRect.anchorMin = Vector2.zero;
                    viewportRect.anchorMax = Vector2.one;
                    viewportRect.offsetMin = Vector2.zero;
                    viewportRect.offsetMax = Vector2.zero;
                }

                // If every option fits, there is no reason for Unity to retain
                // a scroll offset around the currently selected option. This is
                // important when switching from Kii Shira to Disable POV and
                // reopening the two-row list.
                if (optionCount <= maxVisibleRows && liveScrollRect.content != null)
                    liveScrollRect.content.anchoredPosition = Vector2.zero;
            }

            // Width/font only. Leave vertical positions and Toggle callbacks alone.
            Toggle[] liveItems =
                liveDropdown.GetComponentsInChildren<Toggle>(true);

            foreach (Toggle itemToggle in liveItems)
            {
                if (!itemToggle.gameObject.activeSelf)
                    continue;

                RectTransform itemRect =
                    itemToggle.GetComponent<RectTransform>();

                if (itemRect != null)
                {
                    itemRect.anchorMin =
                        new Vector2(0f, itemRect.anchorMin.y);
                    itemRect.anchorMax =
                        new Vector2(1f, itemRect.anchorMax.y);
                    itemRect.offsetMin =
                        new Vector2(0f, itemRect.offsetMin.y);
                    itemRect.offsetMax =
                        new Vector2(0f, itemRect.offsetMax.y);
                }

                if (itemToggle.targetGraphic != null)
                {
                    RectTransform graphicRect =
                        itemToggle.targetGraphic.rectTransform;
                    graphicRect.anchorMin =
                        new Vector2(0f, graphicRect.anchorMin.y);
                    graphicRect.anchorMax =
                        new Vector2(1f, graphicRect.anchorMax.y);
                    graphicRect.offsetMin =
                        new Vector2(0f, graphicRect.offsetMin.y);
                    graphicRect.offsetMax =
                        new Vector2(0f, graphicRect.offsetMax.y);
                }

                Text rowText =
                    itemToggle.GetComponentInChildren<Text>(true);

                if (rowText != null)
                    rowText.fontSize = 18;
            }
        }



        // =========================================================
        // SEPARATE DRAGGABLE POV SETTINGS WINDOW
        // =========================================================

        private void LateUpdate()
        {
            if (activePovMirror == null)
                return;

            if (activePovMirrorDynamic)
            {
                UpdateActivePovMirror();
                return;
            }

            // Static mode needs to wait for PerspectiveX's POV camera to
            // settle after SwitchTo/EnablePov. The old code froze the mirror
            // immediately, which captured the pre-settle camera transform.
            if (activePovMirrorSettleFrames > 0)
            {
                UpdateActivePovMirror();

                activePovMirrorSettleFrames--;

                if (activePovMirrorSettleFrames == 0)
                {
                    Vector3 staticRotation =
                        activePovMirror.guideObject.changeAmount.rot;

                    staticRotation.y +=
                        180f;

                    activePovMirror.guideObject.changeAmount.rot =
                        staticRotation;

                }
            }
        }



        private void OnGUI()
        {
            // A POV keyframe can remain selected internally after Timeline's
            // window is closed. Only show our floating window while Timeline's
            // own keyframe value UI is actually visible.
            if (
                povDropdown == null ||
                !povDropdown.gameObject.activeInHierarchy
            )
            {
                viewModeMenuOpen = false;
                return;
            }


            Timeline.Keyframe selectedKeyframe =
                GetSelectedPovKeyframe();


            if (selectedKeyframe == null)
            {
                viewModeMenuOpen = false;
                return;
            }


            // Keep the window on-screen, but otherwise preserve exactly
            // where the user dragged it.
            povSettingsWindowRect.x = Mathf.Clamp(
                povSettingsWindowRect.x,
                0f,
                Mathf.Max(0f, Screen.width - povSettingsWindowRect.width)
            );

            povSettingsWindowRect.y = Mathf.Clamp(
                povSettingsWindowRect.y,
                0f,
                Mathf.Max(0f, Screen.height - 25f)
            );


            // Studio polls Unity's mouse axes separately from IMGUI.
            // Clear those axes while the mouse is over our window so
            // dragging/clicking this GUI does not rotate the Studio camera.
            Event currentEvent = Event.current;

            if (currentEvent != null)
            {
                Vector2 guiMouse = currentEvent.mousePosition;

                if (povSettingsWindowRect.Contains(guiMouse))
                {
                    if (
                        currentEvent.type == EventType.MouseDown ||
                        currentEvent.type == EventType.MouseDrag ||
                        Input.GetMouseButton(0) ||
                        Input.GetMouseButton(1) ||
                        Input.GetMouseButton(2)
                    )
                    {
                        Input.ResetInputAxes();
                    }
                }
            }


            // GUI.Window RETURNS the moved window rectangle.
            // Keeping this returned value is what makes GUI.DragWindow's
            // movement persist between OnGUI calls.
            povSettingsWindowRect =
                GUI.Window(
                    317317,
                    povSettingsWindowRect,
                    DrawPovSettingsWindow,
                    "POV Switch Settings"
                );
        }



        private void DrawPovSettingsWindow(
            int windowId
        )
        {
            // Only the title bar is draggable. GUI.Window will return the
            // moved Rect to OnGUI, where we store it in povSettingsWindowRect.
            GUI.DragWindow(
                new Rect(0f, 0f, povSettingsWindowRect.width, 25f)
            );


            Timeline.Keyframe selectedKeyframe =
                GetSelectedPovKeyframe();


            if (selectedKeyframe == null)
                return;


            string storedValue =
                selectedKeyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


            if (parts.Length < 1)
                return;


            string viewMode =
                parts.Length > 1
                    ? parts[1]
                    : "none";


            string viewLabel =
                "None / Default";


            if (viewMode == "slot1")
                viewLabel = "View Slot 1";
            else if (viewMode == "slot2")
                viewLabel = "View Slot 2";
            else if (viewMode == "slot3")
                viewLabel = "View Slot 3";
            else if (viewMode == "custom")
                viewLabel = "Custom";


            // Force POV is stored independently for every keyframe.
            // Old keyframes default to YES so existing behavior is preserved.
            bool forcePov =
                GetForcePov(
                    ((string)selectedKeyframe.value).Split('|')
                );


            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Force POV", GUILayout.Width(145f));


            if (
                GUILayout.Button(
                    forcePov ? "Yes" : "No",
                    GUILayout.Width(70f)
                )
            )
            {
                forcePov = !forcePov;

                SetKeyframeForcePov(
                    selectedKeyframe,
                    forcePov
                );
            }


            GUILayout.EndHorizontal();


            int selectedSceneId;
            bool isDisablePovKeyframe =
                int.TryParse(parts[0], out selectedSceneId) &&
                selectedSceneId == -1;


            if (
                !isDisablePovKeyframe &&
                IsMirrorFunctionAvailable()
            )
            {
                bool spawnMirror =
                    GetSpawnMirror(parts);

                string mirrorMode =
                    GetMirrorMode(parts);

                GUILayout.BeginHorizontal();

                bool newSpawnMirror =
                    GUILayout.Toggle(
                        spawnMirror,
                        "Spawn Mirror",
                        GUILayout.Width(145f)
                    );

                if (newSpawnMirror != spawnMirror)
                {
                    SetKeyframeSpawnMirror(
                        selectedKeyframe,
                        newSpawnMirror
                    );

                    parts =
                        ((string)selectedKeyframe.value).Split('|');

                    spawnMirror =
                        newSpawnMirror;
                }

                GUI.enabled =
                    spawnMirror;

                if (
                    GUILayout.Button(
                        mirrorMode == "static"
                            ? "Static ▼"
                            : "Dynamic ▼",
                        GUILayout.Width(105f)
                    )
                )
                {
                    mirrorModeMenuOpen =
                        !mirrorModeMenuOpen;
                }

                GUI.enabled =
                    true;

                GUILayout.EndHorizontal();


                if (
                    spawnMirror &&
                    mirrorModeMenuOpen
                )
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(145f);

                    GUILayout.BeginVertical(
                        GUILayout.Width(105f)
                    );

                    if (
                        GUILayout.Button(
                            "Dynamic",
                            GUILayout.Width(105f)
                        )
                    )
                    {
                        SetKeyframeMirrorMode(
                            selectedKeyframe,
                            "dynamic"
                        );

                        mirrorMode =
                            "dynamic";

                        mirrorModeMenuOpen =
                            false;

                        parts =
                            ((string)selectedKeyframe.value).Split('|');
                    }

                    if (
                        GUILayout.Button(
                            "Static",
                            GUILayout.Width(105f)
                        )
                    )
                    {
                        SetKeyframeMirrorMode(
                            selectedKeyframe,
                            "static"
                        );

                        mirrorMode =
                            "static";

                        mirrorModeMenuOpen =
                            false;

                        parts =
                            ((string)selectedKeyframe.value).Split('|');
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                else if (!spawnMirror)
                {
                    mirrorModeMenuOpen =
                        false;
                }
            }



            GUILayout.Space(5f);
            GUILayout.Label("View");


            if (GUILayout.Button(viewLabel))
            {
                viewModeMenuOpen =
                    !viewModeMenuOpen;
            }


            if (viewModeMenuOpen)
            {
                if (GUILayout.Button("None / Default"))
                {
                    SetKeyframeViewMode(
                        selectedKeyframe,
                        "none"
                    );

                    viewModeMenuOpen = false;
                    viewMode = "none";
                }


                if (GUILayout.Button("View Slot 1"))
                {
                    SetKeyframeViewMode(
                        selectedKeyframe,
                        "slot1"
                    );

                    viewModeMenuOpen = false;
                    viewMode = "slot1";
                }


                if (GUILayout.Button("View Slot 2"))
                {
                    SetKeyframeViewMode(
                        selectedKeyframe,
                        "slot2"
                    );

                    viewModeMenuOpen = false;
                    viewMode = "slot2";
                }


                if (GUILayout.Button("View Slot 3"))
                {
                    SetKeyframeViewMode(
                        selectedKeyframe,
                        "slot3"
                    );

                    viewModeMenuOpen = false;
                    viewMode = "slot3";
                }


                if (GUILayout.Button("Custom"))
                {
                    EnsureCustomSettings(
                        selectedKeyframe
                    );

                    SetKeyframeViewMode(
                        selectedKeyframe,
                        "custom"
                    );

                    viewModeMenuOpen = false;
                    viewMode = "custom";
                }
            }


            if (viewMode != "custom")
            {
                // Short help panel for None / View Slot modes.
                // Use an explicit wrapped style and height instead of a plain
                // GUILayout.Box(string), which allowed the text to overflow
                // outside the window on some Studio GUI skins/resolutions.
                GUILayout.Space(24f);

                GUIStyle helpBoxStyle =
                    new GUIStyle(GUI.skin.box);

                helpBoxStyle.wordWrap = true;
                helpBoxStyle.alignment = TextAnchor.UpperLeft;
                helpBoxStyle.fontSize = 11;
                helpBoxStyle.padding =
                    new RectOffset(8, 8, 7, 7);

                GUILayout.Box(
                    "Force POV\n" +
                    "Yes: turn PerspectiveX POV on at this keyframe if it is off.\n" +
                    "No: skip this keyframe if PerspectiveX POV is not on.\n\n" +
                    "STOP = restore original PerspectiveX settings.\n" +
                    "New / loaded Studio scene = restore original PerspectiveX settings.\n" +
                    "Crash = restore pending backup on next startup.\n\n" +
                    "Backup file:\n" +
                    "BepInEx/config/TimelinePOVSwitch.Backup.cfg",
                    helpBoxStyle,
                    GUILayout.Height(180f),
                    GUILayout.ExpandWidth(true)
                );
            }


            if (viewMode == "custom")
            {
                EnsureCustomSettings(
                    selectedKeyframe
                );


                parts =
                    ((string)selectedKeyframe.value).Split('|');


                if (parts.Length >= 11)
                {
                    float fov;
                    bool hideHead;
                    bool alignWithBody;
                    float headSway;
                    float pitchLimit;
                    float positionSmoothing;
                    float forwardOffset;
                    float nearClip;
                    float upOffset;


                    if (!TryReadCustomParts(
                        parts,
                        out fov,
                        out hideHead,
                        out alignWithBody,
                        out headSway,
                        out pitchLimit,
                        out positionSmoothing,
                        out forwardOffset,
                        out nearClip,
                        out upOffset
                    ))
                    {
                        return;
                    }


                    // Set the text boxes from this keyframe when the user
                    // selects a different POV keyframe.
                    if (customInputKeyframe != selectedKeyframe)
                    {
                        customInputKeyframe = selectedKeyframe;
                        fovInput = fov.ToString("0.###", CultureInfo.InvariantCulture);
                        headSwayInput = Mathf.RoundToInt(headSway * 100f).ToString();
                        pitchLimitInput = pitchLimit.ToString("0.###", CultureInfo.InvariantCulture);
                        positionSmoothingInput = Mathf.RoundToInt(positionSmoothing * 100f).ToString();
                        forwardOffsetInput = forwardOffset.ToString("0.###", CultureInfo.InvariantCulture);
                        nearClipInput = nearClip.ToString("0.####", CultureInfo.InvariantCulture);
                        upOffsetInput = upOffset.ToString("0.###", CultureInfo.InvariantCulture);
                    }


                    GUILayout.Space(8f);
                    GUILayout.Label("Custom Settings");


                    // Field of View: 20 - 120.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Field of View", GUILayout.Width(145f));

                    float oldFov = fov;
                    fov =
                        GUILayout.HorizontalSlider(
                            fov,
                            20f,
                            120f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(fov - oldFov) > 0.00001f)
                    {
                        fovInput =
                            fov.ToString("0.##", CultureInfo.InvariantCulture);
                    }

                    fovInput =
                        GUILayout.TextField(
                            fovInput,
                            GUILayout.Width(45f)
                        );

                    float typedFov;
                    if (
                        float.TryParse(
                            fovInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedFov
                        ) &&
                        typedFov >= 20f &&
                        typedFov <= 120f
                    )
                    {
                        fov = typedFov;
                    }

                    GUILayout.EndHorizontal();


                    hideHead =
                        GUILayout.Toggle(
                            hideHead,
                            "Hide Character Head"
                        );


                    alignWithBody =
                        GUILayout.Toggle(
                            alignWithBody,
                            "Align Camera With Body"
                        );


                    // Animation Sway: 0 - 100%.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Animation Sway", GUILayout.Width(145f));

                    float oldHeadSway = headSway;
                    headSway =
                        GUILayout.HorizontalSlider(
                            headSway,
                            0f,
                            1f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(headSway - oldHeadSway) > 0.00001f)
                    {
                        headSwayInput =
                            Mathf.RoundToInt(headSway * 100f).ToString();
                    }

                    headSwayInput =
                        GUILayout.TextField(
                            headSwayInput,
                            GUILayout.Width(45f)
                        );

                    float typedHeadSwayPercent;
                    if (
                        float.TryParse(
                            headSwayInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedHeadSwayPercent
                        ) &&
                        typedHeadSwayPercent >= 0f &&
                        typedHeadSwayPercent <= 100f
                    )
                    {
                        headSway = typedHeadSwayPercent / 100f;
                    }

                    GUILayout.EndHorizontal();


                    // Pitch Limit: 30 - 89.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Pitch Limit", GUILayout.Width(145f));

                    float oldPitchLimit = pitchLimit;
                    pitchLimit =
                        GUILayout.HorizontalSlider(
                            pitchLimit,
                            30f,
                            89f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(pitchLimit - oldPitchLimit) > 0.00001f)
                    {
                        pitchLimitInput =
                            pitchLimit.ToString("0.##", CultureInfo.InvariantCulture);
                    }

                    pitchLimitInput =
                        GUILayout.TextField(
                            pitchLimitInput,
                            GUILayout.Width(45f)
                        );

                    float typedPitchLimit;
                    if (
                        float.TryParse(
                            pitchLimitInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedPitchLimit
                        ) &&
                        typedPitchLimit >= 30f &&
                        typedPitchLimit <= 89f
                    )
                    {
                        pitchLimit = typedPitchLimit;
                    }

                    GUILayout.EndHorizontal();


                    // Position Smoothing: 0 - 100%.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Position Smoothing", GUILayout.Width(145f));

                    float oldPositionSmoothing = positionSmoothing;
                    positionSmoothing =
                        GUILayout.HorizontalSlider(
                            positionSmoothing,
                            0f,
                            1f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(positionSmoothing - oldPositionSmoothing) > 0.00001f)
                    {
                        positionSmoothingInput =
                            Mathf.RoundToInt(positionSmoothing * 100f).ToString();
                    }

                    positionSmoothingInput =
                        GUILayout.TextField(
                            positionSmoothingInput,
                            GUILayout.Width(45f)
                        );

                    float typedPositionSmoothingPercent;
                    if (
                        float.TryParse(
                            positionSmoothingInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedPositionSmoothingPercent
                        ) &&
                        typedPositionSmoothingPercent >= 0f &&
                        typedPositionSmoothingPercent <= 100f
                    )
                    {
                        positionSmoothing = typedPositionSmoothingPercent / 100f;
                    }

                    GUILayout.EndHorizontal();


                    // Forward Offset: 0.0 - 0.2.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Forward Offset", GUILayout.Width(145f));

                    float oldForwardOffset = forwardOffset;
                    forwardOffset =
                        GUILayout.HorizontalSlider(
                            forwardOffset,
                            0f,
                            0.2f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(forwardOffset - oldForwardOffset) > 0.00001f)
                    {
                        forwardOffsetInput =
                            forwardOffset.ToString("0.###", CultureInfo.InvariantCulture);
                    }

                    forwardOffsetInput =
                        GUILayout.TextField(
                            forwardOffsetInput,
                            GUILayout.Width(45f)
                        );

                    float typedForwardOffset;
                    if (
                        float.TryParse(
                            forwardOffsetInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedForwardOffset
                        ) &&
                        typedForwardOffset >= 0f &&
                        typedForwardOffset <= 0.2f
                    )
                    {
                        forwardOffset = typedForwardOffset;
                    }

                    GUILayout.EndHorizontal();


                    // Near Clip Plane: 0.01 - 0.1.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Near Clip Plane", GUILayout.Width(145f));

                    float oldNearClip = nearClip;
                    nearClip =
                        GUILayout.HorizontalSlider(
                            nearClip,
                            0.01f,
                            0.1f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(nearClip - oldNearClip) > 0.000001f)
                    {
                        nearClipInput =
                            nearClip.ToString("0.####", CultureInfo.InvariantCulture);
                    }

                    nearClipInput =
                        GUILayout.TextField(
                            nearClipInput,
                            GUILayout.Width(55f)
                        );

                    float typedNearClip;
                    if (
                        float.TryParse(
                            nearClipInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedNearClip
                        ) &&
                        typedNearClip >= 0.01f &&
                        typedNearClip <= 0.1f
                    )
                    {
                        nearClip = typedNearClip;
                    }

                    GUILayout.EndHorizontal();


                    // Up Offset: -0.1 - 0.1.
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Up Offset", GUILayout.Width(145f));

                    float oldUpOffset = upOffset;
                    upOffset =
                        GUILayout.HorizontalSlider(
                            upOffset,
                            -0.1f,
                            0.1f,
                            GUILayout.Width(105f)
                        );

                    if (Mathf.Abs(upOffset - oldUpOffset) > 0.00001f)
                    {
                        upOffsetInput =
                            upOffset.ToString("0.###", CultureInfo.InvariantCulture);
                    }

                    upOffsetInput =
                        GUILayout.TextField(
                            upOffsetInput,
                            GUILayout.Width(45f)
                        );

                    float typedUpOffset;
                    if (
                        float.TryParse(
                            upOffsetInput,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out typedUpOffset
                        ) &&
                        typedUpOffset >= -0.1f &&
                        typedUpOffset <= 0.1f
                    )
                    {
                        upOffset = typedUpOffset;
                    }

                    GUILayout.EndHorizontal();


                    WriteCustomParts(
                        selectedKeyframe,
                        fov,
                        hideHead,
                        alignWithBody,
                        headSway,
                        pitchLimit,
                        positionSmoothing,
                        forwardOffset,
                        nearClip,
                        upOffset
                    );
                }
            }

        }



        // =========================================================
        // SELECTED POV KEYFRAME
        // =========================================================

        private static Timeline.Keyframe GetSelectedPovKeyframe()
        {
            if (_timeline == null)
                return null;


            List<KeyValuePair<float, Timeline.Keyframe>> selected =
                (List<KeyValuePair<float, Timeline.Keyframe>>)
                _timeline.GetPrivate(
                    "_selectedKeyframes"
                );


            if (selected.Count != 1)
                return null;


            Timeline.Keyframe keyframe =
                selected[0].Value;


            if (
                keyframe == null ||
                keyframe.parent == null ||
                keyframe.parent.id != "POVSwitch"
            )
            {
                return null;
            }


            return keyframe;
        }



        private static void SetKeyframeViewMode(
            Timeline.Keyframe keyframe,
            string viewMode
        )
        {
            string storedValue =
                keyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


            if (parts.Length == 1)
            {
                keyframe.value =
                    parts[0] +
                    "|" +
                    viewMode;

                return;
            }


            parts[1] =
                viewMode;


            keyframe.value =
                string.Join("|", parts);
        }



        private static bool GetForcePov(
            string[] parts
        )
        {
            if (parts == null)
                return true;


            foreach (string part in parts)
            {
                if (part == "force0")
                    return false;

                if (part == "force1")
                    return true;
            }


            // Old keyframes had no Force POV value. Keep their old behavior.
            return true;
        }



        private static void SetKeyframeForcePov(
            Timeline.Keyframe keyframe,
            bool forcePov
        )
        {
            string storedValue =
                keyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


            for (int i = 0; i < parts.Length; i++)
            {
                if (
                    parts[i] == "force0" ||
                    parts[i] == "force1"
                )
                {
                    parts[i] =
                        forcePov ? "force1" : "force0";

                    keyframe.value =
                        string.Join("|", parts);

                    return;
                }
            }


            keyframe.value =
                storedValue +
                "|" +
                (forcePov ? "force1" : "force0");
        }



        private static bool GetSpawnMirror(
            string[] parts
        )
        {
            if (parts == null)
                return false;


            foreach (string part in parts)
            {
                if (part == "mirror0")
                    return false;

                if (part == "mirror1")
                    return true;
            }


            // Keyframes created before this feature default to OFF.
            return false;
        }



        private static void SetKeyframeSpawnMirror(
            Timeline.Keyframe keyframe,
            bool spawnMirror
        )
        {
            string storedValue =
                keyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


            for (int i = 0; i < parts.Length; i++)
            {
                if (
                    parts[i] == "mirror0" ||
                    parts[i] == "mirror1"
                )
                {
                    parts[i] =
                        spawnMirror ? "mirror1" : "mirror0";

                    keyframe.value =
                        string.Join("|", parts);

                    return;
                }
            }


            keyframe.value =
                storedValue +
                "|" +
                (spawnMirror ? "mirror1" : "mirror0");
        }




        private static string GetMirrorMode(
            string[] parts
        )
        {
            if (parts != null)
            {
                foreach (string part in parts)
                {
                    if (part == "mirrorstatic")
                        return "static";

                    if (part == "mirrordynamic")
                        return "dynamic";
                }
            }

            // Existing mirror keyframes from older versions behaved dynamically.
            return "dynamic";
        }



        private static void SetKeyframeMirrorMode(
            Timeline.Keyframe keyframe,
            string mirrorMode
        )
        {
            string storedValue =
                keyframe.value as string;

            if (string.IsNullOrEmpty(storedValue))
                return;

            string newTag =
                mirrorMode == "static"
                    ? "mirrorstatic"
                    : "mirrordynamic";

            string[] parts =
                storedValue.Split('|');

            for (int i = 0; i < parts.Length; i++)
            {
                if (
                    parts[i] == "mirrorstatic" ||
                    parts[i] == "mirrordynamic"
                )
                {
                    parts[i] =
                        newTag;

                    keyframe.value =
                        string.Join("|", parts);

                    return;
                }
            }

            keyframe.value =
                storedValue +
                "|" +
                newTag;
        }



        private static void EnsureCustomSettings(
            Timeline.Keyframe keyframe
        )
        {
            string storedValue =
                keyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


            // A full custom payload already exists.
            if (parts.Length >= 11)
                return;


            float fov;
            bool hideHead;
            bool alignWithBody;
            float headSway;
            float pitchLimit;
            float positionSmoothing;
            float forwardOffset;
            float nearClip;
            float upOffset;


            if (!ReadPerspectiveXSettings(
                out fov,
                out hideHead,
                out alignWithBody,
                out headSway,
                out pitchLimit,
                out positionSmoothing,
                out forwardOffset,
                out nearClip,
                out upOffset
            ))
            {
                return;
            }


            string sceneId =
                parts[0];


            string viewMode =
                parts.Length > 1
                    ? parts[1]
                    : "custom";


            bool forcePov =
                GetForcePov(parts);

            bool spawnMirror =
                GetSpawnMirror(parts);


            keyframe.value =
                sceneId + "|" +
                viewMode + "|" +
                FloatText(Mathf.Clamp(fov, 20f, 120f)) + "|" +
                (hideHead ? "1" : "0") + "|" +
                (alignWithBody ? "1" : "0") + "|" +
                FloatText(headSway) + "|" +
                FloatText(pitchLimit) + "|" +
                FloatText(positionSmoothing) + "|" +
                FloatText(forwardOffset) + "|" +
                FloatText(nearClip) + "|" +
                FloatText(upOffset) + "|" +
                (forcePov ? "force1" : "force0") + "|" +
                (spawnMirror ? "mirror1" : "mirror0");
        }



        // =========================================================
        // PERSISTENT PERSPECTIVEX BACKUP / RECOVERY
        // =========================================================

    }
}
