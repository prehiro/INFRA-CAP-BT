# Third-party notices

## Nuxt UI theme presets and offline font catalog

The 12 reference preset documents and generic icon mappings are adapted from nuxt/ui docs/app/utils/theme (MIT). Original documents are retained in app/assets/theme/reference.json. Nuxt UI's full MIT notice is bundled at public/fonts/studio/licenses/nuxt-ui.txt. Custom device/header icons remain application-specific.

The 24 curated font families are vendored from Fontsource npm packages as local Latin/Latin-ext WOFF2 assets, with original family copyright/license texts in public/fonts/studio/licenses/*.txt. public/fonts/studio/manifest.json records exact package versions, source archives, faces and SHA-256 checksums. SIL OFL font licenses are retained per family. No runtime Google Fonts/CDN service is used for this catalog. scripts/vendor-theme-fonts.py reproduces downloads from the pinned manifest. Icon sets used by presets are bundled from the pinned @iconify-json dependencies; their upstream attribution is recorded in public/fonts/studio/licenses/icon-collections.json.


## Brand wordmark animation

Stroke-dash animation timing adapted from Lissy2019/fat-wasp-24 on Uiverse.
Source: https://uiverse.io/Lissy2019/fat-wasp-24
Archive: https://github.com/uiverse-io/galaxy/blob/main/loaders/Lissy2019_fat-wasp-24.html

MIT License

Copyright (c) 2023 Uiverse.io

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Register loading line

PC Ledger loading-line motion adapted from asgardOP/ancient-cow-78 on Uiverse.
Source: https://uiverse.io/asgardOP/ancient-cow-78
Archive: https://github.com/uiverse-io/galaxy/blob/main/loaders/asgardOP_ancient-cow-78.html
The Uiverse MIT license reproduced above also applies to this adaptation.

## Public Sans glyph artwork

AnimatedBrandText.vue contains outlines generated from the Public Sans 700 font already bundled with the application. The original font notice follows.

Copyright 2015 The Public Sans Project Authors (https://github.com/uswds/public-sans) PublicSans-Italic[wght].ttf: Copyright 2015 The Public Sans Project Authors (https://github.com/uswds/public-sans)

This Font Software is licensed under the SIL Open Font License, Version 1.1.
This license is copied below, and is also available with a FAQ at:
http://scripts.sil.org/OFL


-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded,
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the
copyright statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

"Author" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.

## Sign In glyph

The Sign In button glyph is Sargam Icons "sign-in-line" (MIT, Abhimanyu Rana, github.com/planetabhi/sargam-icons), inlined as SVG so the offline build carries it without an icon dependency.

## Add Record glyph

ChannelAddIcon.vue inlines the Fluent UI System Icons glyph "channel-add-24-regular" (MIT, Microsoft, github.com/microsoft/fluentui-system-icons). It is embedded as SVG so the offline build carries it without an icon dependency.

SaveIcon.vue embeds the Codicon "save" glyph (CC BY 4.0, Microsoft, github.com/microsoft/vscode-codicons), exposed by Iconify as codicon:save. It is embedded as SVG so the offline build carries it without an icon dependency.
