 Koikatsu Studio Timeline plugin that adds direct **PerspectiveX POV control** to Timeline.

TimelinePOVSwitchX allows you to switch between character POVs using Timeline keyframes, automatically enable or disable PerspectiveX POV, load PerspectiveX View Slots, and store custom camera settings independently for individual keyframes.

It also includes optional **AzPlanarReflection** integration for spawning Dynamic, Static, and permanent POV mirrors.


## Features

- Add `POV Switch` keyframes directly to Timeline
- Select any Studio character as the POV target
- Automatically enable PerspectiveX with `Force POV`
- Disable PerspectiveX POV directly from a Timeline keyframe
- Load PerspectiveX View Slots 1–3
- Store custom PerspectiveX camera settings independently for each keyframe
- Automatically back up and restore your original PerspectiveX settings
- Optional **Dynamic** and **Static** POV mirrors using AzPlanarReflection *(Beta / Work in Progress)*
- Optional Studio `MIR/ROR` button for quickly spawning a permanent mirror in front of the current POV
- Mirror functionality can be enabled or disabled through the BepInEx F1 configuration menu


---

# Preview

<img width="1686" height="975" alt="image" src="https://github.com/user-attachments/assets/2adc0a6e-355e-403c-88da-80a0cb594f9d" />

---


# Requirements

TimelinePOVSwitchX is designed for **Koikatsu Studio** and requires the standard modding framework together with Timeline and PerspectiveX.

## Required

- **BepInEx 5**
- **KKAPI**
- **Timeline** — Tested with `1.5.6`
- **PerspectiveX** — Tested with `1.3.4`

Other versions may work, but the versions above are the ones currently tested.

## Optional

- **AzPlanarReflection** — required only for mirror functionality

Without AzPlanarReflection, all core TimelinePOVSwitchX functionality remains available.

---

# Installation

1. Make sure **BepInEx 5**, **KKAPI**, **Timeline**, and **PerspectiveX** are installed and working.

2. Download `TimelinePOVSwitchX.dll` from the repository's **Releases** page.

3. Place the DLL inside:

```text
BepInEx/plugins/
```

For example:

```text
Koikatu/
└── BepInEx/
    └── plugins/
        └── TimelinePOVSwitchX.dll
```

4. Start the game and open Studio.

5. Open Timeline and look for the `POV Switch` interpolable.

If **AzPlanarReflection** is installed, TimelinePOVSwitchX will automatically detect the required mirror item and enable its optional mirror functionality.

If it is not installed, no additional action is required. The mirror functionality will remain unavailable while the rest of TimelinePOVSwitchX continues to work normally.

---

## Building From Source

TimelinePOVSwitchX targets **.NET Framework 3.5**.

To build the project, you will need to reference the following assemblies from your Koikatsu/BepInEx installation:

### BepInEx

- `BepInEx.dll`
- `0Harmony.dll`

### Koikatsu / Unity

- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll`
- `UnityEngine.dll`
- `UnityEngine.UI.dll`

### Plugin References

- `KKAPI.dll`
- `Timeline.dll`

**PerspectiveX does not need to be referenced when building.** TimelinePOVSwitchX communicates with PerspectiveX at runtime through reflection.

Then build in Release mode:

```bash
dotnet build -c Release
```

The project requires the **.NET Framework 3.5** reference assemblies.
---

# My Links

- **Contact:** alice@moonbinder.com

Bug reports and feature requests can be submitted through the repository's **Issues** page.

---

# Credits

- **PerspectiveX** — POV camera functionality used by TimelinePOVSwitchX
- **Timeline** — Timeline integration
- **KKAPI / BepInEx** — Koikatsu plugin framework
- **AzPlanarReflection** — Optional planar reflection functionality used by the mirror features

TimelinePOVSwitchX does not redistribute these dependencies. Please obtain them from their respective authors/releases.

---

# License

TimelinePOVSwitchX is licensed under the **MIT License**.

You are free to use, modify, and redistribute TimelinePOVSwitchX under the terms of the MIT License.

See [`LICENSE`](LICENSE) for the complete license text.

---

# Notes

- Mirror functionality is currently **Beta / Work in Progress**.
- AzPlanarReflection is optional and is not required for POV switching.
- If a character referenced by a POV Switch keyframe is deleted, that keyframe will no longer be able to switch to that character.
- `Disable POV` is a special Timeline target and does not require a character.
- PerspectiveX camera settings are restored after Timeline playback, but the POV enabled/disabled state is intentionally left unchanged.
