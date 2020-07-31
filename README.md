# RATools
A script interpreter for writing achievements for retroachievements.org
Also contains some analysis tools for examining site data

### Build instructions (RATools only):
This is the setup most users should be using.
1) Run `git submodule init` and `git submodule update` to initialize the `Core` repository submodule.
2) Download the [nUnit and Moq dlls](https://github.com/Jamiras/Core/wiki/files/nUnit.3.11.zip) and extract them to a "lib" subdirectory under the `RATools` checkout.
3) Open the "RATools.sln" project.
4) Compile.

### Build instructions (RATools + Core):
This is an advanced setup for users who want to share the Core and/or libs with other projects.
1) Clone the [Core](https://github.com/Jamiras/Core) repository in a directory beside the `RATools` checkout.
2) Download the [nUnit and Moq dlls](https://github.com/Jamiras/Core/wiki/files/nUnit.3.11.zip) and extract them to a "libs" directory beside the RATools checkout.
    * you should now have three directories at the top level: Core, libs, and RATools
3) Open the "RATools + Core.sln" project.
4) Compile.

## Unit Tests
To run the unit tests (using either configuration above), you need to install the "NUnit 3 Test Adapter" from the Online Marketplace within the Tools > Extensions and Updates dialog. You'll need to close all instances of Visual Studio for it to install.

Then open the Test > Windows > Test Explorer tool window and press the Run All button. Individual tests (or groups of tests) can be run by right clicking on them and selecting Run Selected Tests.
