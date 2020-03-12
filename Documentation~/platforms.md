# Supported Platforms

Havok Physics for Unity is currently supported on the following platforms:

* Windows
* Mac
* Linux
* iOS
* Android
* Xbox One - please speak to your Unity account manager
* Playstation 4 - please speak to your Unity account manager

Support for the following platforms is coming soon:

* Nintendo Switch

 > **Note:** Once supported, these will still require that you download additional files from the platform holder websites, since they cannot be included in the package itself due to NDA restrictions.


# Platform-specific instructions

Android:
* It's currently not supported to build both ARMv7 and ARM64 at the same time. Please initiate separate builds if you need app packages for both.
* When building ARMv7, "HK_ANDROID_32" define needs to be added to "Scripting Define Symbols" field in Other Settings/Configuration section of Player options.
* Note: ARM64 is supported out of the box, no need to put any custom defines (please make sure "HK_ANDROID_32" define is NOT present in "Scripting Define Symbols").
