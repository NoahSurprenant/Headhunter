import { approximateAge, formatDistance, fullName, genderLabel, globeParams, initials } from './people';

describe('people helpers', () => {
  it('works out the age from the birth year', () => {
    expect(approximateAge(1990, new Date(2026, 9, 29))).toBe(36);
  });

  it("spells out the voter file's gender codes", () => {
    expect(genderLabel('M')).toBe('Male');
    expect(genderLabel('f')).toBe('Female');
    expect(genderLabel('')).toBeUndefined();
    expect(genderLabel('X')).toBe('X');
  });

  it('joins names, skipping an empty middle name', () => {
    expect(fullName({ firstName: 'Alex', middleName: 'Quinn', lastName: 'Example' })).toBe('Alex Quinn Example');
    expect(fullName({ firstName: 'Alex', middleName: '', lastName: 'Example' })).toBe('Alex Example');
    expect(initials({ firstName: 'Alex', lastName: 'Example' })).toBe('AE');
  });

  it('builds globe links only for placed addresses', () => {
    expect(globeParams(42.3, -83.1, 'a1')).toEqual({ lat: 42.3, lon: -83.1, address: 'a1' });
    expect(globeParams(null, -83.1, 'a1')).toBeNull();
  });

  it('formats walking distance in feet under a mile, miles past it', () => {
    expect(formatDistance(30)).toBe('98 ft');
    expect(formatDistance(2000)).toBe('1.2 mi');
  });
});
