Release notes for the welcome window's "What's new" section. Everything
above the first `## ` heading (this paragraph included) is discarded by
the parser, so it's safe as editor-facing instructions rather than content.

Add one `## x.y.z` section per release, newest first, with its heading
text matching `AppInfo.Version` (i.e. the `Version` value in
`Directory.Build.props`) exactly. A version with no matching section here
falls back to the welcome window's own placeholder text.

## 1.0.0

First release of Norn, an open source Valheim character editor that:

- Loads and displays Valheim characters.
- Allows editing a sensible set of values for a character.
- Can run on Windows or Linux desktop.
- Supports Valheim 1.0 Deep North.