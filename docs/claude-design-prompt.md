# Visual Mockup Prompt for Claude.ai

Paste everything below the line into Claude.ai to generate interactive HTML/CSS mockups.

---

## Prompt

I'm building the frontend for **Memory of X** — a tweet archival and curation platform. The app captures tweets from X (Twitter) before they disappear, organizes them into hierarchical folders, and lets visitors search, browse, and vote on archived tweets.

The tech stack is **Blazor Server (.NET 8)** with **Bootstrap 5** for styling. The backend API is fully built. I need you to create **interactive HTML/CSS/JS mockups** as artifacts for all the pages described below.

### Design Guidelines

- **Framework:** Bootstrap 5 (use CDN in the artifact)
- **Color palette:** Clean, modern, content-focused. Use a dark navy/charcoal primary color (#1a1a2e or similar) with a warm accent for vote buttons and CTAs (#e63946 or similar). White/light gray backgrounds for content areas. The brand is about preservation and memory — feel serious and trustworthy, not flashy.
- **Typography:** System font stack or Inter/Source Sans. Clean and readable. Tweet text should feel like quoted content (slightly different styling from UI text).
- **Layout:** Top navbar (sticky), content area, footer. No sidebar on public pages. Max-width container (~1200px) centered.
- **Cards:** Rounded corners (8px), subtle shadow, white background. Used for folder cards and tweet cards.
- **Responsive:** Must work on mobile (single column) and desktop (2-3 column grids).
- **Icons:** Use Bootstrap Icons (bi bi-*) for folder, search, vote, user, etc.
- **Auth state:** Show a "Sign in with X" button in navbar. When signed in, show username + avatar placeholder. Contributor/admin actions (create folder, submit tweet, edit) should be visible but subtly styled — the app is primarily a public reading experience.

### Pages to Mock Up

Create each page as a separate artifact, or combine into a tabbed single-page prototype with navigation between pages.

---

#### Page 1: Home (`/`)

The landing page. Primary goal: get visitors browsing folders or searching.

**Layout (top to bottom):**
1. **Navbar** — Logo "Memory of X" on left, nav links (Home, Browse, Search, Submit), "Sign in with X" button on right
2. **Hero section** — Large heading "Memory of X", subheading "Archiving tweets before they disappear", prominent search bar with placeholder "Search tweets, users, tags..." and a Search button
3. **Featured Folders section** — Heading "Browse Collections", 3-column card grid showing 6 root folders. Each card has: folder icon, folder name (bold), description snippet (2 lines, muted text), "N subfolders" badge. Cards link to folder detail.
4. **Top Voted Tweets section** — Heading "Most Voted", vertical list of 3-5 tweet cards (compact version). Each shows: vote count on the left (large number with up-arrow icon), author @username + date, tweet text (truncated to 2 lines), tag badges. Clicking goes to tweet detail.
5. **Footer** — "Memory of X · Preserving public discourse" centered, muted

---

#### Page 2: Browse Folders (`/folders`)

Grid view of all root-level folders.

**Layout:**
1. **Breadcrumb** — "Home > Folders"
2. **Page header** — "All Folders" with a "+ New Folder" button on the right (styled as outline/secondary — it's a contributor action)
3. **Folder grid** — 3-column responsive grid of folder cards. Each card: large folder icon at top, folder name (bold, 18px), description (muted, max 2 lines), footer showing "N subfolders" with a chevron-right icon
4. Show 6-9 folder cards for the mockup

---

#### Page 3: Folder Detail (`/folders/{id}`)

The main content browsing page — most important page to get right.

**Layout:**
1. **Breadcrumb** — "Home > Politics > US Elections" (3 levels deep to demonstrate hierarchy)
2. **Folder header** — Folder name "US Elections" (h2), description paragraph below, "Edit" icon button on right (contributor only, subtle)
3. **Subfolders row** — If subfolders exist, show a horizontal scrollable row of small cards: "2024 Race", "Primaries", "Debates". Each shows name + tweet count. Subtle background to distinguish from tweet list below.
4. **Tweet list header** — "89 Tweets" count on left, sort dropdown ("Most Voted" / "Newest" / "Oldest") on right, "+ Add Tweet" button (contributor, outline style)
5. **Tweet cards** — Vertical list of tweet cards. Each card has this layout:

```
┌──────────────────────────────────────────────────┐
│  [▲]    @username · Jun 20, 2026                 │
│  142    "Tweet text goes here, can be multiple    │
│ votes    lines long. Truncated after 3 lines..."  │
│         ┌─────────────┐                           │
│         │ [screenshot  │  🏷 #tag1  #tag2         │
│         │  thumbnail]  │                           │
│         └─────────────┘                           │
└──────────────────────────────────────────────────┘
```

   - Vote section on the left: up-arrow button, vote count number (bold, large), "votes" label
   - Content on the right: author @username (link-colored) + relative date, tweet text (max 3 lines), optional screenshot thumbnail (small, ~120px), tag badges at bottom
   - The entire card is clickable to go to tweet detail
   - Show 4-5 tweet cards with varying content lengths and vote counts

6. **Pagination** — "Previous / Page 1 of 5 / Next" centered at bottom

---

#### Page 4: Tweet Detail (`/tweets/{id}`)

Full view of a single archived tweet.

**Layout:**
1. **Breadcrumb** — "Home > Politics > US Elections > Tweet"
2. **Author section** — Avatar placeholder (circle, 48px), @username (bold), custom display name below (muted), "View profile →" link
3. **Tweet content** — Full tweet text in a slightly larger font or blockquote style. Should feel like quoted content.
4. **Screenshot** — Full-width screenshot image placeholder (gray box with "Tweet Screenshot" text, ~400px tall)
5. **Media gallery** — If media exists, show 2 image placeholders in a row (with rounded corners)
6. **Metadata row** — Tweet date (calendar icon), "View original on X" external link (link icon), tags as badges
7. **Vote section** — Centered, prominent: large up-arrow button, vote count (24px bold), "Vote for this tweet" text. After voting, arrow turns filled/colored and text changes to "You voted!"
8. **Folders section** — "In folders:" followed by linked folder names as badges/chips

---

#### Page 5: Search Results (`/search?q=...`)

Full-text search with filters.

**Layout:**
1. **Search bar** — Full-width at top (same as home hero, but smaller). Pre-filled with "climate policy" as example query.
2. **Filter row** — Horizontal bar below search: Tag dropdown (select), Username text input, Sort dropdown ("Most Voted" / "Newest"). Compact inline layout.
3. **Results count** — "23 results for 'climate policy'"
4. **Optional: Subject Profile card** — When searching by username, show a card at the top: avatar placeholder, @username, custom name, description. This represents the XUserProfile.
5. **Result list** — Compact tweet cards (same as folder detail but without screenshot thumbnails to keep results scannable). Search terms highlighted/bolded in tweet text. Show 4-5 results.
6. **Pagination** — Same as folder detail

---

#### Page 6: Submit Tweet (`/submit`)

Contributor-only page for submitting tweets to be archived.

**Layout:**
1. **Page header** — "Submit a Tweet" with a subtitle "Paste a tweet URL to archive it"
2. **Form:**
   - Tweet URL input (required) — Full-width text input with placeholder "https://x.com/username/status/..."
   - Folder selection — Checklist/multi-select of available folders, shown as a scrollable list with checkboxes. Show 5-6 folders with hierarchy indicated by indentation (e.g., "Politics > US Elections")
   - Submit button — Primary colored, "Archive Tweet"
3. **Status section** (shown after submit, below the form):
   - **Processing state:** Progress indicator with steps: "Fetching tweet data ✓", "Loading author profile ✓", "Capturing screenshot ⏳", "Downloading media ○". Show a subtle animated spinner next to the current step.
   - **Success state:** Green alert box — "Tweet archived successfully!" with two buttons: "View Tweet" (primary) and "Submit Another" (outline)
   - **Error state:** Red alert box — "Scrape failed: Tweet not found (may be deleted or private)" with "Try Again" button
   - Show all three states stacked (labeled "Processing...", "Success", "Error") so I can see them all

---

#### Page 7: Admin Panel (`/admin/users`)

Simple user management table for admins.

**Layout:**
1. **Page header** — "User Management" with "+ Add User" button on right
2. **Users table:**
   - Columns: X Username, X User ID, Role (badge: "Admin" in blue, "Contributor" in green), Status (badge: "Active" green / "Inactive" gray), Created, Actions
   - Show 5-6 rows of sample data
   - Actions column: "Edit" and "Deactivate" buttons (small, outline)
3. **Add/Edit User modal** — A Bootstrap modal overlay with fields: X User ID (text input), Role (dropdown: Admin/Contributor), and Save/Cancel buttons

---

### Sample Data to Use

**Folders:**
- Politics (12 subfolders) — "Tweets on policy, elections, and governance"
- Tech & AI (8 subfolders) — "Silicon Valley, AI breakthroughs, and digital culture"
- Sports (5 subfolders) — "Major sporting events and athlete commentary"
- Science (3 subfolders) — "Research breakthroughs and scientific discourse"
- Culture & Media (6 subfolders) — "Arts, entertainment, and social commentary"
- Breaking News (15 subfolders) — "Real-time coverage of major events"

**Sample tweets (use across pages):**
1. @journalist, Jun 20 2026, "Breaking: Major policy reversal announced this morning. Sources confirm the decision was weeks in the making. Full thread below...", 142 votes, tags: #politics #breaking
2. @techleader, Jun 18 2026, "Thread on why AI regulation needs to happen now, not later. Here's what most people are missing about the current proposals...", 87 votes, tags: #AI #regulation
3. @scientist, Jun 15 2026, "New peer-reviewed study shows significant correlation between climate policy and economic growth. Data from 40 countries over 20 years.", 63 votes, tags: #climate #research
4. @analyst, Jun 12 2026, "Poll numbers showing a dramatic shift in key swing states. This is unprecedented for this point in the cycle.", 45 votes, tags: #polls #elections
5. @activist, Jun 8 2026, "This tweet was deleted 2 hours after posting but we archived it. Thread on corporate lobbying against environmental regulations.", 31 votes, tags: #environment #accountability

**Users (for admin panel):**
- @admin_user, Admin, Active, Jan 2026
- @curator1, Contributor, Active, Feb 2026
- @curator2, Contributor, Active, Mar 2026
- @researcher, Contributor, Active, Apr 2026
- @former_mod, Contributor, Inactive, Jan 2026

### Important Notes

- Make all mockups interactive where possible (hover states on cards, clickable sort dropdowns, toggle states on vote buttons)
- Use realistic proportions and spacing — these should look like a real app, not a wireframe
- The overall feel should be: Wikipedia meets Reddit meets Internet Archive — serious, content-focused, community-driven
- Tweet screenshots are a key feature — give them visual prominence in the tweet detail view
- The vote mechanism is central — make the vote UI feel satisfying and prominent (like Reddit's upvote)
- This is a public-interest archival tool — the design should convey trust and permanence
