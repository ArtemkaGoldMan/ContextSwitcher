# Working on ContextSwitcher

Found a bug or have an idea, but don't want to touch the code? [Write to me](README.md#contact) —
an email or an issue is just as welcome as a pull request.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/ArtemkaGoldMan/ContextSwitcher.git
cd ContextSwitcher
dotnet run --project src/ContextSwitcher.App/ContextSwitcher.App.csproj --framework net10.0
```

The app builds for Windows as well, which is not ready to run yet; `net10.0` is the Mac app.

Run this way, it's a **development build**: it keeps its settings in `~/.config/ContextSwitcher-dev`,
apart from an installed copy's `~/.config/ContextSwitcher`, and says *DEV* in its name — so you can
work on it and use the installed app at the same time without either touching the other's profiles
or time. `CONTEXTSWITCHER_CONFIG_DIR` points either at another folder.

## Before a pull request

[How it is built](docs/architecture.md) has the design rules and the contracts the code keeps —
read it before changing how the app behaves, and go through the questions at the end of its
[design rules](docs/architecture.md#design-rules). [CLAUDE.md](CLAUDE.md) is the short version
that coding agents load. Then check what CI will:

```bash
dotnet build ContextSwitcher.sln -c Release --no-incremental -warnaserror
```

```bash
dotnet test
```

The build has no warnings, and every test passes. The pull request template asks how the change was
checked, and whether the release notes, the docs or the README need a line.

## Docs

| | |
| --- | --- |
| [How it is built](docs/architecture.md) | The projects, the design rules, the files, how a switch runs and what each step does |
| [Roadmap](docs/roadmap.md) | What comes next, what later, and what is not planned |
| [Release process](docs/release-process.md) | Building the `.app`, signing, publishing, how updates work, and redrawing the screenshots |
| [Translating](docs/translating.md) | How the app's text is organised, and adding a language |
| [Performance](docs/performance.md) | What the app costs in memory and CPU, measured, and what did not help |
| [Browser control](docs/browser-control.md) | What another app can do to Chrome and Safari: tabs, tab groups and profiles |
| [Usage statistics](docs/usage-statistics.md) | The downloads and visitors collected every night, and the plan for counting people |
| [A Windows version](docs/windows-port.md) | What each feature becomes on Windows, what Windows can't do and why - tried on Windows 11 |
| [The Windows plan](docs/windows-plan.md) | The order to build it in - apps first, then the browser, then the rest - and where it stands |
