# TCModLoader — Modding Guide for Third Crisis: Neon Nights

Everything you need to install the mod loader, use existing mods, or create your own from scratch. Covers BepInEx setup, the ITCMod API, and mod project structure.

## Contents

1. [What is TCModLoader](#1-what-is-tcmodloader)
2. [Prerequisites](#2-prerequisites)
3. [Install BepInEx + TCModLoader](#3-install-bepinex--tcmodloader)
4. [Install a Mod (for players)](#4-install-a-mod-for-players)
5. [Create Your Own Mod](#5-create-your-own-mod)

---

## 1. What is TCModLoader

**TCModLoader** is a BepInEx plugin that brings mod support to *Third Crisis: Neon Nights*. The game does not include mod support or BepInEx out of the box — you need to install both manually. TCModLoader reimplements the `ITCMod` interface, `ModManifest`, and `ModSpriteResolver` so that mods compiled against those types load and run.

It works by using **Mono.Cecil** to patch each mod's DLL at load time, redirecting type references from the old `Assembly-CSharp` to `TCModLoader.dll`. This is transparent to mod authors — you write code against the same API, and the loader handles the rest.

---

## 2. Prerequisites

### For players (installing mods)

- Third Crisis: Neon Nights (Steam version)
- **BepInEx 5.4.23** (Unity Mono, x64) — must be downloaded and installed manually

### For mod creators

Everything above, plus:

- **.NET SDK** (any version that supports `net472` target — .NET 6+ SDK works)
- A code editor (VS Code, Rider, Visual Studio — anything with C# support)
- Basic knowledge of C# and Unity

> **Note:** You do **not** need Unity Editor installed. Mods are compiled as plain .NET class libraries that reference the game's existing DLLs.

---

## 3. Install BepInEx + TCModLoader

### Step 1 — Install BepInEx

The game does **not** come with BepInEx. You need to download and install it manually.

1. Download **BepInEx 5.4.23** (Unity Mono, x64) from the official GitHub releases
2. Extract the contents of the zip **into the game's root folder** (where `Third Crisis Neon Nights.exe` is)
3. Launch the game **once** and close it — BepInEx will generate the folder structure (`BepInEx/plugins/`, `BepInEx/config/`, etc.)

> **Important:** Make sure you download **BepInEx 5.x** (not 6.x). The game uses Unity Mono, so pick the `BepInEx_win_x64_5.4.23.2.zip` package. After extracting, you should see `winhttp.dll` and `doorstop_config.ini` next to the game's .exe.

### Step 2 — Install TCModLoader

Download `TCModLoader.dll` from the releases and place it in the BepInEx plugins folder:

```
Third Crisis Neon Nights/
  winhttp.dll                 ← from BepInEx
  doorstop_config.ini         ← from BepInEx
  BepInEx/                    ← created after first launch
    plugins/
      TCModLoader.dll         ← put it here
    core/
    cache/
  Third Crisis Neon Nights_Data/
  Third Crisis Neon Nights.exe
```

### Step 3 — Verify

Launch the game again. Check `BepInEx/LogOutput.log` — you should see:

```
[Warning: TCModLoader] === TCModLoader v1.0.0 starting ===
[Warning: TCModLoader] === TCModLoader ready: 0 mod(s) loaded ===
```

If you see this, the loader is installed and ready for mods.

> **Updating the loader:** If you update TCModLoader.dll, delete `BepInEx/cache/chainloader_typeloader.dat` to force BepInEx to re-scan the plugins folder.

---

## 4. Install a Mod (for players)

Each mod is a folder placed inside the mods directory in the game's root directory (next to the .exe). If the mods folder does not exist, create it. Every mod folder must contain a manifest.json and the mod's .dll file.

Download the mod (usually a .zip file).
Create a mods folder in the game's root directory if it doesn't already exist.
Extract the mod into the mods folder.
Verify the folder structure looks like this:
```
Third Crisis Neon Nights/
  mods/
    MyCoolMod/             ← mod folder (name can be anything)
      manifest.json        ← required
      MyCoolMod.dll        ← the compiled mod
      assets/              ← sprites, images (optional)
  BepInEx/
  Third Crisis Neon Nights.exe
```
4. Launch the game — the mod loads automatically
5. Check `BepInEx/LogOutput.log` to confirm it loaded

### manifest.json format

```json
{
  "Name": "MyCoolMod",
  "Author": "YourName",
  "Version": "v1.0.0",
  "PathToDLL": "MyCoolMod.dll",
  "Requires": {
    "neonnightsdk": "v0.2.0"
  },
  "Enabled":true

}
```

---

## 5. Create Your Own Mod

### Step 1 — Create the project folder

Inside the game's root directory, create a folder for your mod:

```
Third Crisis Neon Nights/
  MyMod/
    MyMod.cs
    MyMod.csproj
    manifest.json
    assets/               (for sprites)
```

### Step 2 — Create manifest.json

```json
{
  "Name": "MyMod",
  "Author": "YourName",
  "Version": "v1.0.0",
  "PathToDLL": "MyMod.dll",
  "Requires": {
    "neonnightsdk": "v0.2.0"
  },
  "Enabled":true
}
```

### Step 3 — Create MyMod.csproj

This tells .NET how to build the project and where to find the game's DLLs:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>MyMod</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Paths relative to the mod folder -->
    <GameDir>..</GameDir>
    <ManagedDir>$(GameDir)\Third Crisis Neon Nights_Data\Managed</ManagedDir>
    <BepInExDir>$(GameDir)\BepInEx</BepInExDir>
  </PropertyGroup>

  <ItemGroup>
    <!-- TCModLoader (provides ITCMod, ModManifest, ModSpriteResolver) -->
    <Reference Include="TCModLoader">
      <HintPath>$(BepInExDir)\plugins\TCModLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- Unity Engine -->
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDir)\UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedDir)\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.PhysicsModule">
      <HintPath>$(ManagedDir)\UnityEngine.PhysicsModule.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- Game code -->
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDir)\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="ANToolkit.Utility">
      <HintPath>$(ManagedDir)\ANToolkit.Utility.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

> **Why Private=false?** This tells MSBuild not to copy the game's DLLs into your output folder. Your mod runs inside the game process where those DLLs are already loaded.

### Step 4 — Write your mod class

Create `MyMod.cs` — every mod must implement the `ITCMod` interface:

```csharp
using Asuna.Dialogues;
using Modding;
using UnityEngine;

public class MyMod : ITCMod
{
    private ModManifest _manifest;

    public void OnModLoaded(ModManifest manifest)
    {
        _manifest = manifest;
        Debug.Log("[MyMod] Hello, Neon Nights!");
    }

    public void OnModUnLoaded()
    {
        Debug.Log("[MyMod] Goodbye!");
    }

    public void OnDialogueStarted(Dialogue dialogue) { }
    public void OnLineStarted(DialogueLine line) { }
    public void OnFrame() { }
}
```

### Step 5 — Build and deploy

```bash
# Build
cd MyMod
dotnet build -c Release --no-incremental

# Copy DLL to mod folder
cp bin/Release/MyMod.dll MyMod.dll

# Clear BepInEx cache (if having issues)
del ..\BepInEx\cache\chainloader_typeloader.dat
```

1. Run `dotnet build -c Release --no-incremental` from your mod folder
2. Copy `bin/Release/MyMod.dll` to the mod folder root (next to manifest.json)
3. Launch the game
4. Check `BepInEx/LogOutput.log` for `[MyMod] Hello, Neon Nights!`

### Step 6 — Distribute your mod

To share your mod, zip the mod folder with only the essential files:

```
MyMod/
  manifest.json
  MyMod.dll
  assets/               (if using custom sprites)
    my_sprite.png
```

> **Do not include** `bin/`, `obj/`, `.csproj`, or `.cs` source files in the distribution. Players only need the `manifest.json`, the compiled `.dll`, and any `assets/`.
