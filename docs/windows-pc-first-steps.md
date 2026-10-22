# Windows PC + Xbox One: manual Visual Studio setup

This guide is for developing on a Windows PC and testing on one Xbox One console. The generated app shell and solution were intentionally removed so the project can be created by Visual Studio itself.

## 1. Create the app project in Visual Studio 2026

1. Open the repository folder in File Explorer, then open Visual Studio 2026.
2. Select **Create a new project**.
3. Search for **UWP Blank App** and choose the C# XAML template. Do not choose a WinUI 3 desktop app.
4. Name the project `WatchStream.Xbox`.
5. Set the project location to the repository's `src` folder, so the project is created at `src/WatchStream.Xbox`.
6. If Visual Studio asks whether to create a solution, create one named `WatchStream` in the repository root. If it offers a choice between a modern .NET UWP template and a legacy .NET Native UWP template, note which one you selected; do not assume either template is compatible with Xbox One until its target platforms and deployment options are verified.
7. In Configuration Manager, check whether **x64** is available. Do not add media or network functionality yet.
8. Build the untouched template first. Save the exact template name, target/minimum versions, build output and any errors.

**Important:** the fact that a UWP template builds on the PC does not prove it can be deployed to Xbox One. Confirm that Xbox is an available/valid target for the chosen project model before investing in implementation.

## 2. Keep the existing foundation

The repository's `src/WatchStream.Core`, `tests/WatchStream.Core.Tests`, and existing documentation are the project foundation. Keep these files. After the new app template builds, add the existing core project to the solution only if its target framework is compatible with the app project. Do not overwrite the core contracts.

## 3. Prepare Xbox One Developer Mode

1. Read Microsoft's current Developer Mode guidance before changing the console.
2. On the console, install and launch the **Xbox Dev Mode Activation** app if available to your account, then follow its instructions.
3. Switch into Developer Mode and open Dev Home.
4. Enable Device Portal/network access as available in your Dev Home version and record the console's local IP address.
5. Keep the PC and Xbox on the same trusted home network. Do not expose Device Portal to the public internet.

Official guidance:
- https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/frequently-asked-questions
- https://learn.microsoft.com/en-us/windows/uwp/apps-for-xbox/development-options
- https://learn.microsoft.com/en-us/legal/windows/agreements/xbox-one-developer-mode-activation

## 4. Acceptance checklist

- [ ] A project created by Visual Studio loads without a missing-project-type error.
- [ ] The untouched template builds successfully.
- [ ] The chosen project model explicitly supports the intended Xbox One deployment target.
- [ ] The existing core contracts, tests and documentation remain intact.
- [ ] No credentials or fake Stremio account details are present.

## 5. What comes next

Once the project loads and builds, record the Visual Studio version, selected UWP template, Windows SDK, target/minimum versions, and build output. Verify Xbox One deployment compatibility before adding networking or media playback.
