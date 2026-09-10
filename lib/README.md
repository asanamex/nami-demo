# lib/

Prebuilt Nami assemblies the demo mods reference, so a plain zip download builds
with no submodule and no package restore beyond the .NET SDK.

- `Nami.Sdk.dll` — plugin API (`NamiPlugin`, `IPluginContext`, config, Wave hook slots).
  Referenced by all four mods.
- `Nami.Tide.dll` — game bridge (`Tide`, `GameClass`, `GameObject`). Holds the Tide API;
  `Nami.Sdk` does not contain it. Referenced by SlowMo and FpsOverlay only
  (mirrors `samples/TideProbe`, which references both projects).

Source: built Release from the Nami repo at version **1.0.3**
(`src/Nami.Sdk`, `src/Nami.Tide`; `dotnet build -c Release`).

Upgrade path: when `Nami.Sdk` / `Nami.Tide` are published on NuGet, delete this folder
and replace each csproj `<Reference>` with
`<PackageReference Include="Nami.Sdk" Version="..." />`
(and `Nami.Tide` where used).
