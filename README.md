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
The following function can now accept an array of parameters

- [`format(format_string, parameters...)`](https://github.com/Jamiras/RATools/wiki/Rich-Presence-Functions#rich_presence_conditional_displaycondition-format_string-parameters "format function RATools documentation")
```
area_names = {
    1: "Downtown",
    2: "Roof",
    3: "Garden"
}
function getCurrentAreaNumber() {
    return byte(0xAF10)
}
stringToFormat = "Area {0}: {1} | Heading to Area {2}: {3}"

format(stringToFormat, [
    getCurrentAreaNumber(), 
    Area_names[getCurrentAreaNumber()], 
    getNextAreaNumber(), 
    stage_names[getNextAreaNumber()]
])
```
> Output example: `Area 1: Downtown | Heading to area 3: Garden`

- [`rich_presence_display(format_string, parameters...)`](https://github.com/Jamiras/RATools/wiki/Rich-Presence-Functions#rich_presence_conditional_displaycondition-format_string-parameters "rich_presence_display function RATools documentation")
```
stringToFormat = "Area {0}: {1} | Heading to area {2}: {3} | Time left: {4}"
displayParameters = [
    rich_presence_value("currentAreaNr", getCurrentStageNumber()),
    rich_presence_lookup("currentAreaName", getCurrentStageNumber(), stage_names),
    rich_presence_value("nextAreaNr", getNextAreaNumber()),
    rich_presence_lookup("nextAreaName", getNextAreaNumber(), stage_names),
    rich_presence_macro("Seconds", getTimeLeft()),
]

rich_presence_display(stringToFormat, displayParameters)
```
> Output in rich presence: `Stage 1: Downtown | Heading to 3: Garden`
- [`rich_presence_conditional_display(condition, format_string, parameters...)`](https://github.com/Jamiras/RATools/wiki/Rich-Presence-Functions#rich_presence_conditional_displaycondition-format_string-parameters "rich_presence_conditional_display function RATools documentation")
```
stringToFormat = "Area {0}: {1} | Heading to area {2}: {3} | Fuel left: {4}"
function isTravelling() => bit0(0xACF6) == 1
displayParameters = [
    rich_presence_value("currentAreaNr", getCurrentStageNumber()),
    rich_presence_lookup("currentAreaName", getCurrentStageNumber(), stage_names),
    rich_presence_value("nextAreaNr", getNextAreaNumber()),
    rich_presence_lookup("nextAreaName", getNextAreaNumber(), stage_names),
    rich_presence_macro("Fixed2", getCurrentFuel()),
]

rich_presence_conditional_display(isTravelling(), stringToFormat, displayParameters)
```
> Output in rich presence: `Stage 1: Downtown | Heading to 3: Garden | Fuel left: 02:21`
