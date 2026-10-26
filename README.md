# something-cool

## Ember Vale

A hands-off colony simulator you can watch on your phone. `ember-vale.html` is the whole game: one HTML file with no build step and no dependencies apart from Google Fonts.

It is a world you watch, not one you play. When you start a new world you pick how many settlements it begins with (1–4), and they start out neutral to each other. Each settlement has its own steward AI, which assigns jobs and decides what to build and where. Each one grows through six ages, Settlement → Village → Town → City → Kingdom → Empire, and every age needs a population target plus a landmark: chapel, market, cathedral, castle, then palace.

What else happens along the way:

- **Territory:** each settlement's land grows with its age, and from the Town age on it draws a border. It can only build inside that land.
- **Politics:** every pair of settlements has its own relationship. Mood, trade and the friction of a shared border move it up or down. So do shared enemies, rivalry between the two largest powers, and political events such as royal marriages, trade pacts, border disputes, heresy and murdered envoys. Relationships can turn into alliances or wars, and wars bring raids, peace treaties and sometimes conquest.
- **Breakaways:** groups occasionally leave to found new settlements. These are full settlements in their own right, and the parent settlement's mood decides whether they start as friends or enemies.
- **Barbarian camps:** they appear in the unclaimed wilds, grow stronger and raid whoever is closest.
- **Seasons and mood:** farms yield little in winter and every hearth burns wood. Food, housing, cold, losses, landmarks, wars and allies all affect mood.
- **Controls:** speed buttons (Pause, 1×, 3×, 10×); Realm, Politics and Chronicle tabs; and tap a settlement to follow it.

Progress saves to the browser's local storage.

### Run it

Open `ember-vale.html` in any browser. To play it on an iPhone, serve it from anywhere, for example GitHub Pages: Settings → Pages → deploy from the `main` branch. Then open the page in Safari and choose Share → Add to Home Screen.
