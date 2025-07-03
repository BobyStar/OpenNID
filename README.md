# Install
Install the VPM package <b>[here](https://bobystar.github.io/OpenNID/)</b> or via the Unity Package in [Releases](https://github.com/BobyStar/OpenNID/releases/latest).<br>
Open the main window in the Unity Editor Menu: `Tools/Open NID`.
# Open NID
An open source utility tool to easily manage Network IDs for VRChat worlds.<br>
![A screenshot showing the main Open NID window.](Promo/Images/OpenNID_Screenshot_MainWindow.png)
# Features
## Automatic Conflict Resolution
Open NID provides simple auto fixes for tedious actions like reapplying scene components to an existing network object and 
clearing missing network objects from the collection. This is helpful when needing to do repeated builds with scene changes for testing.<br>
![A screenshot showing an example dialogue for Auto Resolving ID Conflicts.](Promo/Images/OpenNID_Screenshot_AutoResolveConflicts.png)
## Automatic Build Check
Open NID hooks into the VRChat SDK Build API and allows the user to fix issues before the Network ID Verification process.<br>
![A screenshot showing an example dialogue upon attempting a new build with Network ID Conflicts.](Promo/Images/OpenNID_Screenshot_VRCSDKBuildCheck.png)
## Import/Export Tool
Open NID provides an intuitive export and import tool for transferring Network ID information between scenes and projects.<br>
![A screenshot showing the Open NID import window.](Promo/Images/OpenNID_Screenshot_ImportWindow.png)
## Player Object Enabled Persistence Aware
Open NID checks for network behaviours related to VRC Player Objects w/VRC Enable Persistence components to allow operations like Clear and Regenerate Network IDs to exclude persistence enabled Network IDs.<br>
![A screenshot showing a dialogue for clearing the current scene Network IDs with the option to exclude persistent objects.](Promo/Images/OpenNID_Screenshot_ClearPersistentNetworkIDs.png)
![A screenshot showing a dialogue for regenerating the current scene Network IDs with the option to exclude persistent objects.](Promo/Images/OpenNID_Screenshot_RegeneratePersistentNetworkIDs.png)
## Undo/Redo Support
Most actions done with Open NID support Unity's Undo system and Prefab instance modifications.
# Support
Found a bug? Report it [here](https://github.com/BobyStar/OpenNID/issues)!<br>
Want to support the tool? Share it with others and make a contribution to the codebase!
