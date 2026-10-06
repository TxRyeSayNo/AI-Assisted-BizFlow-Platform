const gridTheme = require('./src/styles/tailwind.theme.cjs');

module.exports = {
  content: ['./src/**/*.{html,ts}'],
  corePlugins: { preflight: false },
  theme: { extend: gridTheme },
  plugins: [],
};
