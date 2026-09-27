# Game Framework source lock

- Upstream: https://github.com/EllanJiang/GameFramework
- Commit: `d0c010b05167c58e92350449d04864a91ca13fd2`
- Commit date: 2021-09-28 UTC
- Archive: https://github.com/EllanJiang/GameFramework/archive/d0c010b05167c58e92350449d04864a91ca13fd2.zip
- Downloaded archive SHA-256: `68441944E8D5915ADF5D22429B84DD5A5EDE0400A8AF48D4CB8A4CC51792A226`
- License: MIT, preserved in `LICENSE.md` and each upstream source header.

Imported the official `GameFramework/**/*.cs` source tree without modification,
excluding `Properties/AssemblyInfo.cs`. The legacy solution/project and build
outputs are not imported. Unity compiles the source through the locally added
`GameFramework.asmdef`; `.meta` files are local Unity asset identifiers.

This is the official pure C# core, not UnityGameFramework's Unity component
package. This project currently uses Fsm, Procedure and Event; it does not claim
to have integrated all framework modules. Do not also import the core DLL from
UnityGameFramework because that would define the same classes twice.

Version pinning makes this dependency reproducible. The upstream master last
changed in 2021; recent active maintenance or Unity 2022 compatibility is not
inferred from its name or popularity. Compatibility is checked in this project.
