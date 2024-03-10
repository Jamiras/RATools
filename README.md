# RATools
A script interpreter for writing achievements for retroachievements.org
Also contains some analysis tools for examining site data

### Build instructions (RATools only):
1) Run `git submodule init` and `git submodule update` to initialize the `Core` repository submodule.
2) Open the "RATools.sln" project in Visual Studio 2022 or higher.
3) Compile.

## Unit Tests
After opening the project, open the Test > Windows > Test Explorer tool window and press the Run All button. Individual tests (or groups of tests) can be run by right clicking on them and selecting Run Selected Tests.


## A Solid Snack's changes
- Currently made it available for the format_string(), rich_presence_display() and rich_presence_conditional_display() to accept an array of parameters besides the normal functionality
