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
    [BepInPlugin(
        "com.Alice317.TimelinePOVSwitchX",
        "TimelinePOVSwitchX",
        "1.1.0"
    )]
    [BepInProcess("CharaStudio")]
    public partial class TimelinePOVSwitchX : BaseUnityPlugin
    {
        // Camera-capture locks remain active until the next POV Switch event.
        // Direction and position are independent.
        private static bool forceCapturedCameraDirection = false;
        private static Quaternion forcedCameraRotation = Quaternion.identity;

        private static bool forceCapturedCameraPosition = false;
        private static Vector3 forcedCameraPosition = Vector3.zero;

        private static TimelinePOVSwitchX pluginInstance;
        private static Timeline.Timeline _timeline;

        private static bool timelinePlaying = false;
        private static float previousPlaybackTime = -1f;

        // Persistent PerspectiveX recovery backup.
        // This does NOT store or restore povEnabled.
        private static bool povSettingsSessionActive = false;

        private static readonly string perspectiveXBackupPath =
            System.IO.Path.Combine(
                BepInEx.Paths.ConfigPath,
                "TimelinePOVSwitch.Backup.cfg"
            );

        private static Dropdown povDropdown;
        private static Text timelineValueText;
        private static Text timelineTooltip;

        private static bool updatingDropdown;

        // Separate draggable settings window for the selected POV keyframe.
        private Rect povSettingsWindowRect =
            new Rect(500f, 200f, 330f, 500f);

        // GUI.Window + GUI.DragWindow handle movement; OnGUI stores the
        // Rect returned by GUI.Window so the dragged position persists.

        private bool viewModeMenuOpen = false;
        private bool mirrorModeMenuOpen = false;

        // Studio toolbar button that creates a normal, permanent mirror.
        // Unlike Timeline mirrors, this one is never tracked or auto-deleted.
        private static Button permanentMirrorToolbarButton;
        private static Sprite embeddedToolbarBackgroundSprite;

        // Appears in BepInEx Configuration Manager (F1).
        // This is the master switch for ALL mirror-related features.
        private ConfigEntry<bool> enableMirrorFunction;

        // Mirror availability is checked against the actual
        // Az.Studio.PlanarReflection item loaded into Studio.
        private static bool mirrorModDetected = false;
        private static int lastMirrorAvailabilityCheckFrame = -1000;

        // The Studio toolbar is populated by multiple plugins during startup.
        // We wait until its button layout has stopped changing before placing
        // MIR/ROR, otherwise it can be placed above REC on first launch and
        // below it after toggling the setting.
        private static int toolbarStableFrames = 0;
        private static string lastToolbarLayoutSignature = "";
        private const int TOOLBAR_STABLE_FRAMES_REQUIRED = 15;

        // empty_icon.png embedded directly into this source file.
        // After compilation, no loose PNG is required.
        private const string EMBEDDED_TOOLBAR_BACKGROUND_BASE64 =
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAw0lEQVR4nO2VQQ6EIAxFC3bDWhaewbt6UE9Aomk7EUcnY5whcfybCW9HgP7+FlIehiGlRBhCCJxSGscRJNB1HVsGJGBmnsD4KlCiChSpAkWYiFSVYLCIzPMMii4ii8A0TUABVcU5UFWsA1VdHMBLJCIuc1dcyxNsHWXLM90X+wnn3Kcx53Iep7vrrcNd/pLCtS17P4D6yW4r+LmD37HNx9PBjR0+8GoyYfCguH8kwLjqU24tN03jPfA3cIyx73uQQNu2DyzdfRxARLbUAAAAAElFTkSuQmCC";

        // Persistent text buffers for the Custom numeric input boxes.
        // Without these, OnGUI would recreate the text every frame and
        // make decimal / multi-digit typing difficult.
        private Timeline.Keyframe customInputKeyframe = null;
        private string fovInput = "";
        private string headSwayInput = "";
        private string pitchLimitInput = "";
        private string positionSmoothingInput = "";
        private string forwardOffsetInput = "";
        private string nearClipInput = "";
        private string upOffsetInput = "";

        // Dropdown index -> actual Studio scene object ID
        private static List<int> povCharacterIds =
            new List<int>();


        // Temporary mirror created by a POV keyframe.
        private static OCIItem activePovMirror;
        private static bool activePovMirrorDynamic = true;

        // Static mirrors still get a few LateUpdate placements after the
        // keyframe fires so PerspectiveX has time to finish moving its camera.
        // After this reaches 0, the mirror becomes fully user-movable.
        private static int activePovMirrorSettleFrames = 0;

        private static bool mirrorTransformDebugLogged = false;

        private const float POV_MIRROR_DISTANCE = 0.45f;

        // Exact Studio item IDs for:
        // Az.Studio.PlanarReflection -> Planar Reflection Unlit
        private const int POV_MIRROR_GROUP = 119911;
        private const int POV_MIRROR_CATEGORY = 119911003;
        private const int POV_MIRROR_ITEM = 1;

        private static bool IsMirrorModDetected()
        {
            if (mirrorModDetected)
                return true;

            // Avoid doing the runtime item lookup every single frame while
            // Studio is still loading, or when the mirror mod is not installed.
            if (
                Time.frameCount -
                lastMirrorAvailabilityCheckFrame <
                60
            )
            {
                return false;
            }

            lastMirrorAvailabilityCheckFrame =
                Time.frameCount;

            mirrorModDetected =
                ResolveRuntimeMirrorItemId() >= 0;

            return mirrorModDetected;
        }


        private static bool IsMirrorFunctionAvailable()
        {
            if (
                pluginInstance == null ||
                pluginInstance.enableMirrorFunction == null ||
                !pluginInstance.enableMirrorFunction.Value
            )
            {
                return false;
            }

            return IsMirrorModDetected();
        }



        private void Start()
        {
            pluginInstance = this;

            enableMirrorFunction =
                Config.Bind(
                    "Mirror",
                    "Enable Mirror Function",
                    true,
                    "Enable Timeline mirror controls and the MIR/ROR toolbar button. Requires Az.Studio.PlanarReflection."
                );



            // If the previous game session crashed while Timeline owned
            // PerspectiveX settings, Pending=true remains in the backup CFG.
            // Restore those values now. POV enabled/disabled is untouched.
            RestorePerspectiveXBackupIfPending();
            povSettingsSessionActive = false;


            if (
                !TimelineCompatibility.IsTimelineAvailable()
            )
            {

                return;
            }


            TimelineCompatibility.AddInterpolableModelStatic<string, string>(
                "TimelinePOVSwitchX",
                "POVSwitch",
                "",
                "POV Switch",

                null,
                null,

                IsCompatible,
                GetValue,

                ReadPovValueFromXml,
                WritePovValueToXml,

                null,
                null,

                null,

                false,

                null,
                null
            );


            _timeline =
                Singleton<Timeline.Timeline>.Instance;


            Harmony harmony =
                new Harmony(
                    "com.Alice317.TimelinePOVSwitchX"
                );

            harmony.PatchAll(
                typeof(TimelinePOVSwitchX)
            );

            // PerspectiveX writes its final POV transform in OnCameraPreCull,
            // which happens after ordinary LateUpdate. Patch that exact method
            // so our optional camera locks get the final word before rendering.
            TryPatchPerspectiveXCameraPreCull(
                harmony
            );
        }



        private void OnDestroy()
        {
            permanentMirrorToolbarButton =
                null;

            DeleteActivePovMirror();

            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;

            // Normal plugin/game shutdown: restore if possible.
            // A hard crash will skip this, and startup recovery handles it.
            RestorePerspectiveXBackupIfPending();
            povSettingsSessionActive = false;
        }



        // =========================================================
        // UPDATE
        // =========================================================
    }
}
