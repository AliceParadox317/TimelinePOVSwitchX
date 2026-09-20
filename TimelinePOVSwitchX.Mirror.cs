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
        // =========================================================
        // PERMANENT MIRROR TOOLBAR BUTTON
        //
        // This creates a normal Studio item at the same camera-relative
        // position/orientation used by Dynamic Timeline mirrors.
        // It is NOT stored in activePovMirror, so Timeline Stop / next
        // keyframe will never delete it.
        // =========================================================

        private static Sprite GetEmbeddedToolbarBackgroundSprite()
        {
            if (embeddedToolbarBackgroundSprite != null)
                return embeddedToolbarBackgroundSprite;

            try
            {
                byte[] pngBytes =
                    System.Convert.FromBase64String(
                        EMBEDDED_TOOLBAR_BACKGROUND_BASE64
                    );

                Texture2D texture =
                    new Texture2D(
                        2,
                        2,
                        TextureFormat.ARGB32,
                        false
                    );

                texture.name =
                    "TimelinePOVSwitchX_EmbeddedToolbarBackground";

                if (!texture.LoadImage(pngBytes))
                {

                    UnityEngine.Object.Destroy(
                        texture
                    );

                    return null;
                }

                texture.filterMode =
                    FilterMode.Bilinear;

                texture.wrapMode =
                    TextureWrapMode.Clamp;

                embeddedToolbarBackgroundSprite =
                    Sprite.Create(
                        texture,
                        new Rect(
                            0f,
                            0f,
                            texture.width,
                            texture.height
                        ),
                        new Vector2(
                            0.5f,
                            0.5f
                        ),
                        100f
                    );

                embeddedToolbarBackgroundSprite.name =
                    "TimelinePOVSwitchX_EmbeddedToolbarBackgroundSprite";


                return embeddedToolbarBackgroundSprite;
            }
            catch
            {
                return null;
            }
        }



        private static void TryAddPermanentMirrorToolbarButtonWhenStable()
        {
            if (permanentMirrorToolbarButton != null)
                return;

            GameObject toolbarObject =
                GameObject.Find(
                    "StudioScene/Canvas System Menu/01_Button"
                );

            if (toolbarObject == null)
            {
                toolbarStableFrames = 0;
                lastToolbarLayoutSignature = "";
                return;
            }

            RectTransform toolbar =
                toolbarObject.transform as RectTransform;

            if (toolbar == null)
            {
                toolbarStableFrames = 0;
                lastToolbarLayoutSignature = "";
                return;
            }

            // Build a signature from the CURRENT direct child buttons.
            // If another plugin adds/moves a toolbar button, this changes and
            // the countdown restarts. Our own button is ignored.
            List<string> parts =
                new List<string>();

            for (
                int i = 0;
                i < toolbar.childCount;
                i++
            )
            {
                Transform child =
                    toolbar.GetChild(i);

                if (child == null)
                    continue;

                if (
                    child.gameObject.name ==
                    "TimelinePOVSwitchX_PermanentMirrorButton"
                )
                {
                    continue;
                }

                Button childButton =
                    child.GetComponent<Button>();

                RectTransform childRect =
                    child as RectTransform;

                if (
                    childButton == null ||
                    childRect == null
                )
                {
                    continue;
                }

                parts.Add(
                    child.gameObject.name +
                    ":" +
                    childRect.anchoredPosition.x.ToString("0.###") +
                    "," +
                    childRect.anchoredPosition.y.ToString("0.###")
                );
            }

            parts.Sort();

            string signature =
                string.Join(
                    "|",
                    parts.ToArray()
                );

            if (
                signature !=
                lastToolbarLayoutSignature
            )
            {
                lastToolbarLayoutSignature =
                    signature;

                toolbarStableFrames =
                    0;

                return;
            }

            toolbarStableFrames++;

            if (
                toolbarStableFrames <
                TOOLBAR_STABLE_FRAMES_REQUIRED
            )
            {
                return;
            }

            TryAddPermanentMirrorToolbarButton();

            // Keep the settled signature. If the button is later removed by
            // the F1 setting, re-enabling starts a fresh stability check.
        }



        private static void TryAddPermanentMirrorToolbarButton()
        {
            if (permanentMirrorToolbarButton != null)
                return;

            GameObject toolbarObject =
                GameObject.Find(
                    "StudioScene/Canvas System Menu/01_Button"
                );

            if (toolbarObject == null)
                return;

            RectTransform toolbar =
                toolbarObject.transform as RectTransform;

            if (toolbar == null)
                return;


            // Use one of Studio's existing buttons as the visual template
            // so our button inherits the same background/transition style.
            Button templateButton =
                null;

            for (
                int i = 0;
                i < toolbar.childCount;
                i++
            )
            {
                Transform child =
                    toolbar.GetChild(i);

                if (child == null)
                    continue;

                Button candidate =
                    child.GetComponent<Button>();

                if (candidate != null)
                {
                    templateButton =
                        candidate;
                    break;
                }
            }

            if (templateButton == null)
                return;


            GameObject buttonObject =
                new GameObject(
                    "TimelinePOVSwitchX_PermanentMirrorButton"
                );

            RectTransform rect =
                buttonObject.AddComponent<RectTransform>();

            rect.SetParent(
                toolbar,
                false
            );


            RectTransform templateRect =
                templateButton.transform as RectTransform;

            if (templateRect != null)
            {
                rect.anchorMin =
                    templateRect.anchorMin;

                rect.anchorMax =
                    templateRect.anchorMax;

                rect.pivot =
                    templateRect.pivot;

                rect.sizeDelta =
                    templateRect.sizeDelta;

                rect.localScale =
                    templateRect.localScale;
            }
            else
            {
                rect.sizeDelta =
                    new Vector2(
                        40f,
                        40f
                    );
            }


            // Align with the existing Studio icon grid.
            //
            // Desired behavior for a 2-column toolbar:
            //
            //   [A] [B]        [A]
            //                  [C] [D]
            //
            // becomes:
            //
            //   [MIR]          [A] [MIR]
            //   [A] [B]       [C] [D]
            //
            // In other words:
            // 1. If the current TOP row has one empty column, fill it.
            // 2. If the current TOP row is already full, make a new row
            //    above it and place MIR in the left column.
            List<Vector2> existingPositions =
                new List<Vector2>();

            List<float> existingX =
                new List<float>();

            List<float> existingY =
                new List<float>();

            for (
                int i = 0;
                i < toolbar.childCount;
                i++
            )
            {
                RectTransform childRect =
                    toolbar.GetChild(i) as RectTransform;

                if (childRect == null)
                    continue;

                Button childButton =
                    childRect.GetComponent<Button>();

                if (childButton == null)
                    continue;

                Vector2 position =
                    childRect.anchoredPosition;

                existingPositions.Add(
                    position
                );

                existingX.Add(
                    position.x
                );

                existingY.Add(
                    position.y
                );
            }

            if (
                existingPositions.Count == 0
            )
            {
                return;
            }


            // Find the actual left and right toolbar columns.
            existingX.Sort();

            float leftColumnX =
                existingX[0];

            float rightColumnX =
                leftColumnX;

            float smallestHorizontalGap =
                float.MaxValue;

            for (
                int i = 1;
                i < existingX.Count;
                i++
            )
            {
                float gap =
                    Mathf.Abs(
                        existingX[i] -
                        existingX[i - 1]
                    );

                if (
                    gap > 4f &&
                    gap < smallestHorizontalGap
                )
                {
                    smallestHorizontalGap =
                        gap;
                }
            }

            if (
                smallestHorizontalGap <
                float.MaxValue
            )
            {
                rightColumnX =
                    leftColumnX +
                    smallestHorizontalGap;
            }
            else
            {
                // Fallback in case only one column currently exists.
                rightColumnX =
                    leftColumnX +
                    (
                        templateRect != null
                            ? Mathf.Abs(
                                templateRect.rect.width
                            )
                            : 40f
                    );
            }


            // Find the current top row.
            float topRowY =
                existingY[0];

            foreach (float y in existingY)
            {
                if (y > topRowY)
                    topRowY = y;
            }


            // Derive vertical row spacing from the real toolbar.
            existingY.Sort();

            float rowSpacing =
                templateRect != null
                    ? Mathf.Abs(
                        templateRect.rect.height
                    )
                    : 40f;

            float smallestUsefulGap =
                float.MaxValue;

            for (
                int i = 1;
                i < existingY.Count;
                i++
            )
            {
                float gap =
                    Mathf.Abs(
                        existingY[i] -
                        existingY[i - 1]
                    );

                if (
                    gap > 4f &&
                    gap < smallestUsefulGap
                )
                {
                    smallestUsefulGap =
                        gap;
                }
            }

            if (
                smallestUsefulGap <
                float.MaxValue
            )
            {
                rowSpacing =
                    smallestUsefulGap;
            }

            if (rowSpacing < 20f)
            {
                rowSpacing =
                    40f;
            }


            // Work out which slots are occupied in the TOP row.
            float rowTolerance =
                Mathf.Max(
                    4f,
                    rowSpacing * 0.25f
                );

            float columnTolerance =
                Mathf.Max(
                    4f,
                    Mathf.Abs(
                        rightColumnX -
                        leftColumnX
                    ) * 0.25f
                );

            bool topLeftOccupied =
                false;

            bool topRightOccupied =
                false;

            foreach (
                Vector2 position
                in existingPositions
            )
            {
                if (
                    Mathf.Abs(
                        position.y -
                        topRowY
                    ) >
                    rowTolerance
                )
                {
                    continue;
                }

                if (
                    Mathf.Abs(
                        position.x -
                        leftColumnX
                    ) <=
                    columnTolerance
                )
                {
                    topLeftOccupied =
                        true;
                }

                if (
                    Mathf.Abs(
                        position.x -
                        rightColumnX
                    ) <=
                    columnTolerance
                )
                {
                    topRightOccupied =
                        true;
                }
            }


            Vector2 anchoredPosition;

            if (
                topLeftOccupied &&
                !topRightOccupied
            )
            {
                // Scenario 2:
                //
                // [A]
                // [B] [C]
                //
                // -> [A] [MIR]
                //    [B] [C]
                anchoredPosition =
                    new Vector2(
                        rightColumnX,
                        topRowY
                    );
            }
            else if (
                !topLeftOccupied &&
                topRightOccupied
            )
            {
                // Unusual reverse case: fill the missing left slot.
                anchoredPosition =
                    new Vector2(
                        leftColumnX,
                        topRowY
                    );
            }
            else
            {
                // Scenario 1, or any already-full top row:
                //
                // [A] [B]
                //
                // -> [MIR]
                //    [A] [B]
                anchoredPosition =
                    new Vector2(
                        leftColumnX,
                        topRowY +
                        rowSpacing
                    );
            }


            rect.anchoredPosition =
                anchoredPosition;



            Image image =
                buttonObject.AddComponent<Image>();

            Sprite embeddedBackground =
                GetEmbeddedToolbarBackgroundSprite();

            if (embeddedBackground != null)
            {
                image.sprite =
                    embeddedBackground;

                image.type =
                    Image.Type.Simple;

                image.preserveAspect =
                    false;

                image.color =
                    Color.white;
            }
            else
            {
                image.sprite =
                    null;

                image.color =
                    new Color(
                        0.18f,
                        0.18f,
                        0.18f,
                        0.85f
                    );
            }


            Button button =
                buttonObject.AddComponent<Button>();

            button.targetGraphic =
                image;

            button.transition =
                templateButton.transition;

            button.colors =
                templateButton.colors;

            button.spriteState =
                templateButton.spriteState;

            button.onClick =
                new Button.ButtonClickedEvent();

            button.onClick.AddListener(
                delegate
                {
                    SpawnPermanentMirrorFromCurrentCamera();
                }
            );


            GameObject textObject =
                new GameObject(
                    "Label"
                );

            RectTransform textRect =
                textObject.AddComponent<RectTransform>();

            textRect.SetParent(
                rect,
                false
            );

            textRect.anchorMin =
                Vector2.zero;

            textRect.anchorMax =
                Vector2.one;

            textRect.offsetMin =
                Vector2.zero;

            textRect.offsetMax =
                Vector2.zero;


            Text label =
                textObject.AddComponent<Text>();

            label.text =
                "MIR\nROR";

            label.alignment =
                TextAnchor.MiddleCenter;

            label.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf"
                );

            label.fontSize =
                11;

            label.fontStyle =
                FontStyle.Bold;

            label.color =
                Color.white;

            label.raycastTarget =
                false;


            permanentMirrorToolbarButton =
                button;

        }



        private static void SpawnPermanentMirrorFromCurrentCamera()
        {
            if (
                Studio.Studio.Instance == null
            )
            {
                return;
            }

            Camera activeCamera =
                Camera.main;

            if (activeCamera == null)
            {
                return;
            }


            int runtimeMirrorItemId =
                ResolveRuntimeMirrorItemId();

            if (runtimeMirrorItemId < 0)
            {
                return;
            }


            HashSet<int> idsBeforeSpawn =
                new HashSet<int>(
                    Studio.Studio.Instance.dicObjectCtrl.Keys
                );

            try
            {
                AddObjectItem.Add(
                    POV_MIRROR_GROUP,
                    POV_MIRROR_CATEGORY,
                    runtimeMirrorItemId
                );
            }
            catch
            {
                return;
            }


            OCIItem spawnedMirror =
                null;

            foreach (
                KeyValuePair<int, ObjectCtrlInfo> pair
                in Studio.Studio.Instance.dicObjectCtrl
            )
            {
                if (idsBeforeSpawn.Contains(pair.Key))
                    continue;

                OCIItem item =
                    pair.Value as OCIItem;

                if (item != null)
                {
                    spawnedMirror =
                        item;
                    break;
                }
            }

            if (
                spawnedMirror == null ||
                spawnedMirror.guideObject == null
            )
            {
                return;
            }


            ApplyMirrorMaterialDefaults(
                spawnedMirror
            );


            Vector3 mirrorPosition;
            Vector3 mirrorEuler;


            // If a Timeline mirror is currently active, copy ITS ACTUAL
            // transform. This guarantees the permanent button creates the
            // mirror in exactly the same final placement you are seeing from
            // Dynamic mode instead of recomputing from a possibly different
            // camera frame.
            if (
                activePovMirror != null &&
                activePovMirror.guideObject != null
            )
            {
                mirrorPosition =
                    activePovMirror.guideObject.changeAmount.pos;

                mirrorEuler =
                    activePovMirror.guideObject.changeAmount.rot;

            }
            else
            {
                Transform cameraTransform =
                    activeCamera.transform;

                mirrorPosition =
                    cameraTransform.position +
                    cameraTransform.forward *
                    POV_MIRROR_DISTANCE;

                Quaternion mirrorRotation =
                    Quaternion.LookRotation(
                        cameraTransform.position -
                        mirrorPosition,
                        cameraTransform.up
                    );

                // Same verified orientation correction used by Dynamic mode.
                mirrorRotation =
                    mirrorRotation *
                    Quaternion.Euler(
                        90f,
                        180f,
                        0f
                    );

                mirrorEuler =
                    mirrorRotation.eulerAngles;

            }


            // Permanent mirror: use the current calculated/copied rotation,
            // then apply only an additional +180 degrees on world Y.
            mirrorEuler.y +=
                180f;

            spawnedMirror.guideObject.changeAmount.pos =
                mirrorPosition;

            spawnedMirror.guideObject.changeAmount.rot =
                mirrorEuler;


            // Give the normal workspace item an obvious name.
            try
            {
                if (spawnedMirror.treeNodeObject != null)
                {
                    spawnedMirror.treeNodeObject.textName =
                        "POV Mirror";
                }
            }
            catch
            {
            }


        }



        // =========================================================
        // MIRROR MATERIAL DEFAULTS
        // =========================================================

        private static void ApplyMirrorMaterialDefaults(
            OCIItem mirror
        )
        {
            if (
                mirror == null ||
                mirror.objectItem == null
            )
            {
                return;
            }

            Renderer[] renderers =
                mirror.objectItem.GetComponentsInChildren<Renderer>(
                    true
                );

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Material[] materials =
                    renderer.materials;

                foreach (Material material in materials)
                {
                    if (material == null)
                        continue;

                    // MaterialEditor stores property names without the leading
                    // underscore. Its API applies the Unity property AND records
                    // the override in MaterialEditor's SceneController so it is
                    // serialized with the Studio scene.
                    if (material.HasProperty("_AlbedoDetailScale"))
                    {
                        // Apply immediately so the spawned mirror always has
                        // the requested visual value, independent of persistence.
                        material.SetFloat("_AlbedoDetailScale", 0f);
                        SetPersistentMaterialEditorFloat(
                            mirror,
                            renderer,
                            material,
                            "_AlbedoDetailScale",
                            0f
                        );
                    }

                    if (material.HasProperty("_ReflectionBlurSigma"))
                    {
                        material.SetFloat("_ReflectionBlurSigma", 0.01f);
                        SetPersistentMaterialEditorFloat(
                            mirror,
                            renderer,
                            material,
                            "_ReflectionBlurSigma",
                            0.01f
                        );
                    }

                    if (material.HasProperty("_ReflectionDistortion"))
                    {
                        material.SetFloat("_ReflectionDistortion", 0f);
                        SetPersistentMaterialEditorFloat(
                            mirror,
                            renderer,
                            material,
                            "_ReflectionDistortion",
                            0f
                        );
                    }
                }
            }
        }


        private static bool SetPersistentMaterialEditorFloat(
            OCIItem mirror,
            Renderer renderer,
            Material material,
            string propertyName,
            float value
        )
        {
            if (
                mirror == null ||
                mirror.objectItem == null ||
                renderer == null ||
                material == null ||
                string.IsNullOrEmpty(propertyName)
            )
            {
                return false;
            }

            try
            {
                BepInEx.PluginInfo materialEditorPlugin;
                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "com.deathweasel.bepinex.materialeditor",
                        out materialEditorPlugin
                    ) ||
                    materialEditorPlugin == null ||
                    materialEditorPlugin.Instance == null
                )
                {
                    return false;
                }

                System.Reflection.Assembly assembly =
                    materialEditorPlugin.Instance.GetType().Assembly;

                // Use MaterialEditor's public MaterialAPI when present.
                // Different KK MaterialEditor builds expose it either as a
                // nested type or as a dotted type, so locate it by name.
                System.Type[] types = assembly.GetTypes();
                foreach (System.Type type in types)
                {
                    if (
                        type == null ||
                        type.FullName == null ||
                        type.FullName.IndexOf("MaterialEditorAPI") < 0 ||
                        type.Name != "MaterialAPI"
                    )
                    {
                        continue;
                    }

                    System.Reflection.MethodInfo[] methods =
                        type.GetMethods(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Static
                        );

                    foreach (System.Reflection.MethodInfo method in methods)
                    {
                        if (method.Name != "SetMaterialFloatProperty")
                            continue;

                        System.Reflection.ParameterInfo[] parameters =
                            method.GetParameters();
                        object[] arguments = new object[parameters.Length];
                        bool compatible = true;

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            System.Type pt = parameters[i].ParameterType;
                            string pn = parameters[i].Name == null
                                ? ""
                                : parameters[i].Name.ToLowerInvariant();

                            if (pt == typeof(GameObject))
                                arguments[i] = mirror.objectItem;
                            else if (pt == typeof(Material))
                                arguments[i] = material;
                            else if (pt == typeof(Renderer))
                                arguments[i] = renderer;
                            else if (pt == typeof(float))
                                arguments[i] = value;
                            else if (pt == typeof(int))
                                arguments[i] = mirror.objectInfo.dicKey;
                            else if (pt == typeof(bool))
                                arguments[i] = true;
                            else if (pt == typeof(string))
                            {
                                if (pn.IndexOf("property") >= 0)
                                    arguments[i] = propertyName;
                                else if (pn.IndexOf("material") >= 0)
                                    arguments[i] = material.name;
                                else if (pn.IndexOf("renderer") >= 0)
                                    arguments[i] = renderer.name;
                                else if (pn.IndexOf("gameobject") >= 0 || pn.IndexOf("object") >= 0)
                                    arguments[i] = mirror.objectItem.name;
                                else
                                {
                                    compatible = false;
                                    break;
                                }
                            }
                            else if (parameters[i].IsOptional)
                                arguments[i] = parameters[i].DefaultValue;
                            else
                            {
                                compatible = false;
                                break;
                            }
                        }

                        if (!compatible)
                            continue;

                        method.Invoke(null, arguments);
                        return true;
                    }
                }
            }
            catch
            {
            }

            // The direct Material.SetFloat was already performed by the caller,
            // so failure here affects persistence only, never the visible mirror.
            return false;
        }


        // =========================================================
        // TEMPORARY POV MIRROR
        // =========================================================

        private static Transform FindCharacterBone(
            OCIChar character,
            string suffix
        )
        {
            if (
                character == null ||
                character.charInfo == null
            )
            {
                return null;
            }


            Transform[] transforms =
                character.charInfo.GetComponentsInChildren<Transform>(
                    true
                );


            foreach (Transform t in transforms)
            {
                if (
                    t != null &&
                    t.name.EndsWith(
                        suffix,
                        System.StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return t;
                }
            }


            return null;
        }



        private static void LogLikelyHeadAndEyeBones(
            OCIChar character
        )
        {
            if (
                character == null ||
                character.guideObject == null ||
                character.guideObject.transformTarget == null
            )
            {
                return;
            }

            Transform[] transforms =
                character.guideObject.transformTarget.GetComponentsInChildren<Transform>(
                    true
                );

            int logged = 0;

            foreach (Transform t in transforms)
            {
                if (t == null)
                    continue;

                string boneName =
                    t.name ?? "";

                if (
                    boneName.IndexOf(
                        "eye",
                        System.StringComparison.OrdinalIgnoreCase
                    ) >= 0 ||
                    boneName.IndexOf(
                        "head",
                        System.StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {

                    logged++;

                    if (logged >= 80)
                    {
                        break;
                    }
                }
            }

        }



        private static int ResolveRuntimeMirrorItemId()
        {
            try
            {
                Dictionary<int, Dictionary<int, Info.ItemLoadInfo>> groupDictionary;

                if (
                    !Studio.Info.Instance.dicItemLoadInfo.TryGetValue(
                        POV_MIRROR_GROUP,
                        out groupDictionary
                    ) ||
                    groupDictionary == null
                )
                {
                    return -1;
                }

                Dictionary<int, Info.ItemLoadInfo> categoryDictionary;

                if (
                    !groupDictionary.TryGetValue(
                        POV_MIRROR_CATEGORY,
                        out categoryDictionary
                    ) ||
                    categoryDictionary == null
                )
                {
                    return -1;
                }


                foreach (
                    KeyValuePair<int, Info.ItemLoadInfo> pair
                    in categoryDictionary
                )
                {
                    object itemInfo = pair.Value;

                    if (itemInfo == null)
                        continue;

                    bool exactMatch = false;
                    List<string> stringValues =
                        new List<string>();

                    System.Type itemType =
                        itemInfo.GetType();

                    System.Reflection.FieldInfo[] fields =
                        itemType.GetFields(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance
                        );

                    foreach (
                        System.Reflection.FieldInfo field
                        in fields
                    )
                    {
                        if (field.FieldType != typeof(string))
                            continue;

                        string value = null;

                        try
                        {
                            value =
                                field.GetValue(itemInfo) as string;
                        }
                        catch
                        {
                        }

                        if (string.IsNullOrEmpty(value))
                            continue;

                        stringValues.Add(
                            field.Name + "=" + value
                        );

                        if (
                            string.Equals(
                                value,
                                "Planar Reflection Unlit",
                                System.StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                value,
                                "planar_reflection_unlit",
                                System.StringComparison.OrdinalIgnoreCase
                            ) ||
                            value.IndexOf(
                                "studio/az/planar_reflection.unity3d",
                                System.StringComparison.OrdinalIgnoreCase
                            ) >= 0
                        )
                        {
                            exactMatch = true;
                        }
                    }

                    System.Reflection.PropertyInfo[] properties =
                        itemType.GetProperties(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance
                        );

                    foreach (
                        System.Reflection.PropertyInfo property
                        in properties
                    )
                    {
                        if (
                            property.PropertyType != typeof(string) ||
                            property.GetIndexParameters().Length != 0
                        )
                        {
                            continue;
                        }

                        string value = null;

                        try
                        {
                            value =
                                property.GetValue(
                                    itemInfo,
                                    null
                                ) as string;
                        }
                        catch
                        {
                        }

                        if (string.IsNullOrEmpty(value))
                            continue;

                        stringValues.Add(
                            property.Name + "=" + value
                        );

                        if (
                            string.Equals(
                                value,
                                "Planar Reflection Unlit",
                                System.StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                value,
                                "planar_reflection_unlit",
                                System.StringComparison.OrdinalIgnoreCase
                            ) ||
                            value.IndexOf(
                                "studio/az/planar_reflection.unity3d",
                                System.StringComparison.OrdinalIgnoreCase
                            ) >= 0
                        )
                        {
                            exactMatch = true;
                        }
                    }


                    if (exactMatch)
                    {

                        return pair.Key;
                    }
                }

            }
            catch
            {
            }

            return -1;
        }



        private static void SpawnPovMirrorForCharacter(
            OCIChar character,
            bool dynamicMirror
        )
        {

            if (character == null)
            {
                return;
            }

            if (Studio.Studio.Instance == null)
            {
                return;
            }

            string characterName =
                "<unknown>";

            try
            {
                if (
                    character.treeNodeObject != null &&
                    !string.IsNullOrEmpty(
                        character.treeNodeObject.textName
                    )
                )
                {
                    characterName =
                        character.treeNodeObject.textName;
                }
            }
            catch
            {
            }



            int runtimeMirrorItemId =
                ResolveRuntimeMirrorItemId();

            if (runtimeMirrorItemId < 0)
            {
                return;
            }

            HashSet<int> idsBeforeSpawn =
                new HashSet<int>(
                    Studio.Studio.Instance.dicObjectCtrl.Keys
                );


            try
            {
                AddObjectItem.Add(
                    POV_MIRROR_GROUP,
                    POV_MIRROR_CATEGORY,
                    runtimeMirrorItemId
                );

            }
            catch
            {
                return;
            }


            OCIItem spawnedMirror =
                null;

            int newObjectCount = 0;

            foreach (
                KeyValuePair<int, ObjectCtrlInfo> pair
                in Studio.Studio.Instance.dicObjectCtrl
            )
            {
                if (idsBeforeSpawn.Contains(pair.Key))
                    continue;

                newObjectCount++;

                string objectType =
                    pair.Value != null
                        ? pair.Value.GetType().FullName
                        : "<null>";

                string workspaceName =
                    "<no tree name>";

                try
                {
                    if (
                        pair.Value != null &&
                        pair.Value.treeNodeObject != null
                    )
                    {
                        workspaceName =
                            pair.Value.treeNodeObject.textName;
                    }
                }
                catch
                {
                }


                OCIItem item =
                    pair.Value as OCIItem;

                if (
                    item != null &&
                    spawnedMirror == null
                )
                {
                    spawnedMirror =
                        item;
                }
            }


            if (spawnedMirror == null)
            {
                return;
            }


            // Give our object an unmistakable workspace name immediately.
            // Even if bone tracking fails, KEEP it in the scene for diagnosis.
            try
            {
                if (spawnedMirror.treeNodeObject != null)
                {
                    spawnedMirror.treeNodeObject.textName =
                        "[Timeline POV DEBUG Mirror]";

                }
            }
            catch
            {
            }

            ApplyMirrorMaterialDefaults(
                spawnedMirror
            );

            // Do not track the character's head/eye rotation here.
            // PerspectiveX deliberately decouples POV rotation from those bones.
            // Track the ACTUAL rendered POV camera instead.
            activePovMirror =
                spawnedMirror;

            activePovMirrorDynamic =
                dynamicMirror;

            // Dynamic follows forever.
            // Static follows only for a few rendered frames, then freezes.
            activePovMirrorSettleFrames =
                dynamicMirror
                    ? 0
                    : 3;

            mirrorTransformDebugLogged = false;

            Camera activeCamera =
                Camera.main;

            if (activeCamera == null)
            {

                activePovMirror = null;
                return;
            }



            UpdateActivePovMirror();

        }



        private static void UpdateActivePovMirror()
        {
            if (
                activePovMirror == null ||
                activePovMirror.guideObject == null
            )
            {
                return;
            }

            Camera activeCamera =
                Camera.main;

            if (activeCamera == null)
                return;

            Transform cameraTransform =
                activeCamera.transform;

            if (cameraTransform == null)
                return;


            // Put the mirror directly in front of the ACTUAL PerspectiveX view.
            //
            // This is different from the old version:
            // OLD: character eye midpoint + character head.forward
            // NEW: rendered POV camera position + rendered POV camera.forward
            //
            // PerspectiveX intentionally lets the camera rotate independently
            // from the head, so the camera transform is the correct reference
            // for a mirror that should stay squarely in front of the POV.
            Vector3 mirrorPosition =
                cameraTransform.position +
                cameraTransform.forward *
                POV_MIRROR_DISTANCE;


            // Make the mirror's forward axis point back toward the camera.
            // That keeps the mirror plane perpendicular to the current POV.
            Quaternion mirrorRotation =
                Quaternion.LookRotation(
                    cameraTransform.position -
                    mirrorPosition,
                    cameraTransform.up
                );

            // This mirror asset's reflective face is rotated 90 degrees
            // relative to the transform forward axis. Correct that fixed
            // local-axis offset so the mirror stands vertically in front
            // of the POV instead of facing upward.
            mirrorRotation =
                mirrorRotation *
                Quaternion.Euler(
                    90f,
                    180f,
                    0f
                );


            activePovMirror.guideObject.changeAmount.pos =
                mirrorPosition;

            activePovMirror.guideObject.changeAmount.rot =
                mirrorRotation.eulerAngles;


            if (!mirrorTransformDebugLogged)
            {
                mirrorTransformDebugLogged = true;

            }
        }



        private static void DeleteActivePovMirror()
        {
            if (activePovMirror != null)
            {
                try
                {
                    if (
                        activePovMirror.treeNodeObject != null &&
                        Studio.Studio.Instance != null &&
                        Studio.Studio.Instance.treeNodeCtrl != null
                    )
                    {
                        Studio.Studio.Instance.treeNodeCtrl.DeleteNode(
                            activePovMirror.treeNodeObject
                        );
                    }
                }
                catch
                {
                }
            }


            ClearActiveMirrorReferences();
        }



        private static void ClearActiveMirrorReferences()
        {
            activePovMirror = null;
        }



        // =========================================================
        // PERSPECTIVEX POV SWITCH
        // =========================================================

    }
}
