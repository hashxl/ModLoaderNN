# TCModLoader — Modding Guide for Third Crisis: Neon Nights

Everything you need to install the standalone mod loader, use existing mods, or create your own from scratch. Covers Doorstop setup, the ITCMod API, and mod project structure.

## Contents

1. [What is TCModLoader](#1-what-is-tcmodloader)
2. [Prerequisites](#2-prerequisites)
3. [Install TCModLoader](#3-install-tcmodloader)
4. [Install a Mod (for players)](#4-install-a-mod-for-players)
5. [Create Your Own Mod](#5-create-your-own-mod)

---

## 1. What is TCModLoader

**TCModLoader** is a standalone Unity Doorstop plugin that brings mod support to *Third Crisis: Neon Nights*. BepInEx is not required. TCModLoader reimplements the `ITCMod` interface, `ModManifest`, and `ModSpriteResolver` so that mods compiled against those types load and run.

It works by using **Mono.Cecil** to patch each mod's DLL at load time, redirecting type references from the old `Assembly-CSharp` to `TCModLoader.dll`. This is transparent to mod authors — you write code against the same API, and the loader handles the rest.

---

## 2. Prerequisites

### For players (installing mods)

- Third Crisis: Neon Nights (Steam version)
- The TCModLoader standalone package, including Unity Doorstop

### For mod creators

Everything above, plus:

- **.NET SDK** (any version that supports `net472` target — .NET 6+ SDK works)
- A code editor (VS Code, Rider, Visual Studio — anything with C# support)
- Basic knowledge of C# and Unity

> **Note:** You do **not** need Unity Editor installed. Mods are compiled as plain .NET class libraries that reference the game's existing DLLs.

---

## 3. Install TCModLoader

Extract the standalone package into the game's root folder:

```
Third Crisis Neon Nights/
  winhttp.dll
  doorstop_config.ini
  TCModLoader/
    Runtime/
      TCModLoader.dll
      Mono.Cecil.dll
      Mono.Cecil.Rocks.dll
    Cache/
    Logs/
  Third Crisis Neon Nights_Data/
  Third Crisis Neon Nights.exe
```

### Verify

Launch the game and check `TCModLoader/Logs/TCModLoader.log`:

```
[Warning: TCModLoader] === TCModLoader v1.0.0 starting ===
[Warning: TCModLoader] === TCModLoader ready: 0 mod(s) loaded ===
```

If you see this, the loader is installed and ready for mods.

> **Updating the loader:** Replace `TCModLoader/Runtime/TCModLoader.dll`. Delete `TCModLoader/Cache` only when patched mod assemblies need to be rebuilt.

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
  TCModLoader/
  Third Crisis Neon Nights.exe
```
4. Launch the game — the mod loads automatically
5. Check `TCModLoader/Logs/TCModLoader.log` to confirm it loaded

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
    <TCModLoaderDir>$(GameDir)\TCModLoader\Runtime</TCModLoaderDir>
  </PropertyGroup>

  <ItemGroup>
    <!-- TCModLoader (provides ITCMod, ModManifest, ModSpriteResolver) -->
    <Reference Include="TCModLoader">
      <HintPath>$(TCModLoaderDir)\TCModLoader.dll</HintPath>
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

# Clear TCModLoader cache (if having issues)
rmdir /s /q ..\TCModLoader\Cache
```

1. Run `dotnet build -c Release --no-incremental` from your mod folder
2. Copy `bin/Release/MyMod.dll` to the mod folder root (next to manifest.json)
3. Launch the game
4. Check `TCModLoader/Logs/TCModLoader.log` and the Unity player log

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
