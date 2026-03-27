// Leaflet map helpers for LOTV — uses state centroid lookup (no geocoding API needed)
const STATE_CENTERS = {
  "AL":[32.8,-86.8],"AK":[64.2,-153.4],"AZ":[34.3,-111.1],"AR":[34.9,-92.4],
  "CA":[37.2,-119.6],"CO":[39.0,-105.5],"CT":[41.6,-72.7],"DE":[39.0,-75.5],
  "FL":[28.6,-82.4],"GA":[32.7,-83.4],"HI":[20.3,-156.4],"ID":[44.4,-114.6],
  "IL":[40.0,-89.2],"IN":[39.9,-86.3],"IA":[42.1,-93.5],"KS":[38.5,-98.4],
  "KY":[37.5,-85.3],"LA":[31.1,-91.9],"ME":[45.3,-69.0],"MD":[39.1,-76.8],
  "MA":[42.3,-71.8],"MI":[44.3,-85.4],"MN":[46.4,-93.1],"MS":[32.7,-89.7],
  "MO":[38.4,-92.3],"MT":[47.0,-110.4],"NE":[41.5,-99.9],"NV":[39.3,-117.1],
  "NH":[43.7,-71.6],"NJ":[40.1,-74.5],"NM":[34.8,-106.2],"NY":[42.9,-75.5],
  "NC":[35.5,-79.4],"ND":[47.5,-100.5],"OH":[40.4,-82.8],"OK":[35.6,-97.5],
  "OR":[43.9,-120.6],"PA":[40.6,-77.2],"RI":[41.7,-71.5],"SC":[33.9,-80.9],
  "SD":[44.4,-100.2],"TN":[35.9,-86.7],"TX":[31.5,-99.3],"UT":[39.3,-111.1],
  "VT":[44.1,-72.7],"VA":[37.5,-79.5],"WA":[47.4,-120.5],"WV":[38.6,-80.6],
  "WI":[44.3,-89.8],"WY":[42.9,-107.6],"DC":[38.9,-77.0]
};

const _maps = {};

window.initLotvMap = function (id, markers) {
  if (_maps[id]) { _maps[id].remove(); delete _maps[id]; }

  const el = document.getElementById(id);
  if (!el) return;

  const map = L.map(id, { scrollWheelZoom: false }).setView([39.5, -98.35], 4);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 18
  }).addTo(map);

  // Track state offsets to avoid perfect stacking
  const stateUsed = {};

  (markers || []).forEach(function (m) {
    const center = STATE_CENTERS[m.State];
    if (!center) return;

    stateUsed[m.State] = (stateUsed[m.State] || 0) + 1;
    const offset = stateUsed[m.State] - 1;
    const lat = center[0] + (offset % 3 - 1) * 0.6;
    const lng = center[1] + (Math.floor(offset / 3) % 3 - 1) * 0.8;

    const radius = Math.max(8, Math.min(28, 8 + Math.sqrt(Math.abs(m.Value || 0)) / 4));

    L.circleMarker([lat, lng], {
      radius: radius,
      fillColor: m.Color || '#2d469d',
      color: 'white',
      weight: 2,
      opacity: 1,
      fillOpacity: 0.82
    })
      .bindPopup('<strong>' + m.Label + '</strong>' + (m.Tooltip ? '<br>' + m.Tooltip : ''))
      .addTo(map);
  });

  _maps[id] = map;
};

window.destroyLotvMap = function (id) {
  if (_maps[id]) { _maps[id].remove(); delete _maps[id]; }
};
