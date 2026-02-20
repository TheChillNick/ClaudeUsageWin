# Contributing to Claude Usage Win

Thank you for your interest in contributing. This guide will help you get started.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows 10/11
- An IDE such as Visual Studio 2022, VS Code with the C# extension, or JetBrains Rider

## Getting Started

1. Fork and clone the repository:

   ```bash
   git clone https://github.com/TheChillNick/ClaudeUsageWin.git
   cd ClaudeUsageWin
   ```

2. Restore dependencies:

   ```bash
   dotnet restore
   ```

3. Run in debug mode:

   ```bash
   dotnet run
   ```

4. Build a release executable:

   ```bash
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
   ```

## Project Structure

| Path | Description |
|---|---|
| `App.xaml.cs` | Application entry point, tray icon setup, global event handling |
| `MainWindow.xaml.cs` | Usage popup UI and data-binding logic |
| `SettingsWindow.xaml.cs` | Settings dialog for session key, thresholds, and preferences |
| `Models/UsageData.cs` | Data models for the Claude API response |
| `Services/ClaudeApiClient.cs` | HTTP client that calls the Claude usage API |
| `Services/ConfigService.cs` | Reads and writes persistent settings to `%AppData%` |
| `Services/CredentialsReader.cs` | Reads Claude Code credentials from `~/.claude/.credentials.json` |
| `Services/Logger.cs` | Debug logging to a local log file |
| `Services/StartupService.cs` | Registers/unregisters the app for Windows startup |
| `Services/ThresholdNotifier.cs` | Fires tray notifications when usage crosses thresholds |

## Pull Request Guidelines

- Create a feature branch from `main` (`git checkout -b feature/my-change`).
- Keep changes focused. One feature or fix per PR.
- Test your changes locally before submitting.
- Write clear commit messages describing what changed and why.
- Make sure the project builds without warnings (`dotnet build`).
- Update documentation if your change affects user-facing behavior.

## Reporting Issues

Open a GitHub issue with:

- A clear description of the problem or suggestion.
- Steps to reproduce (if applicable).
- Your Windows version and .NET SDK version.
