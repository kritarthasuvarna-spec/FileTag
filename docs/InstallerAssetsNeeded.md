# What FileTag Setup needs from you

Everything else from the professionalism audit (button order, path validation,
rollback, progress checklist, screen transitions, disk-space check, exe
metadata fields) is a code change — I'll handle those directly, no input
needed. This doc covers only the things that have to come from you: assets,
decisions, and purchases.

---

## 1. App icon / logo (recommended)

The current `filetag.ico` is a placeholder I generated with a script early on
— a flat polygon, not a designed mark. It's used in the tray, the taskbar,
Explorer, and (currently as an emoji stand-in, not the real icon — a bug I'll
fix either way) the Setup welcome screen. This is the single highest-impact
visual asset in the whole product.

**Deliverables:**

| File | Spec |
|---|---|
| `filetag.ico` | Multi-resolution Windows icon: 16×16, 32×32, 48×48, 256×256, all in one `.ico` file |
| `filetag-logo.png` | Same mark, standalone, 512×512, transparent background — for the Setup welcome screen and any future marketing use |

**Design guidance:**
- Must read clearly at **16×16** (tray icon size) — test it small, not just large. Simple beats detailed here.
- Current palette: accent blue `#4F8EF7` on dark `#1E1E2B`/`#28283A` backgrounds. Doesn't have to match exactly, but should sit comfortably next to it.
- Flat or subtly-shaded is fine — no need for photorealism or gradients on a utility-app icon.

**Where it plugs in once you hand it over:** `FileTag.App/filetag.ico`, `FileTag.Setup/filetag.ico` (currently linked from the same file) — I swap the file, everything downstream (tray, taskbar, Explorer, installer) picks it up automatically.

---

## 2. Branding text (required — takes 2 minutes)

These strings go into the exe's file properties (Explorer → right-click →
Properties → Details), the Apps & Features listing, and the installer's
copyright line. Right now they're either blank or generic placeholders.

Give me:

- **Publisher / company name** — the exact string to show in Apps & Features "Publisher" and exe "Company". Can be your own name if there's no company entity.
- **Copyright line** — e.g. `© 2026 [Your Name]`
- **One-line description** — shown as the exe's "File description" in Explorer. Something like *"Attach personal notes to any file"* works, or write your own.
- **Website / contact URL** — shown as "Visit website" in Apps & Features. The GitHub repo (`https://github.com/kritarthasuvarna-spec/FileTag`) works fine if you don't have a separate site.

---

## 3. Code signing (optional, costs money — the one that actually removes the SmartScreen warning)

This is the single biggest lever on "looks legitimate," and the only one on
this list that isn't free. Your call whether it's worth it for a free
personal utility.

**What it costs:** roughly $100–400/year for an OV (Organization Validation)
certificate from a CA like DigiCert, Sectigo, SSL.com, or Certum (Certum is
usually the cheapest legitimate option for indie/open-source devs).

**What's actually involved** (worth knowing before you buy):
- You'll go through an identity-verification process with the CA — can take a few days, sometimes requires a phone call or notarized documents depending on the CA and whether you're signing as an individual or a company.
- As of 2023, CA/Browser Forum rules require the private key live on a **hardware token or cloud HSM** — you get a physical USB key (or cloud signing service access), not a file you can hand to me. This means the actual `signtool.exe` signing step has to run on **your machine** with the token plugged in (or via a cloud signing API you configure).
- **What I need from you once you have it:** nothing to hand over — I'll write the exact `signtool` command and wire it into `build.ps1` as a signing step, and you run the build/sign on your end (or tell me the cloud-HSM API details if the CA offers one, and I can call it from the script instead).

**Decision needed from you:** whether to pursue this at all, and if so, which CA/tier — I can help you compare options once you decide you want it.

---

## 4. Distribution size trade-off (a decision, not a purchase)

Current installer is ~140MB because it's self-contained (bundles the whole
.NET runtime — works on a bare Windows machine with nothing pre-installed).
A framework-dependent build would shrink this to a few MB, but then Setup
needs to detect and prompt-install the .NET 8 Desktop Runtime if it's
missing — real additional engineering, not a config flag.

**Decision needed:** keep it self-contained and heavy-but-zero-dependency
(current approach, no work needed), or should I build the runtime-detection
flow to shrink it? Worth knowing this is a multi-day feature, not a tweak,
before you ask for it.

---

## Not needed, just FYI

Everything in the original audit *not* listed above — button order, real
progress checklist, path validation before install, rollback on partial
failure, screen-transition animation, disk-space precheck, filling in the
missing Apps & Features registry fields — needs nothing from you. Say the
word and I'll build those next.
