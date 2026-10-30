// Lighthouse CI (https://github.com/GoogleChrome/lighthouse-ci), run by the lighthouse job in
// .github/workflows/client-tests.yml, once per preset: LIGHTHOUSE_PRESET=mobile|desktop.
// (Not LHCI_*: lhci reads those environment variables as its own options.)
// It audits the production build as e2e/serve.mjs serves it, with the API answered from
// e2e/mockApi.mts (8.5M voters, as on the site) and grey map tiles.
const { id } = require('./e2e/mockApi.mts');

const port = Number(process.env.E2E_PORT ?? 4300);
const desktop = process.env.LIGHTHOUSE_PRESET === 'desktop';
const base = `http://127.0.0.1:${port}`;

const pages = ['/voters', `/voter/${id('voter', 12)}`, `/address/${id('address', 4)}`];
// The globe ("/" redirects there): Cesium, its WebGL globe and its workers. Lighthouse's
// performance score there measures Cesium starting up (several MB of script and seconds of main
// thread work, more so on the runner's software WebGL), not anything this app can trim, so it has no
// performance floor. Its accessibility, best practices and SEO count as on every page.
const globe = '/globe';

// Performance must not drop below these on the CI runner, some margin below what it scores.
// Cesium.js is a global script (angular.json), so every page loads it; that is most of the cost.
// A single mobile run varies by some 15 points there, so mobile takes the median of three.
// e2e/lighthouse-summary.mjs warns below 90.
const performanceFloor = desktop ? 0.7 : 0.4;

const median = { aggregationMethod: 'median-run' };
module.exports = {
  ci: {
    collect: {
      startServerCommand: 'node e2e/serve.mjs',
      startServerReadyPattern: 'Serving',
      url: [globe, ...pages].map(path => base + path),
      numberOfRuns: desktop ? 1 : 3,
      settings: {
        ...(desktop ? { preset: 'desktop' } : {}),
        // e.g. --no-sandbox when Chromium runs as root in a container. Software WebGL for the globe.
        chromeFlags: `--use-gl=angle --use-angle=swiftshader --enable-unsafe-swiftshader ${process.env.LIGHTHOUSE_CHROME_FLAGS ?? ''}`,
        // Slow, and only a picture for the report
        skipAudits: ['full-page-screenshot'],
      },
    },
    assert: {
      // Of several runs of a page, the median one counts
      assertMatrix: [
        {
          matchingUrlPattern: '.*',
          assertions: {
            'categories:accessibility': ['error', { minScore: 0.9, ...median }],
            'categories:best-practices': ['error', { minScore: 0.9, ...median }],
            'categories:seo': ['error', { minScore: 0.9, ...median }],
          },
        },
        {
          matchingUrlPattern: '/(voters|voter|address)(/|$)',
          assertions: {
            'categories:performance': ['error', { minScore: performanceFloor, ...median }],
          },
        },
      ],
    },
    upload: {
      target: process.env.LIGHTHOUSE_UPLOAD_TARGET ?? 'temporary-public-storage',
    },
  },
};
