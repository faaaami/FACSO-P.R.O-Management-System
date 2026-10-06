# FASCO PRO Service — Frontend Design System

> **Visual Source of Truth**  
> This document defines the permanent visual and design standards for the FASCO PRO Service frontend. All UI development, modifications, components, and layouts must adhere strictly to the values and rules set forth here unless explicitly instructed otherwise.

---

## 1. Brand Identity

- **Product Name**: FASCO PRO Service
- **Product Domain**: UAE Government Relations (PRO), Corporate Document Clearing, Visa & Commercial License Management

### Core Color Palette

| Token Name | Hex / RGBA | Usage |
| :--- | :--- | :--- |
| **Primary** | `#1C1F26` | Main brand elements, primary CTA buttons, dark headings |
| **Primary Hover** | `#101319` | Hover state for primary buttons and interactive dark elements |
| **Accent / Emerald** | `#0F9D74` | Active navigation, primary status accents, verified indicators |
| **Accent Hover** | `#0B7D5D` | Hover state for accent buttons and links |
| **Accent Tint** | `rgba(15, 157, 116, 0.08)` | Active navigation background, subtle highlight pills, selected item backdrop |
| **Tertiary** | `#251E13` | Specialized deep warm neutral accents |

---

## 2. Surfaces & Backgrounds

| Surface | Value | Tailwind / CSS Specification |
| :--- | :--- | :--- |
| **App Background** | `#F7F8FA` | `bg-[#F7F8FA]` — Full screen app canvas & main content background |
| **Card / Container** | `#FFFFFF` | `bg-white` — Dashboard widgets, modal panels, table wrappers, cards |
| **Border / Divider** | `#E2E4E9` | `border-[#E2E4E9]` — 1px solid dividers, card borders, table line borders |

---

## 3. Typography

- **Primary Font**: `Inter`, system-ui, -apple-system, sans-serif
- **Font Weights**:
  - `400` — Regular (Body text, descriptions, table cell text)
  - `500` — Medium (Form labels, secondary labels, navigation links)
  - `600` — Semi-Bold (Card headings, button text, subheadings, key metadata)
  - `700` — Bold (Page titles, primary stat values, modal titles)

### Text Color Hierarchy

| Token Name | Value | Tailwind / CSS Specification | Description |
| :--- | :--- | :--- | :--- |
| **Text Primary** | `#16181D` | `text-[#16181D]` | High-contrast body, primary titles, and table headers |
| **Text Secondary** | `#6B7280` | `text-[#6B7280]` | Helper text, breadcrumbs, timestamps, captions |
| **Text Placeholder**| `#9CA3AF` | `text-[#9CA3AF]` | Unfilled input placeholders, inactive icons |

---

## 4. Status Colors & Indicators

Used for document lifecycle tracking (visas, commercial licenses, labor cards), renewal tasks, and request states:

| State / Meaning | Hex Code | Background Tint / Pill Style |
| :--- | :--- | :--- |
| **Success / Valid / Completed** | `#0F9D74` | `bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20` |
| **Warning / Due Soon / Pending** | `#D97706` | `bg-amber-50 text-[#D97706] border border-[#D97706]/20` |
| **Danger / Expired / Overdue / Blocked** | `#DC2626` | `bg-red-50 text-[#DC2626] border border-[#DC2626]/20` |
| **Neutral / Inactive / Draft** | `#9CA3AF` | `bg-gray-100 text-[#6B7280] border border-[#E2E4E9]` |

---

## 5. Shape & Corner Radii

Component borders must follow these exact radii:

| Element | Radius | Tailwind Class / CSS |
| :--- | :--- | :--- |
| **Cards & Panels** | `12px` | `rounded-[12px]` |
| **Inputs & Buttons** | `10px` | `rounded-[10px]` |
| **Badges & Pills** | `6px` | `rounded-[6px]` |
| **Modals & Drawers** | `12px` | `rounded-[12px]` |

---

## 6. Elevation & Shadows

Restrained enterprise shadows with zero excessive blur:

- **Subtle Shadow**:  
  `box-shadow: 0 1px 3px rgba(28, 31, 38, 0.06);`  
  *Usage*: Standard cards, table containers, search bars, static panels.
- **Elevated Shadow**:  
  `box-shadow: 0 8px 24px rgba(28, 31, 38, 0.10);`  
  *Usage*: Dropdown menus, popovers, modals, flyout action menus.

---

## 7. Spacing System

- **Base Unit**: `8px` (`0.5rem`)
- Multiples:
  - `8px` (`0.5rem` / `gap-2`, `p-2`)
  - `16px` (`1rem` / `gap-4`, `p-4`)
  - `24px` (`1.5rem` / `gap-6`, `p-6`)
  - `32px` (`2rem` / `gap-8`, `p-8`) — Standard main content padding
  - `48px` (`3rem` / `gap-12`, `p-12`)

---

## 8. Application Layout Architecture

```
+-------------------------------------------------------------------------------+
| FASCO PRO [Brand]  | Top Bar: [ Search... ]       (Bell) [ User Profile v ]   |
| (w: 260px)         | (h: 64px, bg: #FFFFFF, border-b: #E2E4E9)                |
+--------------------+----------------------------------------------------------+
| Navigation Links   | Main Content Area                                        |
|                    | (bg: #F7F8FA, padding: 32px)                             |
|                    |                                                          |
|                    |                                                          |
|                    |                                                          |
|                    |                                                          |
|                    |                                                          |
| (bg: #FFFFFF       |                                                          |
|  border-r: #E2E4E9)|                                                          |
+--------------------+----------------------------------------------------------+
```

### 1. Left Sidebar
- **Width**: `260px` (fixed on desktop, collapsable drawer on mobile/tablet)
- **Background**: `#FFFFFF`
- **Right Border**: `1px solid #E2E4E9`
- **Header**: FASCO logo / wordmark at the top (`h-16 flex items-center px-6 border-b border-[#E2E4E9]`)
- **Navigation Items**:
  1. **Dashboard** (`LayoutDashboard`)
  2. **Clients** (`Building2`)
  3. **Employees** (`Users`)
  4. **Documents** (`FileText`)
  5. **Renewal Tasks** (`RefreshCw`)
  6. **Service Requests** (`ClipboardList`)
  7. **Invoices** (`Receipt`)
  8. **Staff** (`UserCheck`)
  9. **Notifications** (`Bell`)
  10. **Audit Log** (`History`)
  11. **Settings** (`Settings`)

### 2. Navigation Item Styling
- **Default Item**:
  - Color: `#6B7280`
  - Font Size: `14px` (`text-sm`)
  - Font Weight: `500`
  - Padding: `10px 16px`
  - Hover: `hover:bg-gray-50 hover:text-[#16181D]`
- **Active Item**:
  - Background: `rgba(15, 157, 116, 0.08)`
  - Text & Icon Color: `#0F9D74`
  - Left Border Indicator: `3px solid #0F9D74` (`border-l-[3px] border-[#0F9D74]`)
  - Font Weight: `600`

### 3. Top Bar
- **Height**: `64px` (`h-16`)
- **Background**: `#FFFFFF`
- **Bottom Border**: `1px solid #E2E4E9`
- **Contents**:
  - Global Search Input with `Search` icon and placeholder (`#9CA3AF`)
  - Notifications Bell icon with unread indicator badge
  - Current User info (Name, Role/Avatar)
  - User Dropdown trigger

### 4. Main Content Canvas
- **Background**: `#F7F8FA`
- **Padding**: `32px` (`p-8`)
- **Container Max-Width**: `1600px` (centered or fluid based on dashboard density)

---

## 9. Core Component Standards

### 1. Buttons
- **Shape**: `rounded-[10px]`
- **Typography**: `text-sm font-semibold`
- **Padding**: `px-4 py-2.5`
- **Variants**:
  - **Primary**: `bg-[#1C1F26] text-white hover:bg-[#101319] shadow-[0_1px_3px_rgba(28,31,38,0.06)]`
  - **Accent / Action**: `bg-[#0F9D74] text-white hover:bg-[#0B7D5D]`
  - **Secondary / Outline**: `bg-white border border-[#E2E4E9] text-[#16181D] hover:bg-gray-50`
  - **Ghost**: `text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D]`
  - **Disabled**: `opacity-50 cursor-not-allowed`

### 2. Form Inputs
- **Shape**: `rounded-[10px]`
- **Border**: `1px solid #E2E4E9`
- **Background**: `#FFFFFF`
- **Height**: `40px` (`py-2 px-3.5`)
- **Typography**: `text-sm text-[#16181D]`
- **Placeholder**: `text-[#9CA3AF]`
- **Focus State**: `border-[#0F9D74] ring-2 ring-[rgba(15,157,116,0.15)] outline-none`
- **Error State**: `border-[#DC2626] ring-2 ring-red-100 outline-none`

### 3. Cards & Tables
- **Card**:
  - Background: `#FFFFFF`
  - Border: `1px solid #E2E4E9`
  - Radius: `rounded-[12px]`
  - Shadow: `shadow-[0_1px_3px_rgba(28,31,38,0.06)]`
  - Internal Padding: `24px` (`p-6`)
- **Tables**:
  - Header: `bg-gray-50 text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3`
  - Rows: `border-b border-[#E2E4E9] hover:bg-gray-50/60 transition-colors`
  - Cells: `px-4 py-3.5 text-sm text-[#16181D]`

### 4. Status Badges
- **Shape**: `rounded-[6px]`
- **Padding**: `px-2.5 py-0.5`
- **Typography**: `text-xs font-medium`
- **Examples**:
  - `Completed`: `bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20`
  - `Due Soon`: `bg-amber-50 text-[#D97706] border border-[#D97706]/20`
  - `Expired`: `bg-red-50 text-[#DC2626] border border-[#DC2626]/20`

---

## 10. Design Principles & Anti-Patterns

### Strictly Enforced Principles
1. **Professional B2B Enterprise Restraint**: Clean, predictable, high information density appropriate for government relations and corporate compliance.
2. **Icon Consistency**: Always use **Lucide React** (`lucide-react`) icons with standard stroke width (`1.75` to `2.0`).
3. **Accessibility & Contrast**: Maintain WCAG AA compliance across text colors on `#FFFFFF` and `#F7F8FA`.
4. **Predictable Focus & Hover States**: All interactive elements must provide clear visual feedback without disruptive motion.
5. **Responsive Behavior**: Ensure desktop layout cleanly transforms on tablet and mobile viewports with accessible drawer navigation.

### Anti-Patterns to Avoid
- ❌ **No excessive gradients** or decorative background blobs.
- ❌ **No glassmorphism** or blurred semi-transparent backdrops on primary data surfaces.
- ❌ **No flashy consumer marketing UI**, animated cards, or oversized hero sections.
- ❌ **No unnecessary or prolonged animations** (only subtle 150ms transitions).
- ❌ **No ad-hoc colors or arbitrary border radii** outside the defined tokens.
