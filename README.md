# JellybeansPicUploader

基于 **Avalonia UI** 的跨平台桌面图床客户端。将图片上传至你的 **GitHub 仓库**，并一键生成 Markdown、HTML、jsDelivr、GitHub Pages 等多种外链格式。

完全开源，可免费使用。若觉得有帮助，欢迎在 GitHub 点个 Star。

## 功能特性

### 上传

- 从文件对话框、**剪贴板**、**拖拽**或 **图片 URL** 添加待上传图片
- 批量上传，支持选中项删除与全选
- 上传前可选 **压缩**（WebP / MozJPEG / AVIF）与 **文字水印**
- 自定义文件名（哈希、前缀）与仓库内目录规则（根目录 / 按日期 / 按仓库名 / 自定义子目录）
- 上传进度与可取消操作
- 上传完成后自动复制链接（可关闭）

### 链接与发布

- 内置多种链接规则：GitHub Raw、GitHub Pages、jsDelivr、Statically、国内 jsDelivr 镜像等
- 支持 **自定义链接模板**（占位符：`{{owner}}`、`{{repo}}`、`{{branch}}`、`{{path}}` 等）
- 输出格式：纯链接、Markdown、HTML、BBCode 或自定义模板
- 上传后可选用 **GitHub Pages** 或 **jsDelivr** 作为访问方式；jsDelivr 支持按分支、Commit Hash 或 Tag 引用版本

### 管理

- 浏览仓库内已上传图片，远程缩略图预览
- 批量选择与删除远程文件
- 一键部署 **GitHub Pages** 分支

### 登录与配置

- **GitHub OAuth** 授权登录（与 PicX 网页端共用 GitHub App）
- 或使用 **Personal Access Token** 手动登录
- 配置目标仓库、分支、提交说明、邮箱等

### 工具箱

- 本地图片压缩、Base64 编码、水印处理（不经过 GitHub 上传）

### 快捷上传

- 主窗口最小化后，屏幕右下角显示贴边 **「+」** 便签
- 点击打开快捷面板：拖拽、粘贴或选择文件即可快速上传

## 技术栈

| 类别 | 技术 |
|------|------|
| UI 框架 | [Avalonia UI](https://avaloniaui.net/) 12 |
| 主题 | [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) |
| 图像处理 | [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) |
| 运行时 | .NET 10 |
| 存储后端 | GitHub REST API（Contents API / Git 树批量提交） |

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)（开发或框架依赖发布）
- Windows / Linux / macOS 桌面环境

## 快速开始

### 从源码运行

在仓库根目录执行：

```bash
cd JellybeansPicUploader
dotnet run --project JellybeansPicUploader.Avalonia/JellybeansPicUploader.Avalonia.csproj
```

### Windows 发布包

在 `JellybeansPicUploader` 目录下执行：

```powershell
.\publish.ps1
```

产物位于仓库根目录的 `dist/`：

| 目录 / 文件 | 说明 |
|-------------|------|
| `JellybeansPicUploader-win-x64-self-contained/` | 自包含运行时，无需单独安装 .NET |
| `JellybeansPicUploader-win-x64-framework-dependent/` | 需本机已安装 .NET 10 运行时 |
| `JellybeansPicUploader-1.0.0-win-x64-*.zip` | 上述两种形态的压缩包 |

也可单独使用 `dotnet publish` 与 `Properties/PublishProfiles/` 中的配置文件。

## 使用说明

### 1. 登录 GitHub

**方式 A：OAuth（推荐）**

1. 打开应用，进入 **「登录/配置」**
2. 点击 **GitHub 授权登录**，浏览器完成授权
3. 若使用 GitHub App，按提示安装应用到目标账号或组织

**方式 B：Personal Access Token**

1. 在 GitHub → Settings → Developer settings 创建 Token（需 `repo` 等仓库权限）
2. 在应用中粘贴 Token 并登录

### 2. 配置仓库

- 填写 **Owner**、**Repository**、**Branch**（留空则使用默认分支）
- 设置图片存放路径（如 `images`）与目录组织方式
- 首次使用可让应用自动初始化仓库 README

### 3. 上传并复制链接

1. 在 **「上传」** 页添加图片
2. 在 **「设置」** 中调整压缩、水印、链接规则与输出格式
3. 点击上传；完成后在列表中复制单条或批量链接

### 4. GitHub Pages（可选）

若希望通过 `https://<owner>.github.io/<repo>/...` 访问图片：

1. 在 **「管理」** 中部署 GitHub Pages
2. 在设置中将 **上传后发布方式** 设为 GitHub Pages，或选用 GitHubPages 链接规则

## 项目结构

```
JellybeansPicUploader/          # 仓库根目录
├── README.md
├── LICENSE                     # AGPL-3.0
└── JellybeansPicUploader/
    ├── JellybeansPicUploader.Avalonia/   # 当前主推的 Avalonia 客户端
    │   ├── ViewModels/                   # ShellViewModel 等业务逻辑
    │   ├── Views/Pages/                  # 登录、上传、管理、设置、工具箱
    │   ├── Views/QuickUpload/            # 贴边快捷上传窗口
    │   ├── Services/                     # GitHub API、OAuth、图像处理等
    │   └── Models/                       # 设置与数据模型
    ├── JellybeansPicUploader.Wpf/        # 历史 WPF 实现（维护中/对照）
    ├── PicX.Wpf/                         # 更早的 PicX 命名版本
    └── publish.ps1                       # Windows x64 发布脚本
```

## 分支说明

| 分支 | 说明 |
|------|------|
| `avalonia` | 以 Avalonia 实现为主，推荐用于桌面客户端开发与发布 |
| `main` / `master` | 可能仍包含 WPF 或历史 Web 相关文件，以仓库实际内容为准 |

## 配置与数据存储

应用设置保存在本机用户目录（由 `AppSettingsService` 管理），包含 Token、仓库信息与用户偏好。**请勿将含 Token 的配置文件提交到公开仓库。**

## 参与贡献

欢迎提交 Issue 与 Pull Request。开发前请先阅读现有 `JellybeansPicUploader.Avalonia` 中的代码风格与 MVVM 结构，改动尽量保持最小范围并避免影响无关功能。

## 许可证

本项目采用 [GNU Affero General Public License v3.0](LICENSE)（AGPL-3.0）。使用、修改与分发请遵守许可证条款；若你通过网络提供服务基于本软件，需按 AGPL 要求提供相应源代码。

## 相关链接

- 仓库：<https://github.com/JellyBeans/JellybeansPicUploader>
- Avalonia：<https://avaloniaui.net/>
