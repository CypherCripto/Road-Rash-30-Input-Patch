# Road Rash 30th Anniversary - Input Patch (experimental)

An independent launcher for the GOG/Steam game executable SHA-256
`9e40e25737149ab095061b7154797a35495faea97644fafc5a1e616f1400c968`.
It applies changes to the game process in memory and leaves the original EXE
and saves untouched on disk. This is not an official MultiFix release.

Features: DualSense input corrections, cardinal D-pad directions, native
controller video skipping, mouse cursor release, and preventing minimization
when clicking outside the game. The initial test user confirmed these features
on their own installation. Other machines, controller models, connections,
video cases and game versions need testing. Diagonal D-pad inputs are not mapped.
Changes are all enabled together; this is not a settings application.

## Installation

1. [Download RoadRashInputFix.exe](https://github.com/CypherCripto/Road-Rash-30-Input-Patch/raw/refs/heads/main/RoadRashInputFix.exe).
2. Copy it into the game installation folder, next to `RoadRash95.exe`.
3. Start the game using `RoadRashInputFix.exe`.

The game must be purchased and installed separately. The original game executable
is not changed on disk. The player tested the original local launcher with
MultiFix installed; this portable build has been compiled but has not yet been
tested in a separate installation. Other setups require testing.

This patch targets the game executable with the SHA-256 hash listed above.
The launcher also verifies the expected code bytes before applying its changes.
It does not install MultiFix or change existing MultiFix settings.

**Steam users:** You may need to disable Steam Input for this game in the controller settings to use the controller's native compatibility.

## Uninstall

Delete `RoadRashInputFix.exe` from the game folder and launch the game normally.

## Build from source (optional)

On Windows, using the .NET Framework 4.x C# compiler:

```bat
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /target:winexe /platform:x86 /optimize+ /reference:System.Windows.Forms.dll /out:RoadRashInputFix.exe RoadRashInputFixLauncher.cs
```

The launcher uses normal process-memory patching techniques, including making
code writable. Security software may flag this behavior. Do not disable
security software or restore quarantined files to run the patch. A separate
test fixture developed during investigation was flagged and quarantined by
Bitdefender; it is not included here. This does not establish whether this
launcher will be flagged or whether any detection is a false positive.

No game files, assets, or third-party MultiFix source code are included.
