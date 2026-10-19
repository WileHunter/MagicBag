# MagicBag

一款面向安全 / CTF 的工具箱 + 笔记本桌面应用，主要用于工具存储、快捷启动、命令执行与笔记速记。

> 基于 WPF (.NET 8) + HandyControl，v1.6 完成了界面重构、主题系统、内置终端与笔记富文本增强。

## 功能、特性

- 工具箱

  1、树形列表，多级抽屉，快捷选中查看工具。

  2、网格状工具列表，卡片虚拟化，上千工具也能秒开。

  3、自动提取每个程序自身的图标，无图标时回退到分类图块。

  ![image-20250910113257137](https://github.com/WileHunter/MagicBag/blob/main/mdgitpic/Snipaste_2025-09-10_11-39-12.png)

  4、工具详情页面，快捷语句、目录、自定义语句，启动工具或执行命令。

  ![image-20250910113604615](https://github.com/WileHunter/MagicBag/blob/main/mdgitpic/Snipaste_2025-09-10_11-39-35.png)

- 内置终端

  1、命令默认在内置终端执行，输出实时回显。

  2、可切换为系统终端运行 CLI 工具。

  3、支持 cd / cls / exit，Stop 可结束整个进程树。

- 笔记本

  1、笔记采用 RTF 格式，富文本编辑模式。

  2、增强的富文本工具栏，提高文本可读性。

  3、Ctrl+S 快捷保存，并实时提醒未保存笔记。

  ![image-20250910113338370](https://github.com/WileHunter/MagicBag/blob/main/mdgitpic/Snipaste_2025-09-21_15-35-37.png)

- 数据库

  1、导航栏模式，选中分类后以表格呈现工具列表。

  ![image-20250910113358684](https://github.com/WileHunter/MagicBag/blob/main/mdgitpic/Snipaste_2025-09-10_11-39-50.png)

- 主题

  内置 7 套主题，可在标题栏一键切换，选择会被记住：

  | 主题 | 风格 |
  |---|---|
  | 科技蓝 | 深海蓝底 · 青紫霓虹 |
  | 墨白 | 黑 · 白 · 灰 极简（深色）|
  | 素白 | 柔白纸感 · 墨色点睛 |
  | 云白 | 仿 Apple 浅灰 · 天空蓝 |
  | 燕麦 | 暖米白 · 陶土橙 |
  | 雾紫 | 仿 Linear 冷白 · 靛紫 |
  | 鼠尾草 | 雾绿白 · 莫兰迪绿 |

- 关于
  1、版本信息与更新日志。

  ![image-20250910113431440](https://github.com/WileHunter/MagicBag/blob/main/mdgitpic/Snipaste_2025-09-10_11-39-58.png)

## 运行环境

- Windows 10 / 11
- [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（框架依赖版需要）

## 发布说明

- 发布格式：框架依赖单文件 EXE。
- 首次运行会在程序所在目录读写 `tools.json`、`settings.json` 与 `Notebooks/`。
- 从旧版本迁移时，把上述数据文件复制到新 `MagicBag.exe` 同级目录即可。

## 作者

- [@WileHunter](https://github.com/WileHunter/)
