# Usage statistics

Two questions with different answers: **how often ContextSwitcher is downloaded**, which GitHub
already counts and a workflow now keeps a history of, and **how many people use it**, which nothing
can say yet. The second half of this page is the plan for that.

## What is collected now

Every night, the [Usage stats workflow](../.github/workflows/stats.yml) writes down what GitHub
counts about the project on the
[`stats` branch](https://github.com/ArtemkaGoldMan/ContextSwitcher/tree/stats), whose README sums
it up with charts. Each run also shows the summary on its page under Actions → Usage stats. The
data is CSV, one file per kind, added to day by day. The app takes no part in any of it.

### What the numbers mean

- **Downloads** count every download, not every person: one person downloading three times is
  three. `ContextSwitcher.zip` is what the app's updater, `install.sh` and Homebrew download, so a
  release's `.zip` count is roughly the copies that installed or updated to it. The `.dmg` is only
  ever downloaded by hand. Your own downloads and updates count too.
- **Downloads a day** is the difference between two nights' totals, so it starts on the second run.
- **Visitors** are GitHub's own count of views of the repository's pages and of the different
  people behind them. Your own visits count: with few visitors, most of the views are you.
- **Clones** are nearly all automated - CI, bots, mirrors - and say little about people.
- **Where they came from** and **what they looked at** are GitHub's top ten for the 14 days up to
  the run.

GitHub keeps visitors for 14 days only. If the workflow stops for longer, the days in between are
lost for good; downloads are not, since they are running totals.

### Setting it up: the token for visitors

Downloads need nothing. Visitors can only be read with access to the repository's Administration
settings, which the workflow's own token never has, so they need a token of yours. Until it is
there, every run collects downloads and says "Visitors not collected" as a warning - the run still
succeeds.

1. Open [New fine-grained token](https://github.com/settings/personal-access-tokens/new) (GitHub →
   your photo → Settings → Developer settings → Personal access tokens → Fine-grained tokens →
   Generate new token).
2. **Token name:** `ContextSwitcher stats`. **Expiration:** a year, with a reminder in your
   calendar to make a new one.
3. **Repository access:** Only select repositories → `ContextSwitcher`.
4. **Permissions** → Repository permissions → **Administration: Read-only**. Nothing else; GitHub
   adds Metadata: Read-only by itself.
5. **Generate token** and copy it.
6. In the repository: Settings → Secrets and variables → Actions → **New repository secret**.
   Name `STATS_TRAFFIC_TOKEN`, paste the token, **Add secret**. Or, in Terminal, run the command
   below and paste the token when it asks:

   ```bash
   gh secret set STATS_TRAFFIC_TOKEN --repo ArtemkaGoldMan/ContextSwitcher
   ```

7. Run it once rather than waiting for the night: Actions → Usage stats → **Run workflow**, or:

   ```bash
   gh workflow run stats.yml --repo ArtemkaGoldMan/ContextSwitcher
   ```

The token can read the repository's settings, not change them, and only this repository's.

### When it stops

- **The token expired:** runs still succeed, with the "Visitors not collected" warning, and the
  branch's README says visitors were not updated. Make a new token and replace the secret.
- **Nothing happened in the repository for 60 days:** GitHub pauses scheduled workflows in a public
  repository and sends an email. Actions → Usage stats → **Enable workflow** starts it again.

## The plan: counting the people who use it

Not built yet. Downloads cannot say how many people use the app: an update is a download, one
person can download five times, and someone who deleted the app a month ago still counts. The only
way to know is for the app to say, once a day, that it is in use. That is a change to what the app
promises - the README says "No account, no tracking, no analytics sent anywhere", and `agent.md`
§13 "No telemetry" - so it is done in the open or not at all.

### What it sends, all of it

Once a day at most, alongside the update check, and only when turned on:

| Field | Example | Why |
| --- | --- | --- |
| `install` | `8f14e45f-…` | A random id made when the setting is turned on, and deleted when it is turned off. It is what tells one person from two; it says nothing about who. |
| `version` | `0.6.3` | Which versions are still in use. |
| `os` | `26.1` | Which macOS versions to keep working on. |

Nothing else: no profiles, apps, sites, times, names or email. The server keeps no IP address -
Cloudflare sees one to deliver the request, as any server does, and the code below never stores it.
The app is open source, so anyone can check that this is all.

### Asked, not assumed

Two honest ways to turn it on. Choose one before starting:

- **Ask (recommended).** Setup asks "Share an anonymous count of people using ContextSwitcher?",
  answered no unless chosen; Settings has the same switch. People who installed earlier keep it off
  until they turn it on. Fewer numbers, no broken promise - and in the EU, consent is the simplest
  footing for keeping an install id.
- **On by default, said plainly.** The README's Privacy section, the release notes and Settings say
  so, with the switch to turn it off. More numbers, but it changes the deal for people who installed
  it on the old one.

### What it gives

People using the app each day, week and month; new installs a day; which versions and macOS
versions are in use; how many still use it a month after installing. All of it as counts, written
to the `stats` branch next to the downloads.

### How: a Cloudflare Worker with a small database (recommended)

```text
app ──POST /ping (once a day)──▶ Worker ──▶ D1 table: one row per install per day
stats workflow ──GET /stats (secret)──▶ Worker ──▶ counts only ──▶ stats branch
```

**Cost:** nothing at this size. The free plan allows 100,000 requests a day, and one person is one
request a day. D1, Cloudflare's database, is free within daily row limits that stop queries rather
than charge, from September 2026; tens of thousands of people a day stay well inside them.

**Where to register and what to run:**

1. **Cloudflare account:** [dash.cloudflare.com/sign-up](https://dash.cloudflare.com/sign-up) -
   email and password, no card for the free plan. In Workers & Pages, pick the `workers.dev`
   subdomain your Workers will live under.
2. **Node.js** on the Mac, for Cloudflare's command line tool: `brew install node`.
3. **The Worker's code** goes in the repository, in `stats-server/`: `wrangler.toml`,
   `src/index.js` and `schema.sql` (below).
4. **Sign in:** `npx wrangler login` - it opens the browser.
5. **The database:** `npx wrangler d1 create contextswitcher-stats`, and its `database_id` goes into
   `wrangler.toml`. Then the table:
   `npx wrangler d1 execute contextswitcher-stats --remote --file schema.sql`.
6. **The secret** for reading the counts: `npx wrangler secret put STATS_READ_TOKEN`, a long random
   string. The same string becomes the repository secret `STATS_SERVER_TOKEN`, for the workflow.
7. **Deploy:** `npx wrangler deploy` prints the address,
   `https://contextswitcher-stats.<subdomain>.workers.dev`, which goes into the app.
8. **Optional:** deploy from GitHub Actions instead of the Mac, with `CLOUDFLARE_API_TOKEN` (a
   Cloudflare API token allowed to edit Workers) and `CLOUDFLARE_ACCOUNT_ID` as repository secrets.

**The server**, about 120 lines:

```sql
CREATE TABLE pings (
  day     TEXT NOT NULL,  -- UTC date, 2026-10-15
  install TEXT NOT NULL,  -- the random id
  version TEXT NOT NULL,
  os      TEXT NOT NULL,
  PRIMARY KEY (day, install)
);
```

- `POST /ping` takes the JSON above, refuses anything that isn't a UUID, a version and a macOS
  version, and writes today's row for that install - a second ping the same day replaces it, so
  sending twice counts once. It answers `204`.
- `GET /stats`, with `Authorization: Bearer <STATS_READ_TOKEN>`, answers counts only - never ids:
  active installs a day for the last 90 days, a week and a month, new installs a day (an id's first
  day), and versions in use over the last week.
- A daily cron trigger deletes rows older than 400 days.

Anyone can send made-up pings to a public address, and an open-source app cannot hide a key that
would stop it. Strict checks of what is sent, the one-row-per-install-per-day table, and Cloudflare's
rate limiting keep that small; the numbers are close, not exact.

**The app**, about 300 lines and their tests:

- `ContextSwitcher.Core`: the setting (`shareUsageCount`, off), and `IUsagePing` in
  `Abstractions`.
- The install id lives in a file of its own in the settings folder, not in `settings.json`, so
  exporting settings to another Mac doesn't copy it. Turning the setting off deletes it; turning it
  on again makes a new one.
- `ContextSwitcher.Infrastructure`: `HttpUsagePing`, on the app's existing `HttpClient`, with a
  short timeout. A ping that fails is retried tomorrow and never shown; at most it is a line in the
  detailed log.
- `ContextSwitcher.App`: sent from the same daily timer as the update check
  (`UpdatesViewModel`), and once when the setting is turned on. A switch in Settings, under
  Privacy, listing exactly what is sent, with a link here; the question in setup; the state in
  Settings → Diagnostics' report. Text in English and Ukrainian.
- Tests: nothing is sent while off; what is sent has exactly the three fields; off deletes the id
  and on makes a new one; a failure is silent; the id never appears in an export.
- In the same pull request: the README's Privacy section, `agent.md` §13, `docs/configuration.md`
  and the release notes, saying what is sent and how to turn it off.

**The workflow:** one more step in `stats.yml` reads `GET /stats` with `STATS_SERVER_TOKEN`, keeps
`people.csv` (day, active, new) and adds a "People using it" section with a chart to the branch's
README.

### Without a server of our own

| Service | Free | Good | Not so good |
| --- | --- | --- | --- |
| [TelemetryDeck](https://telemetrydeck.com) | 100,000 signals a month - prices changed in July 2026, so check what a new account gets | Dashboards ready-made; built for privacy; EU company | No official .NET library: signals go over plain HTTP. The data is theirs to keep, not ours |
| [Aptabase](https://aptabase.com/pricing) | 20,000 events a month | Dashboards ready-made; sends no identifiers at all | Because of that it counts sessions a day, not people: no monthly users, no retention. Only an unofficial .NET library |

Registering is an account on their site, an app created in their dashboard, and the key it gives
(TelemetryDeck's App ID, Aptabase's App Key) in the app. Everything under "What it sends" and "Asked,
not assumed" applies the same.

The Worker is the better fit: the data stays ours, it counts people rather than sessions, and it
costs nothing. TelemetryDeck is the choice if ready-made dashboards matter more than owning the data.

### Decisions to make

- Ask, or on by default.
- Cloudflare, or TelemetryDeck.
- The fields: the three above, or also the processor and the app's language.
- Whether people who installed earlier are asked once, on a card after updating, or only find the
  switch in Settings.
