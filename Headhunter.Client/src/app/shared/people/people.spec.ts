import { approximateAge, formatDistance, fullName, genderLabel, globeParams, initials, registrationDateOrNull, yearsAgoLabel } from './people';

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

  describe('registrationDateOrNull', () => {
    const now = new Date(2026, 9, 29);

    it('parses a real registration date', () => {
      expect(registrationDateOrNull('2020-03-15T00:00:00', now)).toEqual(new Date('2020-03-15T00:00:00'));
    });

    it('treats the 1900-01-01 placeholder, and anything earlier, as unknown', () => {
      expect(registrationDateOrNull('1900-01-01T00:00:00', now)).toBeNull();
      expect(registrationDateOrNull('1850-01-01T00:00:00', now)).toBeNull();
    });

    it('treats missing, unparseable and future dates as unknown', () => {
      expect(registrationDateOrNull(null, now)).toBeNull();
      expect(registrationDateOrNull('', now)).toBeNull();
      expect(registrationDateOrNull('not-a-date', now)).toBeNull();
      expect(registrationDateOrNull('2099-01-01T00:00:00', now)).toBeNull();
    });
  });

  describe('yearsAgoLabel', () => {
    const now = new Date(2026, 9, 29);

    it('pluralizes correctly', () => {
      expect(yearsAgoLabel('2025-10-01T00:00:00', now)).toBe('1 year ago');
      expect(yearsAgoLabel('2020-03-15T00:00:00', now)).toBe('6 years ago');
    });

    it('calls out a date less than a year ago instead of saying "0 years ago"', () => {
      expect(yearsAgoLabel('2026-06-01T00:00:00', now)).toBe('less than a year ago');
    });

    it('is null for an unknown/placeholder registration date', () => {
      expect(yearsAgoLabel('1900-01-01T00:00:00', now)).toBeNull();
      expect(yearsAgoLabel(null, now)).toBeNull();
    });
  });
});
