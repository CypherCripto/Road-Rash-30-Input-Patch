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

## Build and use

Build on Windows with .NET Framework 4.x C# compiler:

```
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /target:winexe /platform:x86 /optimize+ /reference:System.Windows.Forms.dll /out:RoadRashInputFix.exe RoadRashInputFixLauncher.cs
```

Put the built `RoadRashInputFix.exe` beside `RoadRash95.exe`, then start the
game through that launcher. Verify that the game's SHA-256 matches the value
above. The launcher checks the bytes it changes and aborts if they differ.
To remove it, delete only `RoadRashInputFix.exe` and start the game normally.
It does not install MultiFix or change existing MultiFix settings.

The launcher uses normal process-memory patching techniques, including making
code writable. Security software may flag this behavior. Do not disable
security software or restore quarantined files to run the patch. A separate
test fixture developed during investigation was flagged and quarantined by
Bitdefender; it is not included here. This does not establish whether this
launcher will be flagged or whether any detection is a false positive.

No game files, assets, or third-party MultiFix source code are included.
