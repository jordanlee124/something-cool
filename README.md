# something-cool

## Ember Vale

A hands-off colony simulator you can watch on your phone. `ember-vale.html` is the whole game: one HTML file with no build step and no dependencies apart from Google Fonts.

A steward AI runs the colony. It assigns jobs (farmers, woodcutters, miners, builders) and decides what to build and where. The settlement grows through six ages, Settlement → Village → Town → City → Kingdom → Empire, and each age needs a population target plus a landmark: chapel, market, cathedral, castle, then palace.

What else happens along the way:

- **Seasons:** farms yield little in winter, and every hearth burns wood.
- **Mood:** food, housing, cold, losses, landmarks and size all move it up or down.
- **Breakaway settlements:** groups occasionally leave to found their own towns. Depending on mood, these start out allied, neutral or hostile.
- **Barbarian camps:** they appear at the edges of the map, grow stronger and raid everyone.
- **Speed controls:** Pause, 1×, 3× and 10×. You can tap the map to inspect anything.

Progress saves to the browser's local storage.

### Run it

Open `ember-vale.html` in any browser. To play it on an iPhone, serve it from anywhere, for example GitHub Pages: Settings → Pages → deploy from the `main` branch. Then open the page in Safari and choose Share → Add to Home Screen.
