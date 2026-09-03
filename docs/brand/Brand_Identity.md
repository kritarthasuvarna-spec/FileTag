# Footnote — Brand Identity Reference

**Status: FINAL — locked.** Production assets generated and delivered in `footnote_brand_assets/` (SVG sources + PNG exports at all standard sizes, both wordmark color variants, brand sheet).

Supersedes earlier "tag-badge" icon direction from the original FileTag brand sheet — that direction is retired along with the old name. Also supersedes several exploratory logo rounds (ink-blot asterisk, typographic dagger, pen nib, literal file+note icons, abstract Arc/Notion-register marks) — full history preserved in conversation for reference, but Bleeding Drop is the final decision.

---

## 1. Naming & concept

**Footnote.** A footnote is a short note attached to something else — the literal function of the product. Chosen after testing 12+ alternatives (Footnote, Annotafile, AttachNote, PinFile, and others) for trademark collision; Footnote came back clean with no dedicated software using the name.

Tagline: **"A Footnote for Every File."**

---

## 2. Logo mark — Bleeding Drop

A single round ink drop with a soft directional bleed trailing beneath it, like ink that's just landed and is still spreading. Final choice after exploring several other directions (an ink-blot asterisk, a superscript-styled drop, a typographic dagger, a pen nib, literal file+note icons, and several fully abstract marks in the Arc/Notion register). Selected because it's the simplest shape tested across every round — holds up cleanly at 16px tray-icon size with no simplified fallback version needed, while still being distinctive enough to anchor the brand and directly tied to the "Ink" brand color and the Ink Bleed UI motif used throughout the product.

**Construction:**
- Primary circle: solid fill, brand Ink blue
- Trailing bleed: soft blurred ellipse beneath/behind the circle, ~25–30% opacity, same hue
- No hard edges anywhere in the mark — everything soft/organic, consistent with the Ink Bleed UI motif
- Single asset works at all sizes — no two-tier export needed (unlike the retired ink-blot-asterisk direction)

**Sizes to export:** 16px, 32px, 48px, 128px, 256px, 512px (standard Windows icon set + Store listing sizes).

---

## 3. Wordmark

Split-type lockup, lowercase: **"foot"** in bold sans (Inter, weight 800) + **"note"** in serif italic (Source Serif 4, weight 500 italic). Lowercase chosen over title case in a later revision — reads slightly quieter and more contemporary (in line with how Arc, Notion, and Linear style their own names), while keeping the one detail that matters: the serif-italic half is the same typographic choice used for actual comment text inside the app, so the wordmark previews the brand's core idea (a note rendered differently from everything around it).

Icon sits to the left of the wordmark in the primary horizontal lockup. Same single mark used everywhere — tray, favicon, Store icon, wordmark lockup — since it doesn't require a separate small-size variant.

---

## 4. Color palette

| Name | Hex | Role |
|---|---|---|
| **Ink** | `#4F8EF7` | Primary brand color, default logo/mark color, default tag preset |
| **Moss** | `#5FB88A` | Secondary tag preset — "approved / good" |
| **Ember** | `#E0714E` | Tertiary tag preset — "needs attention" |

Same three colors serve double duty as both brand palette and the in-app note tag-color presets (see UI spec doc) — no separate marketing vs. product palette.

Neutral/dark base for all product surfaces: `#202024` card, `#1A1A1C` app background, `#2C2C30` borders — matches the existing dark Explorer-adjacent theme rather than the originally-planned light Details Pane theme (superseded; product is dark-native going forward).

---

## 5. Typography

| Use | Typeface | Weight/style |
|---|---|---|
| Wordmark — "Foot" | Inter | 800 |
| Wordmark — "note" | Source Serif 4 | 500 italic |
| In-app comment/note text | Source Serif 4 | 400 italic |
| All other UI text (filenames, timestamps, buttons, chrome) | Inter (or Segoe UI on Windows-native surfaces) | 400–600 |

Rule of thumb: **serif italic = the user's own words, always.** Nothing else in the product ever uses serif. This is the single consistent signal tying wordmark, UI, and brand together.

---

## 6. Motif: Ink Bleed

Not just a logo treatment — the core visual language of the product. Tag colors render as soft blurred blooms rather than flat swatches, edge bars, or dots, both in the logo mark and in the in-app comment cards. See `Footnote_InkBleed_UISpec.md` for exact implementation values (blur radii, opacity, layering).

---

## 7. What NOT to do

- No tag/label/pin iconography (belongs to the retired FileTag identity)
- No sticky-note/Post-it skeuomorphism (tested and deliberately moved away from — see palette exploration history)
- No hard-edged color blocks or flat accent bars — everything brand-colored should look like ink, not paint
- No light-mode assumption anywhere in product UI — dark-native is now the baseline, not a "V2 dark mode" add-on

---

## 8. Open items

- [ ] Final SVG/PNG export of the Bleeding Drop mark at all required sizes (single asset, no size-variant needed)
- [ ] GitHub social preview, Store listing assets, README wordmark cutout — need regenerating under this identity (old assets were built for the tag-badge/FileTag direction and are no longer valid)
- [ ] Icon-only tray version needs testing against actual Windows 11 taskbar (light and dark taskbar themes) before finalizing
