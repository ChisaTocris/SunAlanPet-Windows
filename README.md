# SunAlanPet Windows

孙宇晨 SunPet 与邵艾伦 ShaolanPet 的 Windows 桌宠移植版。项目源码可公开复制；运行所需的原始头像、录音和台词清单由使用者从两个上游仓库的固定提交自行取得。应用运行时离线。

上游项目：[SunPet](https://github.com/Ganzhe2028/SunPet) · [ShaolanPet](https://github.com/Ganzhe2028/ShaolanPet)。两只桌宠在一个 Windows 程序中分别显示为透明窗口，支持点击播放、拖动、右键菜单和“捧哏联动”。素材获取脚本会逐文件校验哈希，不会改动原始内容。

## 交给 Codex

把下面这句话连同仓库链接发给 Codex：

> 克隆这个仓库，阅读 README.md 和 NOTICE.md；在 Windows 10/11 x64 上运行 `scripts/Fetch-Upstream-Assets.ps1` 获取并校验两个上游项目的原始素材，再运行 `scripts/Build-Windows.ps1`，最后启动 `publish/SunAlanPet.exe`。不要改动音频、头像或台词清单。

## 手动复现

需要 Windows 10/11 x64、Git 和 .NET 9 SDK。PowerShell 中运行：

```powershell
git clone https://github.com/ChisaTocris/SunAlanPet-Windows.git
cd SunAlanPet-Windows
powershell -ExecutionPolicy Bypass -File .\scripts\Fetch-Upstream-Assets.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Windows.ps1
.\publish\SunAlanPet.exe
```

获取脚本从固定提交提取两套原始素材，核对 28 条 SunPet 音频、37 条 ShaolanPet 音频、两张头像和两份台词清单。构建脚本发布包含 .NET 运行时的本地 Windows 程序；构建完成后运行程序不需要联网。设置保存在 `%APPDATA%\SunAlanPetWindows\settings.json`。

## 验证范围

本地版本已通过构建、自检、两种音频的 Windows `MediaPlayer` 打开检查，以及两个可见窗口的启动检查。实际鼠标点击和拖动尚未完成自动化实测。

## 授权与素材

Windows 移植代码采用 [MIT 许可](LICENSE)。头像和录音不属于 MIT 许可，版权归原权利人。本公开仓库不附带这些素材，也不发布包含素材的安装包。请阅读 [NOTICE.md](NOTICE.md) 和两个上游仓库的素材说明，仅用于学习交流及非商业场景。取得素材和使用素材时应遵守权利人的要求。