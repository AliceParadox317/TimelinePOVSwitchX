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
        private static bool TimelineContainsPerspectiveXSettingChanges()
        {
            try
            {
                if (_timeline == null)
                {
                    _timeline =
                        Singleton<Timeline.Timeline>.Instance;

                    if (_timeline == null)
                        return false;
                }


                Dictionary<int, Interpolable> interpolables =
                    (Dictionary<int, Interpolable>)
                    _timeline.GetPrivate(
                        "_interpolables"
                    );


                if (interpolables == null)
                    return false;


                foreach (Interpolable interpolable in interpolables.Values)
                {
                    if (interpolable.id != "POVSwitch")
                        continue;


                    foreach (
                        KeyValuePair<float, Timeline.Keyframe> pair
                        in interpolable.keyframes
                    )
                    {
                        string storedValue =
                            pair.Value.value as string;


                        if (string.IsNullOrEmpty(storedValue))
                            continue;


                        string[] parts =
                            storedValue.Split('|');


                        string viewMode =
                            parts.Length > 1
                                ? parts[1]
                                : "none";


                        if (
                            viewMode == "slot1" ||
                            viewMode == "slot2" ||
                            viewMode == "slot3" ||
                            viewMode == "custom"
                        )
                        {
                            return true;
                        }
                    }


                    break;
                }
            }
            catch
            {
            }


            return false;
        }



        private static bool CreatePerspectiveXBackup()
        {
            try
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


                if (
                    !ReadPerspectiveXSettings(
                        out fov,
                        out hideHead,
                        out alignWithBody,
                        out headSway,
                        out pitchLimit,
                        out positionSmoothing,
                        out forwardOffset,
                        out nearClip,
                        out upOffset
                    )
                )
                {
                    return false;
                }


                string backupText =
                    "[Recovery]\n" +
                    "Pending=true\n" +
                    "DefaultFov=" + FloatText(fov) + "\n" +
                    "HideHead=" + (hideHead ? "true" : "false") + "\n" +
                    "AlignWithBody=" + (alignWithBody ? "true" : "false") + "\n" +
                    "HeadSway=" + FloatText(headSway) + "\n" +
                    "PitchLimit=" + FloatText(pitchLimit) + "\n" +
                    "PositionSmoothing=" + FloatText(positionSmoothing) + "\n" +
                    "ForwardOffset=" + FloatText(forwardOffset) + "\n" +
                    "NearClip=" + FloatText(nearClip) + "\n" +
                    "UpOffset=" + FloatText(upOffset) + "\n";


                System.IO.File.WriteAllText(
                    perspectiveXBackupPath,
                    backupText
                );


                return true;
            }
            catch (System.Exception)
            {

                return false;
            }
        }



        private static void RestorePerspectiveXBackupIfPending()
        {
            try
            {
                if (!System.IO.File.Exists(perspectiveXBackupPath))
                    return;


                string[] lines =
                    System.IO.File.ReadAllLines(
                        perspectiveXBackupPath
                    );


                Dictionary<string, string> values =
                    new Dictionary<string, string>();


                foreach (string rawLine in lines)
                {
                    if (rawLine == null)
                        continue;


                    string line =
                        rawLine.Trim();


                    if (
                        line.Length == 0 ||
                        line.StartsWith("[") ||
                        line.StartsWith("#") ||
                        line.StartsWith(";")
                    )
                    {
                        continue;
                    }


                    int equalsIndex =
                        line.IndexOf('=');


                    if (equalsIndex <= 0)
                        continue;


                    string key =
                        line.Substring(0, equalsIndex).Trim();

                    string value =
                        line.Substring(equalsIndex + 1).Trim();


                    values[key] = value;
                }


                string pendingText;


                if (
                    !values.TryGetValue(
                        "Pending",
                        out pendingText
                    ) ||
                    !string.Equals(
                        pendingText,
                        "true",
                        System.StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return;
                }


                BepInEx.PluginInfo pluginInfo;


                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return;
                }


                object plugin =
                    pluginInfo.Instance;

                System.Type pluginType =
                    plugin.GetType();


                float fov;
                bool hideHead;
                bool alignWithBody;
                float headSway;
                float pitchLimit;
                float positionSmoothing;
                float forwardOffset;
                float nearClip;
                float upOffset;


                if (
                    !TryGetBackupFloat(values, "DefaultFov", out fov) ||
                    !TryGetBackupBool(values, "HideHead", out hideHead) ||
                    !TryGetBackupBool(values, "AlignWithBody", out alignWithBody) ||
                    !TryGetBackupFloat(values, "HeadSway", out headSway) ||
                    !TryGetBackupFloat(values, "PitchLimit", out pitchLimit) ||
                    !TryGetBackupFloat(values, "PositionSmoothing", out positionSmoothing) ||
                    !TryGetBackupFloat(values, "ForwardOffset", out forwardOffset) ||
                    !TryGetBackupFloat(values, "NearClip", out nearClip) ||
                    !TryGetBackupFloat(values, "UpOffset", out upOffset)
                )
                {

                    return;
                }


                SetPerspectiveXSetting(plugin, pluginType, "DefaultFov", fov);
                SetPerspectiveXSetting(plugin, pluginType, "HideHead", hideHead);
                SetPerspectiveXSetting(plugin, pluginType, "AlignWithBody", alignWithBody);
                SetPerspectiveXSetting(plugin, pluginType, "HeadSway", headSway);
                SetPerspectiveXSetting(plugin, pluginType, "PitchLimit", pitchLimit);
                SetPerspectiveXSetting(plugin, pluginType, "PositionSmoothing", positionSmoothing);
                SetPerspectiveXSetting(plugin, pluginType, "ForwardOffset", forwardOffset);
                SetPerspectiveXSetting(plugin, pluginType, "NearClip", nearClip);
                SetPerspectiveXSetting(plugin, pluginType, "UpOffset", upOffset);


                // Deliberately DO NOT touch PerspectiveX's povEnabled state.


                string restoredText =
                    "[Recovery]\n" +
                    "Pending=false\n" +
                    "DefaultFov=" + FloatText(fov) + "\n" +
                    "HideHead=" + (hideHead ? "true" : "false") + "\n" +
                    "AlignWithBody=" + (alignWithBody ? "true" : "false") + "\n" +
                    "HeadSway=" + FloatText(headSway) + "\n" +
                    "PitchLimit=" + FloatText(pitchLimit) + "\n" +
                    "PositionSmoothing=" + FloatText(positionSmoothing) + "\n" +
                    "ForwardOffset=" + FloatText(forwardOffset) + "\n" +
                    "NearClip=" + FloatText(nearClip) + "\n" +
                    "UpOffset=" + FloatText(upOffset) + "\n";


                System.IO.File.WriteAllText(
                    perspectiveXBackupPath,
                    restoredText
                );
            }
            catch (System.Exception)
            {
            }
        }



        private static bool TryGetBackupFloat(
            Dictionary<string, string> values,
            string key,
            out float result
        )
        {
            result = 0f;

            string text;


            if (!values.TryGetValue(key, out text))
                return false;


            return float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result
            );
        }



        private static bool TryGetBackupBool(
            Dictionary<string, string> values,
            string key,
            out bool result
        )
        {
            result = false;

            string text;


            if (!values.TryGetValue(key, out text))
                return false;


            return bool.TryParse(
                text,
                out result
            );
        }



        private static bool ReadPerspectiveXSettings(
            out float fov,
            out bool hideHead,
            out bool alignWithBody,
            out float headSway,
            out float pitchLimit,
            out float positionSmoothing,
            out float forwardOffset,
            out float nearClip,
            out float upOffset
        )
        {
            fov = 60f;
            hideHead = false;
            alignWithBody = true;
            headSway = 1f;
            pitchLimit = 89f;
            positionSmoothing = 0f;
            forwardOffset = 0f;
            nearClip = 0.01f;
            upOffset = 0f;


            BepInEx.PluginInfo pluginInfo;


            if (
                !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                    "bucky.kk.perspectivex",
                    out pluginInfo
                )
            )
            {
                return false;
            }


            object plugin =
                pluginInfo.Instance;


            System.Type pluginType =
                plugin.GetType();


            object value;


            if (!GetPerspectiveXSetting(plugin, pluginType, "DefaultFov", out value))
                return false;
            fov = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "HideHead", out value))
                return false;
            hideHead = (bool)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "AlignWithBody", out value))
                return false;
            alignWithBody = (bool)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "HeadSway", out value))
                return false;
            headSway = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "PitchLimit", out value))
                return false;
            pitchLimit = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "PositionSmoothing", out value))
                return false;
            positionSmoothing = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "ForwardOffset", out value))
                return false;
            forwardOffset = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "NearClip", out value))
                return false;
            nearClip = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "UpOffset", out value))
                return false;
            upOffset = (float)value;


            return true;
        }



        private static bool GetPerspectiveXSetting(
            object plugin,
            System.Type pluginType,
            string settingName,
            out object value
        )
        {
            value = null;


            System.Reflection.PropertyInfo property =
                pluginType.GetProperty(
                    settingName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                );


            if (property == null)
                return false;


            object configEntry =
                property.GetValue(
                    plugin,
                    null
                );


            if (configEntry == null)
                return false;


            System.Reflection.PropertyInfo valueProperty =
                configEntry.GetType().GetProperty(
                    "Value"
                );


            if (valueProperty == null)
                return false;


            value =
                valueProperty.GetValue(
                    configEntry,
                    null
                );


            return value != null;
        }



        private static void SetPerspectiveXSetting(
            object plugin,
            System.Type pluginType,
            string settingName,
            object value
        )
        {
            System.Reflection.PropertyInfo property =
                pluginType.GetProperty(
                    settingName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                );


            if (property == null)
                return;


            object configEntry =
                property.GetValue(
                    plugin,
                    null
                );


            if (configEntry == null)
                return;


            System.Reflection.PropertyInfo valueProperty =
                configEntry.GetType().GetProperty(
                    "Value"
                );


            if (valueProperty == null)
                return;


            valueProperty.SetValue(
                configEntry,
                value,
                null
            );
        }



        private static bool TryReadCustomParts(
            string[] parts,
            out float fov,
            out bool hideHead,
            out bool alignWithBody,
            out float headSway,
            out float pitchLimit,
            out float positionSmoothing,
            out float forwardOffset,
            out float nearClip,
            out float upOffset
        )
        {
            fov = 0f;
            hideHead = false;
            alignWithBody = false;
            headSway = 0f;
            pitchLimit = 30f;
            positionSmoothing = 0f;
            forwardOffset = 0f;
            nearClip = 0.01f;
            upOffset = 0f;


            if (parts.Length < 11)
                return false;


            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out fov))
                return false;

            hideHead =
                parts[3] == "1";

            alignWithBody =
                parts[4] == "1";

            if (!float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out headSway))
                return false;

            if (!float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out pitchLimit))
                return false;

            if (!float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out positionSmoothing))
                return false;

            if (!float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out forwardOffset))
                return false;

            if (!float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out nearClip))
                return false;

            if (!float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out upOffset))
                return false;


            return true;
        }



        private static void WriteCustomParts(
            Timeline.Keyframe keyframe,
            float fov,
            bool hideHead,
            bool alignWithBody,
            float headSway,
            float pitchLimit,
            float positionSmoothing,
            float forwardOffset,
            float nearClip,
            float upOffset
        )
        {
            string storedValue =
                keyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


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
                FloatText(Mathf.Clamp01(headSway)) + "|" +
                FloatText(Mathf.Clamp(pitchLimit, 30f, 89f)) + "|" +
                FloatText(Mathf.Clamp01(positionSmoothing)) + "|" +
                FloatText(Mathf.Clamp(forwardOffset, 0f, 0.2f)) + "|" +
                FloatText(Mathf.Clamp(nearClip, 0.01f, 0.1f)) + "|" +
                FloatText(Mathf.Clamp(upOffset, -0.1f, 0.1f)) + "|" +
                (forcePov ? "force1" : "force0") + "|" +
                (spawnMirror ? "mirror1" : "mirror0");
        }



        private static string FloatText(
            float value
        )
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture
            );
        }



        // =========================================================
        // TIMELINE VALUE SAVE / LOAD
        //
        // Timeline calls these methods when it serializes keyframes into
        // the scene's Timeline data. The entire POV payload is one string:
        //
        // sceneId|viewMode|...custom settings...|force1
        //
        // Keeping it in one XML attribute means character selection,
        // View mode, Force POV and all Custom values survive scene saves.
        // Old values remain compatible because the payload format itself
        // has not changed.
        // =========================================================


        private static void SwitchPOV(
            int sceneId,
            string viewMode,
            string[] storedParts
        )
        {
            try
            {
                // The previous keyframe's mirror lives only until the next
                // POV keyframe is reached.
                DeleteActivePovMirror();


                // -1 is the special "Disable POV" Timeline action.
                // It exits PerspectiveX POV immediately and intentionally
                // ignores Force POV, View Slot and Custom settings.
                if (sceneId == -1)
                {
                    BepInEx.PluginInfo disablePluginInfo;

                    if (
                        !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                            "bucky.kk.perspectivex",
                            out disablePluginInfo
                        )
                    )
                    {
                        return;
                    }


                    object disablePlugin =
                        disablePluginInfo.Instance;

                    System.Reflection.MethodInfo disablePov =
                        disablePlugin.GetType().GetMethod(
                            "DisablePov",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance
                        );


                    if (disablePov != null)
                    {
                        disablePov.Invoke(
                            disablePlugin,
                            null
                        );
                    }


                    return;
                }


                ObjectCtrlInfo obj;


                // Find character from stored scene ID.
                if (
                    !Studio.Studio.Instance.dicObjectCtrl.TryGetValue(
                        sceneId,
                        out obj
                    )
                )
                {
                    return;
                }


                OCIChar target =
                    obj as OCIChar;


                if (target == null)
                    return;



                // Find PerspectiveX.
                BepInEx.PluginInfo pluginInfo;


                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return;
                }


                object plugin =
                    pluginInfo.Instance;

                System.Type pluginType =
                    plugin.GetType();


                // Find PerspectiveX's povEnabled field first.
                System.Reflection.FieldInfo povEnabledField =
                    pluginType.GetField(
                        "povEnabled",

                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance
                    );


                if (povEnabledField == null)
                    return;


                bool povEnabled =
                    (bool)povEnabledField.GetValue(
                        plugin
                    );


                bool forcePov =
                    GetForcePov(storedParts);


                // Force POV OFF means this event is allowed to control an
                // already-active POV, but it must not activate POV by itself.
                if (
                    !povEnabled &&
                    !forcePov
                )
                {
                    return;
                }


                // Apply this keyframe's Custom settings before enabling/switching.
                if (
                    viewMode == "custom" &&
                    storedParts != null &&
                    storedParts.Length >= 11
                )
                {
                    float customFov;
                    bool customHideHead;
                    bool customAlignWithBody;
                    float customHeadSway;
                    float customPitchLimit;
                    float customPositionSmoothing;
                    float customForwardOffset;
                    float customNearClip;
                    float customUpOffset;


                    if (TryReadCustomParts(
                        storedParts,
                        out customFov,
                        out customHideHead,
                        out customAlignWithBody,
                        out customHeadSway,
                        out customPitchLimit,
                        out customPositionSmoothing,
                        out customForwardOffset,
                        out customNearClip,
                        out customUpOffset
                    ))
                    {
                        SetPerspectiveXSetting(plugin, pluginType, "DefaultFov", Mathf.Clamp(customFov, 20f, 120f));
                        SetPerspectiveXSetting(plugin, pluginType, "HideHead", customHideHead);
                        SetPerspectiveXSetting(plugin, pluginType, "AlignWithBody", customAlignWithBody);
                        SetPerspectiveXSetting(plugin, pluginType, "HeadSway", customHeadSway);
                        SetPerspectiveXSetting(plugin, pluginType, "PitchLimit", customPitchLimit);
                        SetPerspectiveXSetting(plugin, pluginType, "PositionSmoothing", customPositionSmoothing);
                        SetPerspectiveXSetting(plugin, pluginType, "ForwardOffset", customForwardOffset);
                        SetPerspectiveXSetting(plugin, pluginType, "NearClip", customNearClip);
                        SetPerspectiveXSetting(plugin, pluginType, "UpOffset", customUpOffset);
                    }
                }


                // Only Force POV is allowed to activate PerspectiveX.
                if (
                    !povEnabled &&
                    forcePov
                )
                {
                    System.Reflection.MethodInfo enablePov =
                        pluginType.GetMethod(
                            "EnablePov",

                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance
                        );


                    if (enablePov == null)
                        return;


                    enablePov.Invoke(
                        plugin,
                        null
                    );
                }


                // Find PerspectiveX's private SwitchTo method.
                System.Reflection.MethodInfo switchTo =
                    plugin.GetType().GetMethod(
                        "SwitchTo",

                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance
                    );


                if (switchTo == null)
                    return;



                switchTo.Invoke(
                    plugin,
                    new object[]
                    {
                        target.charInfo
                    }
                );


                // Apply a PerspectiveX saved view only when
                // this keyframe explicitly asks for one.
                // None / Default and Custom do nothing here.
                int viewSlot =
                    0;


                if (viewMode == "slot1")
                    viewSlot = 1;
                else if (viewMode == "slot2")
                    viewSlot = 2;
                else if (viewMode == "slot3")
                    viewSlot = 3;


                if (viewSlot != 0)
                {
                    System.Reflection.MethodInfo loadViewSlot =
                        pluginType.GetMethod(
                            "LoadViewSlot",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance
                        );


                    if (loadViewSlot != null)
                    {
                        loadViewSlot.Invoke(
                            plugin,
                            new object[]
                            {
                                viewSlot
                            }
                        );
                    }
                }


                if (
                    IsMirrorFunctionAvailable() &&
                    GetSpawnMirror(storedParts)
                )
                {
                    SpawnPovMirrorForCharacter(
                        target,
                        GetMirrorMode(storedParts) == "dynamic"
                    );
                }


            }
            catch
            {
            }
        }
    }
}
