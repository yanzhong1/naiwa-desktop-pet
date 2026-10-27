# Naiwa Desktop Pet · 奶娃桌宠

使用 C#、WinForms 和 Windows 逐像素透明窗口实现的桌宠程序。支持拖动、分身、动作播放和鼠标靠近反应，不需要 Electron、Node.js 或 Python 运行环境。

## 直接下载（Windows 用户）

### [点击下载完整便携版 ZIP](https://github.com/yanzhong1/naiwa-desktop-pet/releases/latest/download/NaiwaDesktopPet-Windows-Portable.zip)

**下载后解压即可使用，无需编译，也不用安装 Python、Node.js 或 .NET SDK。**

1. 点击上面的下载链接，保存 `NaiwaDesktopPet-Windows-Portable.zip`。
2. 右键压缩包，选择“全部解压”。
3. 打开解压得到的“奶龙桌宠”文件夹，双击“奶龙桌宠.exe”。

程序默认静音，想听音效时右键角色，打开“声音”开关。右键“全部退出”可以关闭所有角色。

完整便携包已包含动画和音效，约 33.4 MiB。移动程序时，请把整个文件夹一起移动，保留 EXE 旁边的 `assets`。不要直接在压缩包里双击运行。

[查看最新版本和更新内容](https://github.com/yanzhong1/naiwa-desktop-pet/releases/latest)。GitHub 的“Code → Download ZIP”下载的是源码，普通使用请点上面的完整便携版。

本仓库公开程序源码、构建脚本和动画速度配置。第三方角色图片、动画、音效和图标不提交到源码目录，完整运行素材由 Releases 便携包提供，其许可与程序代码分开，详见 [素材来源与许可范围](THIRD_PARTY_ASSETS.md)。

## 功能

- 左键拖动移动角色；单击播放大笑，双击喷出一个分身。
- 右键“分裂4只”新增四只大号分身，连续减速后停在屏幕内的随机位置，数量不设固定上限。
- 右键“齐齐哈尔”让当前所有角色同步大笑；支持收回分身、移除单只和全部退出。
- 待机、跑步、大笑、挥手、闭眼休息和自由散步；跑步与挥手播放两轮后回到待机。
- 鼠标靠近时摆出防备姿势，悬浮身体或移远后取消；拖动、菜单和主动动作优先。
- 大小、置顶和声音开关；默认静音，托盘图标不显示悬停说明文字。

## 环境

Windows 10 / 11，启用 .NET Framework 4.x。构建使用系统自带的 C# 编译器和 Windows PowerShell 5.1，无需另装 .NET SDK。

## 构建和运行

在仓库目录打开 PowerShell。先导入现有桌宠文件夹中的素材：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\import-assets.ps1 -Source "C:\path\to\奶龙桌宠"
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
& '.\dist\奶龙桌宠\奶龙桌宠.exe'
```

`-Source` 也可以直接指定包含 `animations` 的 `assets` 目录。脚本只复制动画 PNG、音效、图标和动画配置，不导入预览、说明、验证报告或个人设置。导入的媒体文件默认被 Git 忽略，不会随源码提交。

只检查源码能否编译，不导入素材、不运行程序：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -CompileOnly
```

本地打包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

输出为 `dist/NaiwaDesktopPet-Windows-Portable.zip`，只包含 `奶龙桌宠.exe` 与 `assets`。完整文件夹可以移动，EXE 需要与 `assets` 放在一起。打包不会自动上传任何文件。

## 素材结构

```text
assets/
  animation.ini
  icon.ico                 # 可选
  sounds/laugh.wav         # 可选；没有时静音播放
  animations/
    idle/*.png
    walk/*.png
    laugh/*.png
    wave/*.png
    sleep/*.png
    dragged/*.png
    run/*.png
    near/*.png
```

每个动作至少需要一张 PNG，按数字文件名排序。图片支持透明通道。`assets/animation.ini` 控制各动作的播放帧率，修改后重启生效；大笑音轨应与大笑动画的完整时长匹配。

## 数据与退出

程序运行时不调用网络服务。窗口位置、大小、置顶、散步和声音设置保存在 `%LOCALAPPDATA%\NaiwaDesktopPet\settings.ini`，异常日志也写在该本地目录，不放入仓库或便携包。

右键选择“全部退出”关闭所有角色，也可以运行：

```powershell
& '.\dist\奶龙桌宠\奶龙桌宠.exe' --exit
```

## 仓库结构

```text
src/PetApp.cs            程序源码
src/app.manifest         Windows 清单
assets/animation.ini     动画速度配置
build.ps1                编译并复制本地素材
import-assets.ps1        导入本地素材
package.ps1              构建便携包
```

## 许可证

本仓库中的程序代码、脚本与配置使用 [MIT License](LICENSE)。完整便携包附带的角色形象、第三方动画和声音不包含在此授权内，其权利归各自权利人。这个项目是非官方桌宠实现，与角色权利人没有隶属或代言关系。
