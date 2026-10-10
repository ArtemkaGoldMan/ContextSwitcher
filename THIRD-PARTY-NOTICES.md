# Third-party notices

The prebuilt app bundles the libraries below, each under its own license. Their source and full
license texts are at the links.

| Library | Used for | License |
| --- | --- | --- |
| [.NET runtime](https://github.com/dotnet/runtime) | Runs the app; bundled so nothing else needs installing | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | The user interface | [MIT](https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md) |
| [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) | The look of the standard controls | [MIT](https://github.com/irihitech/Semi.Avalonia/blob/main/LICENSE) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | Drawing, through Google's Skia | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md); Skia itself [BSD-3-Clause](https://github.com/google/skia/blob/main/LICENSE) |
| [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | Text shaping, through HarfBuzz | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md); HarfBuzz itself [Old MIT](https://github.com/harfbuzz/harfbuzz/blob/main/COPYING) |
| [MicroCom](https://github.com/kekekeks/MicroCom) | Avalonia's bridge to macOS | [MIT](https://github.com/kekekeks/MicroCom/blob/master/LICENSE) |
| [Tmds.DBus](https://github.com/tmds/Tmds.DBus) | Part of Avalonia; unused on macOS | [MIT](https://github.com/tmds/Tmds.DBus/blob/main/COPYING) |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | Wiring the app together | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |

## Lucide

Icon geometry in `src/ContextSwitcher.App/Styles/Icons.axaml` is derived from
[Lucide](https://lucide.dev) (v1.35.0), used under the ISC license. Each icon's path data is
transcribed from the corresponding Lucide SVG; shapes originally expressed as `<rect>` or
`<circle>` are written as the equivalent path so that one icon is one geometry.

```
ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT).
All other copyright (c) for Lucide are held by Lucide Contributors 2022.

Permission to use, copy, modify, and/or distribute this software for any purpose with or without
fee is hereby granted, provided that the above copyright notice and this permission notice appear
in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS
SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT,
NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE
OF THIS SOFTWARE.
```
