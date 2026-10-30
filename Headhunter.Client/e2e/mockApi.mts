// The API, mocked for Lighthouse and the layout test (e2e/serve.mjs answers /api/* with this):
// synthetic, obviously fake Michigan-looking voters, never real data. The counts are the size of the
// real thing (8.5M voters), so a page that shows them gets numbers as long as on the site.

export const TOTAL_VOTERS = 8_506_687;

export interface MockResponse {
  status: number;
  contentType: string;
  body: string;
}

const FIRST = ['Alex', 'Jordan', 'Taylor', 'Morgan', 'Casey', 'Riley', 'Jamie', 'Avery', 'Quinn', 'Rowan'];
const MIDDLE = ['Lee', 'Marie', 'James', '', 'Ann', 'Ray', 'Lynn', '', 'Jo', 'Dale'];
const LAST = ['Example', 'Sample', 'Placeholder', 'Testerson', 'Mockingbird', 'Fakename', 'Dummy', 'Specimen', 'Prototype', 'Notreal'];
// Long ones too: the results truncate them, the voter and address pages wrap them.
const STREETS = ['Fake', 'Pretend', 'Imaginary Lake', 'Synthetic Ridge Country Club', 'Mock', 'Nowhere'];
const TYPES = ['St', 'Ave', 'Rd', 'Dr', 'Blvd', 'Ct'];
const CITIES = ['Testville', 'Sampleton', 'Mockingham Township', 'Fakesburg', 'Placeholder Heights', 'Examplewood'];

/** A GUID, as the API's ids are, made from a number. */
export function id(kind: 'voter' | 'address', n: number): string {
  const hex = n.toString(16).padStart(12, '0');
  return `${kind === 'voter' ? '00000000' : '0000000a'}-0000-4000-8000-${hex}`;
}

function voterRow(n: number) {
  const address = Math.floor(n / 3);
  return {
    id: id('voter', n),
    addressId: id('address', address),
    firstName: FIRST[n % FIRST.length],
    middleName: MIDDLE[(n * 7) % MIDDLE.length],
    lastName: LAST[(n * 3) % LAST.length],
    birthYear: 1940 + (n * 13) % 65,
    gender: ['F', 'M', 'X'][n % 3],
    streetNumberPrefix: '',
    streetNumber: String(100 + (address * 37) % 9800),
    streetNumberSuffix: '',
    directionPrefix: address % 4 === 0 ? 'N' : '',
    streetName: STREETS[address % STREETS.length],
    streetType: TYPES[address % TYPES.length],
    directionSuffix: '',
    extension: address % 5 === 0 ? 'Apt 1204' : '',
    city: CITIES[address % CITIES.length],
    state: 'MI',
    zipCode: String(48001 + (address * 11) % 1900),
    latitude: 42.3 + (address % 50) * 0.02,
    longitude: -83.2 - (address % 40) * 0.03,
  };
}

function voterDetail(n: number) {
  const row = voterRow(n);
  const street = [row.directionPrefix, row.streetNumber, row.streetName, row.streetType, row.extension].filter(x => x).join(' ');
  return {
    id: row.id,
    firstName: row.firstName,
    middleName: row.middleName,
    lastName: row.lastName,
    birthYear: row.birthYear,
    gender: row.gender,
    registrationDate: `${2000 + n % 24}-0${1 + n % 9}-15T00:00:00`,
    addressId: row.addressId,
    fullStreetAddress: street,
    city: row.city,
    state: row.state,
    zip: row.zipCode,
    latitude: row.latitude,
    longitude: row.longitude,
  };
}

/** The number in a mock id, or -1 for any other. */
function numberOf(guid: string): number {
  const match = /^0000000[0a]-0000-4000-8000-([0-9a-f]{12})$/i.exec(guid);
  return match ? parseInt(match[1], 16) : -1;
}

const json = (body: unknown, status = 200): MockResponse => ({ status, contentType: 'application/json', body: JSON.stringify(body) });
const notFound = (): MockResponse => ({ status: 404, contentType: 'text/plain', body: '' });

/** The mocked answer to an API call (path with its /api prefix, any case, and its query). */
export function mockResponse(method: string, url: string): MockResponse {
  const { pathname, searchParams } = new URL(url, 'http://localhost');
  const path = pathname.toLowerCase().replace(/^\/api\//, '');

  if (method === 'POST' && path === 'voters') {
    const pageSize = Math.max(1, Number(searchParams.get('pageSize') ?? 10));
    const pageNumber = Math.max(1, Number(searchParams.get('pageNumber') ?? 1));
    const start = (pageNumber - 1) * pageSize;
    const results = Array.from({ length: Math.max(0, Math.min(pageSize, TOTAL_VOTERS - start)) }, (_, i) => voterRow(start + i));
    return json({ totalCount: TOTAL_VOTERS, results });
  }

  // Others with the same last name, somewhere else
  let match = /^voter\/([^/]+)\/relatives$/.exec(path);
  if (match) {
    const n = numberOf(match[1]);
    return n < 0 ? notFound() : json([1, 2].map(i => {
      const { registrationDate, ...relative } = voterDetail(n + 30 * i);
      return { ...relative, lastName: voterDetail(n).lastName };
    }));
  }

  match = /^voter\/([^/]+)$/.exec(path);
  if (match) {
    const n = numberOf(match[1]);
    return n < 0 ? notFound() : json(voterDetail(n));
  }

  // The houses either side and the closest households
  match = /^address\/([^/]+)\/walk$/.exec(path);
  if (match) {
    const n = numberOf(match[1]);
    if (n < 0) return notFound();
    const house = (a: number) => ({ id: id('address', a), fullStreetAddress: voterDetail(a * 3).fullStreetAddress });
    return json({
      previous: n > 0 ? house(n - 1) : null,
      next: house(n + 1),
      nearby: [1, 2, 3, 4].map(i => ({ ...house(n + i * 6), voterCount: 1 + i % 3, distanceMeters: 40 * i * i })),
    });
  }

  match = /^address\/([^/]+)$/.exec(path);
  if (match) {
    const n = numberOf(match[1]);
    return n < 0 ? notFound() : json([0, 1, 2].map(i => voterDetail(n * 3 + i)));
  }

  if (path === 'area') {
    // The globe zoomed out over Michigan: counts per cell
    const clusters = Array.from({ length: 24 }, (_, i) => ({
      latitude: 42.2 + (i % 6) * 0.45,
      longitude: -86 + Math.floor(i / 6) * 0.8,
      count: 1000 + (i * 7919) % 250_000,
    }));
    return json({ addresses: [], clusters });
  }

  if (path.endsWith('suggestions')) {
    return json(['Alex', 'Alexander', 'Alexandra']);
  }

  return notFound();
}
