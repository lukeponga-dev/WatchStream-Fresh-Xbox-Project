# Windows PC + Xbox One: concrete first steps

This guide is for developing on a Windows PC and testing on one Xbox One console.

## 1. Prepare the Windows PC

1. Install a current Visual Studio release from https://visualstudio.microsoft.com/downloads/.
2. In Visual Studio Installer, check whether **Universal Windows Platform development** and the Windows 10 SDK components needed by the project are available. Install the UWP workload/SDK if offered.
3. Open `src/WatchStream.Xbox/WatchStream.Xbox.csproj` in Visual Studio.
4. If Visual Studio reports that the UWP project type or target SDK is unavailable, do not convert it to WinUI 3 automatically. First check the installed UWP tooling and the target SDK listed in the project file.
5. Select **x64** and **Debug**. Build the project. Resolve build/tooling errors before adding network or media code.

The starter project currently targets UWP SDK 10.0.19041.0 and minimum version 10.0.17763.0. These are initial compatibility settings, not a guarantee that your machine has those SDKs installed or that the package has been validated on hardware.

## 2. Prepare Xbox One Developer Mode

1. Back up/sync anything important and read Microsoft's current Developer Mode guidance.
2. Install and launch the **Xbox Dev Mode Activation** app on the console, then follow its instructions.
3. Switch into Developer Mode and open Dev Home.
4. In Dev Home, enable the console's Device Portal/network access as available in your Dev Home version. Record the console's local IP address.
5. Keep the PC and Xbox on the same trusted home network. Do not expose Device Portal to the public internet.

Developer Mode changes the console into a development environment; use Dev Home's option to return to Retail Mode when you want to play normally.

Official guidance:
- https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/frequently-asked-questions
- https://learn.microsoft.com/en-us/windows/uwp/apps-for-xbox/development-options
- https://learn.microsoft.com/en-us/legal/windows/agreements/xbox-one-developer-mode-activation

## 3. Connect Visual Studio to Xbox One

1. Ensure the Xbox is in Developer Mode and a user is signed in.
2. In Visual Studio, set the deployment target to **Remote Machine** and enter the console's local IP address.
3. Pair/authorize the connection using the authentication options presented by your installed tooling.
4. Deploy the app to the console. If deployment fails, first verify Developer Mode, network reachability, and SDK/tooling versions.
5. Use a trusted private network. Microsoft's legacy UWP remote deployment instructions warn that Universal (Unencrypted Protocol) is vulnerable on untrusted networks.

## 4. Acceptance checklist

- [ ] Project builds in Debug x64.
- [ ] Package is created without manifest errors.
- [ ] App deploys and launches on Xbox One.
- [ ] All interactive controls are reachable with a controller.
- [ ] Button and dialog respond to controller A/B.
- [ ] Back navigation behaves correctly.
- [ ] Suspend/resume does not crash.
- [ ] App reports its current foundation status accurately.
- [ ] No credentials or fake Stremio account details are present.

## 5. What comes next

Once the shell launches on Xbox One, record the Visual Studio version, installed Windows SDK, console OS version, build output, and any deployment errors. Then add the smallest network contract test against a documented Stremio endpoint. Do not implement speculative QR login or persist passwords.
