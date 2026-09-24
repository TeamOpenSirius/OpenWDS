# OpenWDS

从《ワールドダイスター 夢のステラリウム》（WDS）Android 版本恢复音游玩法的非官方、非盈利 Unity 项目。目标是可维护的「标题 → Home → 选歌 → 游玩 → 结算 → 返回」闭环；不连接官方账号或服务器，不提供充值功能。

当前已接入普通与特殊谱面、触摸判定、CRI 音频、结算、成绩保存及部分原版 UI。账号、在线活动及 Home 中未恢复的入口不在当前范围内。工程仍在开发中，Editor 回归不代表所有 Android 设备均已验收。

## 开始开发

1. 安装 **Unity 2022.3.62f2**（具体 revision 见 `ProjectSettings/ProjectVersion.txt`）。Android 构建还需同版 Android Build Support、SDK、NDK 和 OpenJDK。
2. 安装 Git 和 Git LFS。Windows 上建议全程使用 Windows Git，避免与 WSL Git 的 LFS filter 混用。
3. 克隆并获取实际资源：

   ```powershell
   git lfs install
   git clone https://github.com/TeamOpenSirius/OpenWDS.git
   cd OpenWDS
   git lfs pull
   git lfs fsck
   ```

4. 用 Unity Hub 打开仓库根目录，等待 Packages 恢复和首次导入完成。UPM 依赖版本在 `Packages/manifest.json` 与锁文件中；工程使用 CRIWARE、Spine、DOTween、UniRx、Enhanced Scroller、MagicaCloth 等组件。缺失组件时先解决对应依赖和授权，不用占位脚本跳过。
5. 打开 `Assets/OpenWDS/Scenes/OfflineBootstrap.unity`，点击 Play。Editor 从仓库根目录 `SongResources/` 读取歌曲，进入标题后点击进入 Home。首次 Home 自动显示本地声明，每次运行仅弹出一次，也可从菜单重新查看。

如果看到 `version https://git-lfs.github.com/spec/v1`，说明拿到的是 LFS 指针，需要完成 `git lfs pull`。歌曲目录必须同时包含 `catalog.json`、`OpenWDS/AnotherNotations/catalog.json` 及目录中引用的封面、谱面、配置、正片和试听文件。

## 目录与修改入口

| 路径 | 用途 |
| --- | --- |
| `Assets/OpenWDS/Runtime/` | 离线适配、导航、选歌、判定、资源导入及演出 |
| `Assets/OpenWDS/Scripts/` | 恢复的原版类型及 UI 逻辑 |
| `Assets/OpenWDS/Editor/` | 场景生成、批处理验证和 Android 构建入口 |
| `Assets/OpenWDS/Scenes/` | 启动、Home、选歌、游玩及结算场景 |
| `Assets/Resources/` | 内置 prefab、字体、离线声明等 |
| `Assets/StreamingAssets/` | 内置公共 UI、模型、音效和 bundle；由 LFS 管理 |
| `SongResources/` | 外置歌曲和 catalog；由 LFS 管理，不放入 Assets、不生成 meta |
| `DeveloperTools/` | 独立仓库可用的资源打包脚本（Python 3，仅标准库） |

逆向工作区中的 `source/`、`reverse/`、`tools/`、`docs/` 位于本 Git 仓库之外，克隆此仓库不会得到它们。日常打开场景和构建使用本仓库已有产物；重新提取 APK 资源、重建机器码证据则需要完整逆向工作区。以机器码为逻辑依据，保留原始输入，不直接编辑生成物来掩盖提取问题。

## 歌曲包与 Android 构建

生成完整资源 ZIP：

```powershell
python DeveloperTools/package_song_resources.py
```

输出 `Build/Resources/OpenWDS-resources.zip`。`--base-manifest PREVIOUS.manifest.json` 可生成增量 ZIP；打包时 hash 仅用于识别变化，播放器不计算或校验 hash，也不绑定特定资源 ZIP。增量包省略的文件必须已存在，不能检测同大小的错误基线，修复时应导入完整包。

完整 ZIP 也可不带 manifest：根目录应直接包含两份 catalog 和 `OpenWDS/` 歌曲树，不要多套 `SongResources/` 目录。导入会检查路径、重复条目、解压长度及 catalog 依赖，成功后事务提交；出错保留旧资源。启动仅解析 catalog 并检查引用文件存在，实际资源格式由加载器按需读取；存在不代表内容一定正确。

关闭正在使用该工程的 Unity Editor，然后在 PowerShell 中执行（替换 Unity 路径）：

```powershell
$unity = 'D:\Program Files\Unity2022\Editor\Unity.exe'
& $unity -batchmode -quit -buildTarget Android -projectPath $PWD.Path -executeMethod OpenWDS.Editor.BuildOpenWDSAndroid.BuildTouchDeviceApk -logFile "$PWD/android-build.log"
```

输出 `Build/Android/OpenWDS-touch-test.apk`，ARM64 / IL2CPP。歌曲不内嵌 APK；把 ZIP 复制到设备，首次启动通过系统文件选择器导入。导入成功后会尝试删除所选 ZIP，系统拒绝时可手动删除。成绩与设置不随歌曲安装覆盖。

## 验证与贡献

一次只允许一个 Unity 进程操作工程。独立仓库先运行不依赖外层逆向报告目录的导入事务检查：

```powershell
& $unity -batchmode -quit -projectPath $PWD.Path -executeMethod OpenWDS.Editor.ValidateSongResourceImport.Run -logFile "$PWD/song-import-test.log"
```

日志应包含 `OPENWDS_SONG_IMPORT passed=True`，覆盖无 manifest / 无 hash、同大小更新、增量缺失基线、解压长度异常、目录穿越和中断回滚。

完整逆向工作区中的验证入口是 `tools/validate_unity_project.sh`、`tools/validate_frontend_views.sh`、`tools/validate_offline_song_resources.sh`，状态与复现要求见外层 `docs/STATUS.md` 和 `docs/WORKFLOW.md`。部分 Editor 验证依赖外层证据/夹具，不应把缺少这些输入造成的失败误判为克隆工程无法运行。

提交需说明问题、依据、修改与实际验证范围。UI 改动检查实际显示，判定修改核对机器码并跑对应边界回归；不要只用编译通过代替游玩验收。保留 Unity `.meta`，勿提交 Library、Temp、Build、个人存档或 SDK。

## 声明与授权

OpenWDS 不代表原作运营方。原作图片、音频、歌曲、角色、商标等归各权利人所有，本项目不授予这些素材的使用或再分发许可。禁止将含有原作素材的项目发行包和资源包用于商业用途（包括售卖、付费下载、收费服务或广告变现）。

OpenWDS 为非盈利项目，禁止用于商业用途。第三方素材、依赖仍适用其各自的权利与协议，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。游戏内声明提供日语、[简体中文](Assets/Resources/TextAsset/OfflineMenu/Statement.zh-Hans.txt)和 [English](Assets/Resources/TextAsset/OfflineMenu/Statement.en.txt) 三版。
