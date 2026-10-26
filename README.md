# something-cool

## Ember Vale

A hands-off colony simulator you can watch on your phone. `ember-vale.html` is the whole game: one HTML file with no build step and no dependencies apart from Google Fonts.

It is a world you watch, not one you play. When you start a new world you pick how many settlements it begins with (1–4), and they start out neutral to each other. Each settlement has its own steward AI, which assigns jobs and decides what to build and where. Each one grows through six ages, Settlement → Village → Town → City → Kingdom → Empire, and every age needs a population target plus a landmark: chapel, market, cathedral, castle, then palace.

What else happens along the way:

- **Territory:** each settlement's land grows with its age, and from the Town age on it draws a border. It can only build inside that land.
- **Politics:** every pair of settlements has its own relationship. Mood, trade and the friction of a shared border move it up or down. So do shared enemies, rivalry between the two largest powers, and political events such as royal marriages, trade pacts, border disputes, heresy and murdered envoys. Relationships can turn into alliances or wars, and wars bring raids, peace treaties and sometimes conquest.
- **Borders:** villagers can't walk through another realm's land unless the two have an open-borders treaty. Allies open their borders automatically; friendly neighbours sign treaties, and souring relations or war close them again. A villager caught on the wrong side when a border moves walks home.
- **Breakaways:** groups occasionally leave to found new settlements. These are full settlements in their own right, and the parent settlement's mood decides whether they start as friends or enemies.
- **Rulers and the chronicle:** every settlement has a named ruler with a trait (wise, just, pious, proud, weak or cruel) that affects mood and diplomacy. Rulers die and are succeeded, and they earn epithets such as "the Conqueror" or "the Brief". The Chronicle tab keeps a year-by-year history of the vale, and big moments such as wars, betrayals, conquests, collapses and plagues pop up as cards over the map.
- **Decline and collapse:** plagues break out and spread along borders, alliances and trade. Unhappy realms revolt, disputed successions start civil wars, and aging empires can shatter into successor states. A settlement that loses its people or its landmark falls back an age.
- **An ever-expanding map:** the world is made of 16×16 chunks generated from a seed. As settlements grow toward the edge, or there is no room left to found a new one, scouts map new land and the world grows in that direction. More land allows more settlements, up to 10, while the total population stays capped at 900 to keep phones fast. The camera follows the latest big event; the "Whole world" button zooms out to show everything.
- **Barbarian camps:** they appear in the unclaimed wilds, grow stronger and raid whoever is closest.
- **Seasons and mood:** farms yield little in winter and every hearth burns wood. Food, housing, cold, losses, landmarks, wars and allies all affect mood.
- **Controls:** speed buttons (Pause, 1×, 3×, 10×); Realm, Politics and Chronicle tabs; and tap a settlement to follow it.

The line under the map shows frames per second, the slowest recent frame, and how many milliseconds the simulation and the drawing take per frame; tap it to shorten it.

Progress saves to the browser's local storage. Saves from the earlier fixed-size map are migrated automatically.

### Code layout

The game is still one file, organised into classes: `World` (chunked tiles, terrain generation, pathfinding), `Settlement`, `Colonist`, `Steward` (the settlement AI), `Ruler`, `Camp`, `Diplomacy`, `Chronicle`, `Game` (the clock and save/load), `Renderer` (per-chunk cached drawing and the camera) and `Interface` (panels and cards).

### Run it

Open `ember-vale.html` in any browser. To play it on an iPhone, serve it from anywhere, for example GitHub Pages: Settings → Pages → deploy from the `main` branch. Then open the page in Safari and choose Share → Add to Home Screen.
