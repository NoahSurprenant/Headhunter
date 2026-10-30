import { boxFromCorners, boxFromParams, boxParams, isEmptyBox } from './area-selection';

describe('area selection', () => {
  const params = (values: Record<string, string>) => (name: string) => values[name] ?? null;

  it('makes the box between two dragged corners, whichever way the drag went', () => {
    const box = { west: -84, east: -83, north: 43, south: 42 };
    expect(boxFromCorners({ longitude: -84, latitude: 43 }, { longitude: -83, latitude: 42 })).toEqual(box);
    expect(boxFromCorners({ longitude: -83, latitude: 42 }, { longitude: -84, latitude: 43 })).toEqual(box);
  });

  it('crosses the date line when that is the shorter way round', () => {
    expect(boxFromCorners({ longitude: 179, latitude: 11 }, { longitude: -179, latitude: 9 }))
      .toEqual({ west: 179, east: -179, north: 11, south: 9 });
  });

  it('rounds to six decimals', () => {
    expect(boxFromCorners({ longitude: -84.1234567891, latitude: 42.9876543219 }, { longitude: -84, latitude: 43 }))
      .toEqual({ west: -84.123457, east: -84, north: 43, south: 42.987654 });
  });

  it('calls a box without width or height empty', () => {
    expect(isEmptyBox({ west: -84, east: -84, north: 43, south: 42 })).toBe(true);
    expect(isEmptyBox({ west: -84, east: -83, north: 42, south: 42 })).toBe(true);
    expect(isEmptyBox({ west: -84, east: -83, north: 43, south: 42 })).toBe(false);
  });

  it('reads the box from query parameters', () => {
    expect(boxFromParams(params({ west: '179', east: '-179', north: '11', south: '9' })))
      .toEqual({ west: 179, east: -179, north: 11, south: 9 });
  });

  it('ignores a missing, malformed or impossible box in the query', () => {
    expect(boxFromParams(params({}))).toBeUndefined();
    expect(boxFromParams(params({ west: '-84', east: '-83', north: '43' }))).toBeUndefined();
    expect(boxFromParams(params({ west: '-84', east: '-83', north: '43', south: '' }))).toBeUndefined();
    expect(boxFromParams(params({ west: 'x', east: '-83', north: '43', south: '42' }))).toBeUndefined();
    expect(boxFromParams(params({ west: '-190', east: '-83', north: '43', south: '42' }))).toBeUndefined();
    expect(boxFromParams(params({ west: '-84', east: '-83', north: '95', south: '42' }))).toBeUndefined();
    expect(boxFromParams(params({ west: '-84', east: '-83', north: '42', south: '43' }))).toBeUndefined();
  });

  it('writes the box as query parameters, and nulls to remove it', () => {
    expect(boxParams({ west: -84, east: -83, north: 43, south: 42 })).toEqual({ west: -84, east: -83, north: 43, south: 42 });
    expect(boxParams(undefined)).toEqual({ west: null, east: null, north: null, south: null });
  });
});
