
# 📌 AlwaysOnTop

### A lightweight Windows utility to keep your favourite applications always on top.

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/C%23-.NET%20Framework%204.x-239120?logo=csharp&logoColor=white)
![Dependencies](https://img.shields.io/badge/Dependencies-Zero%20External%20Packages-10B981)
![Size](https://img.shields.io/badge/EXE-~65%20KB-6366F1)
![License](https://img.shields.io/badge/License-MIT-F59E0B)
[![Latest Release](https://img.shields.io/github/v/release/LibbyLamarr/AlwaysOnTop?label=Download&color=brightgreen)](https://github.com/LibbyLamarr/AlwaysOnTop/releases/latest)

**AlwaysOnTop** is a lightweight, portable Windows System Tray application that allows you to keep selected application windows visible above other windows.

Whether you're monitoring information, keeping a reference window visible, or working across multiple applications, AlwaysOnTop gives you control over which windows stay on top.

Unlike simple topmost toggles, AlwaysOnTop can **remember pinned applications across restarts, automatically re-pin reopened windows, manage multi-process application suites, handle elevated Administrator windows, and preserve relative window stacking order.**

**No installer. No third-party dependencies. No unnecessary background services.**

---

## ⬇️ Download

**[Download the latest version of AlwaysOnTop](https://github.com/LibbyLamarr/AlwaysOnTop/releases/latest)**

Download `AlwaysOnTop.exe` from the release assets and run it on Windows 10 or Windows 11.

- No installation required.
- No third-party dependencies.
- Requires .NET Framework 4.x.
- Administrator privileges are only needed when managing certain elevated applications.

> **Note:** Windows SmartScreen may display a warning because the executable is not digitally signed. The complete source code is available in this repository for inspection.

---

## 📸 Screenshots

AlwaysOnTop features a clean Windows interface with Light, Dark and System theme support.

### ☀️ Light Theme

![AlwaysOnTop Light Theme](screenshots/AlwaysOnTop-Light.png)

### 🌙 Dark Theme

![AlwaysOnTop Dark Theme](screenshots/AlwaysOnTop-Dark.png)

The **System** theme automatically follows your Windows Light/Dark appearance setting, including changes made while the utility is running.

---

## ✨ Key Features

- 📌 **Always on Top pinning** — Keep individual or multiple application windows above other windows.
- 🔄 **Persistent application pinning** — Automatically restore Always on Top status when pinned applications are reopened.
- 🎯 **Interactive window picker** — Select a visible window directly from your desktop using a crosshair.
- 🖥️ **Process-level management** — Target applications that don't appear in conventional window lists.
- 🛡️ **Administrator support** — Detect elevated processes and optionally restart the utility with Administrator privileges.
- 🧩 **Multi-process application support** — Manage related windows belonging to complex application suites.
- 🪟 **Intelligent Z-order handling** — Preserve the relative stacking order of multiple pinned windows.
- 🌗 **Light, Dark and System themes** — Choose your preferred appearance or follow Windows automatically.
- ⌨️ **Global keyboard shortcut** — Toggle Always on Top for the active window using `Ctrl + Alt + T`.
- 🚀 **Windows startup integration** — Optionally launch automatically and start minimised to the System Tray.
- 🪶 **Lightweight and portable** — Approximately 65 KB, written in C# using native Windows APIs.

---

## 🚀 Getting Started

### System Requirements

- Windows 10 or Windows 11
- .NET Framework 4.x
- Administrator privileges only when managing certain elevated applications

### Download and Run

1. Open the [latest release](https://github.com/LibbyLamarr/AlwaysOnTop/releases/latest).
2. Download `AlwaysOnTop.exe` from the Assets section.
3. Save the executable to a folder of your choice.
4. Double-click `AlwaysOnTop.exe` to launch the utility.
5. Select an application to pin or use the interactive window picker.

No traditional installation is required.

> **Note:** AlwaysOnTop runs in the System Tray. Closing the main window minimises the application rather than terminating it. Use **Quit Utility** to exit completely.

### Quick Pinning

The fastest way to pin a window is to bring it into focus and press:

**`Ctrl + Alt + T`**

Press the shortcut again to toggle its Always on Top status.

---

## 🖥️ Three Ways to Pin Applications

### 1. Running Applications

The **Running Applications** panel (top-left) displays currently visible standard application windows.

- Browse all detected application windows.
- Use the real-time search filter to find an application.
- Select one or more applications.
- Click **📌 Pin Selected App**, or double-click a row to pin immediately.

Ideal for everyday Windows applications.

### 2. Running Windows Processes

The **Running Windows Processes** panel (top-right) provides access to active Windows processes.

It displays:

- Process name
- Process ID (PID)
- Detected window status

A live search filter makes it easy to locate a particular process.

This method is useful for applications that use non-standard windows, borderless interfaces, tray-hosted components or multiple background processes.

### 3. Interactive Window Picker

The interactive picker lets you select a window directly from the desktop without needing to know its process name.

1. Click **🎯 Pick Window on Screen** in the application header.
2. Your mouse pointer changes to a crosshair (`+`).
3. Move the pointer over the window you want to pin.
4. A floating information banner displays the detected application, window title and PID.
5. Left-click the target window to pin it.

To cancel the picker, press `Esc`, right-click, or click an empty desktop or taskbar area.

---

## 📌 Persistent Application Pinning

AlwaysOnTop does more than temporarily apply a topmost flag.

### Automatic Re-Pinning

Pinned applications are remembered in the configuration file:

`AlwaysOnTop.settings.json`

If you close a pinned application and reopen it later, AlwaysOnTop automatically detects its new window and reapplies the Always on Top status.

This is particularly useful for applications that restart or recreate their windows during normal operation.

### Pinned Applications Manager

The **Always on Top Applications** panel displays your saved pinned applications.

You can:

- Double-click an entry to unpin it.
- Select entries and click **✕ Remove Selected from Always on Top**.
- Click **Remove All from Always on Top** to clear all pinned applications.

### Clean Exit

Selecting **Quit Utility** removes the Always on Top flag from currently managed windows before the utility exits.

This ensures that a normal shutdown does not intentionally leave applications stuck on top.

---

## 🛡️ Advanced Window Management

### Administrator and Elevated Application Support

Windows User Interface Privilege Isolation (UIPI) restricts certain interactions between applications running at different privilege levels.

This can prevent standard-user utilities from reliably managing windows belonging to elevated applications.

AlwaysOnTop includes:

- Detection of elevated target processes.
- A convenient **🛡️ Run as Administrator** option.
- Support for managing elevated application windows when running with appropriate privileges.

> **Security:** Administrator privileges are not required for normal operation. Only elevate AlwaysOnTop when necessary.

### Multi-Process Application Support

Some Windows applications use multiple processes to manage different parts of their interface. These processes may create separate windows, making conventional Always on Top utilities less reliable.

AlwaysOnTop includes process-family matching to identify related processes and manage their associated windows together.

This helps maintain consistent Always on Top behaviour for applications with complex, multi-process architectures, without requiring users to identify and pin every related process individually.

### Handling Stubborn Windows

Certain applications use custom window frameworks or non-standard window handling that doesn't always respond reliably to conventional topmost operations.

AlwaysOnTop uses native Win32 functionality, including:

- `SetWindowPos`
- `HWND_TOPMOST`
- `AttachThreadInput`

A thread-input attachment fallback provides additional handling for windows that require it.

### Intelligent Z-Order Preservation

When several applications are pinned simultaneously, conventional topmost utilities can sometimes cause background pinned windows to jump in front of the active window.

AlwaysOnTop addresses this by applying topmost status in back-to-front Z-order.

This helps maintain the relative stacking order of pinned applications, so the window you're actively using isn't unnecessarily obscured by another pinned window.

---

## 🎨 Themes and Appearance

AlwaysOnTop supports three appearance modes:

| Theme | Description |
|---|---|
| **System** | Automatically follows Windows Light/Dark mode |
| **Light** | Clean, light Windows interface |
| **Dark** | Dark slate interface with matching controls and title bar |

To change themes, right-click the System Tray icon and select **Theme**.

The System theme automatically responds to Windows appearance changes while the utility is running.

---

## ⚙️ System Tray and Startup Options

AlwaysOnTop is designed to operate quietly in the background.

### System Tray Integration

Closing the application window using `X` or **Minimize to Tray** keeps the utility running in the System Tray.

You can reopen the manager using the tray icon or exit completely using **Quit Utility**.

### Start Automatically with Windows

Enable **Start automatically on Windows start** to launch AlwaysOnTop when you sign in.

The utility uses the current-user Windows startup registry location:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

### Start Minimised

Enable **Start minimised to System Tray** to launch directly into the background without displaying the main manager window.

---

## ⌨️ Keyboard Shortcuts and Controls

| Action | Shortcut / Control |
|---|---|
| Toggle Always on Top for active window | `Ctrl + Alt + T` |
| Pin an application or process | Select + Pin, or double-click |
| Unpin a pinned application | Select + Remove, or double-click |
| Pick a window from the desktop | 🎯 Pick Window on Screen |
| Cancel window picker | `Esc`, right-click or click empty desktop |
| Change theme | Tray icon → Theme |
| Open the manager | Left-click or double-click tray icon |
| Quit completely | Quit Utility |

---

## 🛠️ Building from Source

AlwaysOnTop is written in C# using Windows Forms and native Win32 APIs through P/Invoke.

It can be compiled using the C# compiler supplied with the Windows .NET Framework installation.

**No Visual Studio installation or third-party NuGet packages are required.**

### 1. Clone the Repository

Open a Command Prompt and run:

```cmd
git clone https://github.com/LibbyLamarr/AlwaysOnTop.git
cd AlwaysOnTop
```

### 2. Build the Application

Run the included build script:

```cmd
build.bat
```

The script invokes the .NET Framework C# compiler:

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

### 3. Run the Executable

The build produces:

```text
AlwaysOnTop.exe
```

The resulting executable is approximately 65 KB and requires no additional third-party packages.

---

## 📁 Project Structure

```text
AlwaysOnTop/
├── screenshots/
│   ├── AlwaysOnTop-Light.png
│   └── AlwaysOnTop-Dark.png
├── AlwaysOnTop.cs
├── app.ico
├── build.bat
├── README.md
└── LICENSE
```

| File | Description |
|---|---|
| `AlwaysOnTop.cs` | Complete C# application source, WinForms UI, Win32 integration and window management engine |
| `app.ico` | Multi-resolution application and System Tray icon |
| `build.bat` | Build script using the .NET Framework C# compiler |
| `screenshots/` | Light and Dark theme application screenshots |
| `README.md` | Project documentation |
| `LICENSE` | MIT License |

The compiled `AlwaysOnTop.exe` is available from the repository's [Releases](https://github.com/LibbyLamarr/AlwaysOnTop/releases) page.

The utility also generates:

`AlwaysOnTop.settings.json`

This JSON file stores pinned application information, startup preferences and theme settings.

---

## 🔧 Technical Overview

AlwaysOnTop is implemented using C# Windows Forms and native Windows APIs.

Its primary components include:

- **Window discovery:** Enumerates visible application windows and running processes.
- **Window picker:** Uses mouse and keyboard hooks to identify windows directly from the desktop.
- **Topmost engine:** Applies and maintains window topmost status through Win32 APIs.
- **Persistent configuration:** Stores pinned applications and user preferences in JSON.
- **Process matching:** Identifies applications and related multi-process suites.
- **Z-order management:** Preserves relative stacking order between pinned windows.
- **System Tray integration:** Provides background operation and application controls.
- **Theme engine:** Supports Windows appearance integration and custom Light/Dark rendering.

The application is contained in a compact C# codebase, making it straightforward to inspect, modify and compile.

---

## ⚠️ Known Limitations

- **Windows only:** AlwaysOnTop uses Windows-specific APIs and is not compatible with macOS or Linux.
- **Elevated applications:** Some Administrator-level windows may require the utility to run with elevated privileges.
- **Protected windows:** Windows security restrictions may prevent interaction with certain protected or system-level windows.
- **Exclusive fullscreen applications:** Always on Top behaviour is not guaranteed for exclusive fullscreen games or applications.
- **Application-specific behaviour:** Some applications may override topmost settings or use unusual window management techniques.

AlwaysOnTop is designed for standard Windows desktop environments and cannot override every operating-system-level restriction.

---

## ℹ️ About This Project

AlwaysOnTop is a personal project, shared in case others find it useful. It has been developed with AI assistance, and I am continuing to test, refine and improve its features.

Suggestions, bug reports and contributions are welcome through [GitHub Issues](https://github.com/LibbyLamarr/AlwaysOnTop/issues).

---

## 📄 License

AlwaysOnTop is released under the **MIT License**.

See the [LICENSE](LICENSE) file for details.

---

## ☕ Support the Project

AlwaysOnTop started as a personal project to have an always on top utility and as an alternative to using Windows Power Toys, which I previously been using solely for this feature.

I've made it freely available in case it can be of use to others.

If you enjoy using AlwaysOnTop and would like to offer support, you're very welcome to buy me a coffee! It's entirely optional, but always appreciated.

<a href="https://buymeacoffee.com/libbylamarr">
  <img
    src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png"
    alt="Buy Me a Coffee"
    width="180">
</a>

Thank you for looking at AlwaysOnTop :)

---

**📌 AlwaysOnTop — Keep the windows that matter exactly where you need them.**
