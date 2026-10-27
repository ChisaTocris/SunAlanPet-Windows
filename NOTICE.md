# Asset and code notice

This public repository contains the Windows port source code under the MIT License. It does not contain the original avatars, recordings, clip manifests, or an application package containing them.

For local use, `scripts/Fetch-Upstream-Assets.ps1` retrieves byte-for-byte source assets from these fixed upstream commits and checks every file against a SHA256 manifest:

- [SunPet](https://github.com/Ganzhe2028/SunPet), commit `9b68d0911552d8c880a46ca72980d3f9af9743ee`
- [ShaolanPet](https://github.com/Ganzhe2028/ShaolanPet), commit `7ce7e2b1ed49c4da66d19d5aac419794af0d32db`

The upstream authors state that the avatars and audio are outside the MIT license and belong to the original rights holders. Their projects are for learning and non-commercial use. See the upstream [SunPet notice](https://github.com/Ganzhe2028/SunPet/blob/main/NOTICE.md) and [ShaolanPet notice](https://github.com/Ganzhe2028/ShaolanPet/blob/main/NOTICE.md). Do not redistribute or commercialize the media without permission from the rights holders.

The Windows application does not clone voices or generate new media. It plays the copied local files and does not connect to the network at runtime.