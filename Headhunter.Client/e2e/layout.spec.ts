import { expect, test, type Page } from '@playwright/test';
import { id } from './mockApi.mts';

// Every page on a phone: no sideways scrolling, and the parts people use (the nav, the paginator's
// buttons and page size) whole on screen. The production build and the mocked API (8.5M voters, so
// the paginator's numbers are as long as on the site) come from e2e/serve.mjs.
//
// Each page and width is screenshotted to screenshots/<page>-<width>.png, uploaded by CI.

// Phones: a small Android, an iPhone
const WIDTHS = [360, 390];
const HEIGHT = 780;

interface PageCase {
  name: string;
  path: string;
  /** Shown once the page's data has loaded. */
  ready: string;
  /** Each must be on screen, horizontally, whole. */
  controls: string[];
}

const NAV = ['nav a'];

const PAGES: PageCase[] = [
  { name: 'globe', path: '/globe', ready: '.cesium-widget canvas', controls: [...NAV] },
  { name: 'voters', path: '/voters', ready: 'ul li a[href^="/voter/"]',
    controls: [...NAV, 'form x-button button', 'x-paginator select', 'x-paginator button', 'x-paginator p'] },
  // A district searched for: its card above the results
  { name: 'voters-district', path: '/voters?districtType=Precinct&district=00012&districtWithin=MOCKINGHAM%20TOWNSHIP',
    ready: 'app-district-card .grid', controls: [...NAV, 'app-district-card .card', 'app-district-card a', 'x-paginator button'] },
  { name: 'voter', path: `/voter/${id('voter', 12)}`, ready: 'h1', controls: [...NAV, 'h1', '.btn-secondary', '.btn-primary'] },
  { name: 'address', path: `/address/${id('address', 4)}`, ready: 'h1', controls: [...NAV, 'h1', '.btn-primary'] },
];

/** Every problem with the layout at the current size. */
function layoutProblems(page: Page, controls: string[]) {
  return page.evaluate((controls) => {
    const problems: string[] = [];
    const width = document.documentElement.clientWidth;
    const name = (el: Element) => el.tagName.toLowerCase() + [...el.classList].slice(0, 2).map(c => '.' + c).join('');

    // Sideways scrolling, of the page or of the content area under the nav (which scrolls down)
    if (document.documentElement.scrollWidth > width)
      problems.push(`the page scrolls sideways (${document.documentElement.scrollWidth}px wide in ${width}px)`);
    const main = document.querySelector('main');
    if (main && main.scrollWidth > main.clientWidth)
      problems.push(`the content scrolls sideways (${main.scrollWidth}px wide in ${main.clientWidth}px)`);

    for (const selector of controls) {
      const found = [...document.querySelectorAll(selector)].filter(el => {
        const r = el.getBoundingClientRect();
        return r.width > 0 && r.height > 0;
      });
      if (found.length === 0) problems.push(`${selector} is missing`);
      for (const el of found) {
        const r = el.getBoundingClientRect();
        if (r.left < -1 || r.right > width + 1)
          problems.push(`${selector}: ${name(el)} "${(el.textContent ?? '').trim().slice(0, 30)}" is off screen (${Math.round(r.left)} to ${Math.round(r.right)})`);
        // Text that should be one line and wraps
        else if (el.tagName === 'P' && r.height > 1.5 * parseFloat(getComputedStyle(el).lineHeight))
          problems.push(`${selector}: "${(el.textContent ?? '').trim()}" wraps (${Math.round(r.height)}px tall)`);
      }
    }
    return problems;
  }, controls);
}

for (const { name, path, ready, controls } of PAGES) {
  test(`the ${name} page fits a phone`, async ({ page }) => {
    // Hermetic: everything comes from e2e/serve.mjs
    await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
    const problems: string[] = [];
    for (const width of WIDTHS) {
      await page.setViewportSize({ width, height: HEIGHT });
      await page.goto(path);
      await expect(page.locator(ready).first()).toBeVisible({ timeout: 30_000 });
      await page.waitForTimeout(500); // rows fade in
      await page.screenshot({ path: `screenshots/${name}-${width}.png` });
      for (const p of await layoutProblems(page, controls))
        problems.push(`${width}px: ${p}`);
    }
    expect(problems).toEqual([]);
  });
}
