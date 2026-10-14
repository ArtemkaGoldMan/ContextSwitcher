# What the app costs

ContextSwitcher sits in the menu bar all day, so it should cost almost nothing while nothing
happens. These are measurements of a Release build on an Apple silicon Mac, taken with
[`scripts/measure-memory.sh`](../scripts/measure-memory.sh). The number is the *footprint* - what
Activity Monitor's Memory column shows - not resident size, which also counts the shared system
libraries every Mac app maps.

| State | Footprint |
| --- | --- |
| Menu bar only, no window ever opened | about 73 MB |
| After the first update check (30 s after launch, then daily) | about 78 MB |
| Dashboard opened once | about 111 MB (125 MB before the GPU cache cap) |
| Dashboard opened once, `CONTEXTSWITCHER_RENDERING=software` | about 97 MB |

Idle CPU is zero: a sample of a running copy shows every thread blocked, and the process wakes a
handful of times a second, all of it the .NET runtime's own housekeeping. Startup costs about half
a second of CPU, nearly all of it compiling Avalonia's code the first time it runs.

## Where the 73 MB goes

Measured by stopping the app at each stage of startup:

| Stage | Footprint |
| --- | --- |
| .NET runtime, at the first line of `Main` | 23 MB |
| After the app's own services and settings are loaded | 35 MB |
| After Avalonia, the Semi theme, Skia and the menu bar icon | 73 MB |

The managed heap itself is about 5 MB. The rest is the runtime's code and loader memory and
Avalonia's native side, which is why the garbage collector's settings change nothing.

## What a window costs

A window costs memory that stays after it is hidden *or closed*, because Skia keeps GPU textures
cached and the compiled layout code stays compiled. So the popover is hidden rather than closed:
it reopens instantly, and closing it would have returned nothing.

Two things keep that down:

- Skia's GPU cache is capped at 2 MB (`Program.GpuCacheBytes`). Its default left 28 MB behind after
  one look at the dashboard; now 15 MB.
- The popover's elapsed-time clock runs only while the popover is showing, and ticks once a minute
  after the first minute rather than every second. Hidden, it used to wake the app about twenty
  times a second.

### Software rendering

`CONTEXTSWITCHER_RENDERING=software` draws on the CPU, which leaves no GPU memory behind: the
dashboard costs about 14 MB less again. It is not the default because how it looks on a real
screen has not been judged - run the app with it set and look at the dashboard, the Profiles page
and the shadows. A large window costs about the same either way, since a CPU framebuffer grows
with the window's pixels.

## Tried, and not worth it

Each of these was measured on the same build:

- **The garbage collector** - non-concurrent, a small gen 0, `GCConserveMemory`, forcing a full
  collection and handing malloc's free pages back: all within 1 MB of nothing, since the managed
  heap is 5 MB and no collection has even run by then.
- **Tiered compilation and W^X settings:** no change in footprint.
- **ReadyToRun** (precompiling to native code) halves the code compiled at startup - 5900 methods to
  1700 - and 350 ms of it, but the app is 8 MB *larger* afterwards and the total CPU is no
  lower, because the precompiled images are mapped as private memory on macOS.
- **Removing the update check's cost** (about 5 MB, the first HTTPS request loading the TLS stack)
  would mean checking through `curl`; not done.
