// Helps scripts/check-menu-bar-icon.sh look at the menu bar, which has no command of its own.
//
//   menu-bar-items count            how many menu bar items are on screen now
//   menu-bar-items show <seconds>   puts a test item in the menu bar for that long
//
// A menu bar item is a window at the status level (25): the app's own on macOS 15, one Control Center
// draws for it on macOS 26. A closed app's item can stay behind off screen, so only the ones on screen
// count. Reading window levels and bounds needs no permission.
import AppKit

let arguments = CommandLine.arguments.dropFirst()

switch arguments.first {
case "count":
    let windows = CGWindowListCopyWindowInfo([.optionAll], kCGNullWindowID) as? [[String: Any]] ?? []
    let items = windows.filter {
        ($0[kCGWindowLayer as String] as? Int) == 25 && ($0[kCGWindowIsOnscreen as String] as? Bool) == true
    }
    print(items.count)

case "show":
    let seconds = Double(arguments.dropFirst().first ?? "") ?? 20
    let app = NSApplication.shared
    app.setActivationPolicy(.accessory)
    let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    item.button?.title = "test"
    DispatchQueue.main.asyncAfter(deadline: .now() + seconds) { exit(0) }
    app.run()

default:
    FileHandle.standardError.write("usage: menu-bar-items count | show <seconds>\n".data(using: .utf8)!)
    exit(2)
}
