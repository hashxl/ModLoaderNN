# TCModLoader

A standalone mod loader for *Third Crisis: Neon Nights*. It uses Unity Doorstop
to start directly inside the game.

TCModLoader provides the `ITCMod`, `ModManifest`, and `ModSpriteResolver` APIs.
It also uses Mono.Cecil to redirect legacy mod references from
`Assembly-CSharp` to `TCModLoader.dll` at load time.

## Installing the loader

Download `TCModLoader-Standalone.zip` from the latest GitHub Release and extract
it into the game directory, next to `Third Crisis Neon Nights.exe`.

The installed files should look like this:

```text
Third Crisis Neon Nights/
├── Third Crisis Neon Nights.exe
├── Third Crisis Neon Nights_Data/
├── winhttp.dll
├── doorstop_config.ini
└── TCModLoader/
    └── Runtime/
        ├── TCModLoader.dll
        ├── Mono.Cecil.dll
        └── Mono.Cecil.Rocks.dll
```

`winhttp.dll` is Unity Doorstop. The included `doorstop_config.ini` points it to:

```ini
target_assembly=TCModLoader\Runtime\TCModLoader.dll
```

Start the game normally through Steam. TCModLoader creates these directories
automatically:

```text
TCModLoader/
├── Cache/
└── Logs/
    └── TCModLoader.log
```

The log and cache are generated locally and are not included in the download or
source repository. To verify the installation, open
`TCModLoader/Logs/TCModLoader.log` and look for:

```text
=== TCModLoader standalone bootstrap starting ===
=== TCModLoader ready: 0 mod(s) loaded ===
```

The mod count will be greater than zero when mods are installed.

## Installing mods

Each mod belongs in its own directory under `Mods`. A mod requires at least a
`manifest.json` and its compiled DLL:

```text
Third Crisis Neon Nights/
└── Mods/
    └── MyCoolMod/
        ├── manifest.json
        ├── MyCoolMod.dll
        └── Assets/
```

Example manifest:

```json
{
  "Name": "MyCoolMod",
  "Author": "YourName",
  "Version": "v1.0.0",
  "UniqueIdentifier": "mycoolmod",
  "PathToDLL": "MyCoolMod.dll",
  "Enabled": true,
  "Requires": {
    "neonnightsdk": "v0.2.0"
  },
  "LoadAfter": [
    "another-mod-id"
  ]
}
```

`Requires` contains mandatory dependencies and their minimum versions.
`LoadAfter` only affects load order when the referenced mod is installed.

## Creating a mod

Mods target .NET Framework 4.7.2 and implement `ITCMod`.

Example project:

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
    <GameDir>..\..</GameDir>
    <ManagedDir>$(GameDir)\Third Crisis Neon Nights_Data\Managed</ManagedDir>
    <TCModLoaderDir>$(GameDir)\TCModLoader\Runtime</TCModLoaderDir>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="TCModLoader">
      <HintPath>$(TCModLoaderDir)\TCModLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDir)\UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedDir)\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDir)\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Example mod class:

```csharp
using Modding;
using UnityEngine;

public sealed class MyMod : ITCMod
{
    public void OnModLoaded(ModManifest manifest)
    {
        Debug.Log($"[{manifest.Name}] Loaded");
    }

    public void OnFrame()
    {
    }

    public void OnModUnLoaded()
    {
    }
}
```

Build it with:

```powershell
dotnet build -c Release
```

Copy the resulting DLL into the same mod directory as `manifest.json`. If a
modified DLL appears to remain cached, close the game and delete
`TCModLoader/Cache`.

## Publishing a GitHub Release

Distribute the loader as a ZIP with this exact layout:

```text
TCModLoader-Standalone.zip
├── winhttp.dll
├── doorstop_config.ini
└── TCModLoader/
    └── Runtime/
        ├── TCModLoader.dll
        ├── Mono.Cecil.dll
        └── Mono.Cecil.Rocks.dll
```

Do not include game files, installed mods, `TCModLoader/Logs`, or
`TCModLoader/Cache`.

Example packaging command:

```powershell
dotnet build .\TCModLoader.csproj -c Release
Copy-Item .\bin\Release\TCModLoader.dll .\release\TCModLoader\Runtime\ -Force
Compress-Archive -Path .\release\* -DestinationPath .\TCModLoader-Standalone.zip -Force
```

Attach `TCModLoader-Standalone.zip` to a GitHub Release. Players should download
the ZIP from Releases instead of cloning the source repository into the game.

## Updating or removing

To update, replace the files from the latest standalone package. Delete
`TCModLoader/Cache` if mods fail after an update.

To remove TCModLoader, delete:

```text
winhttp.dll
doorstop_config.ini
TCModLoader/
```

Do not delete files from `Third Crisis Neon Nights_Data`.
