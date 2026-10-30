// Small display helpers shared by the voter, address and voters pages.

/** "about 43": the voter file has the birth year only, so the age is within a year. */
export function approximateAge(birthYear: number, now = new Date()): number {
  return now.getFullYear() - birthYear;
}

export function genderLabel(gender: string | null | undefined): string | undefined {
  switch ((gender ?? '').toUpperCase()) {
    case 'M': return 'Male';
    case 'F': return 'Female';
    case '': return undefined;
    default: return gender ?? undefined;
  }
}

export function fullName(p: { firstName: string; middleName?: string | null; lastName: string }): string {
  return [p.firstName, p.middleName, p.lastName].filter(x => !!x).join(' ');
}

/**
 * Voter files sometimes carry 1900-01-01 (or earlier) as a "we don't actually know" placeholder for a date
 * field rather than leaving it null. Anything on or before that, unparseable, or in the future isn't a real
 * registration date.
 */
const EARLIEST_PLAUSIBLE_REGISTRATION = new Date(1900, 0, 1);

/** The registration date as a real `Date`, or `null` if it's missing, unparseable, or a known placeholder. */
export function registrationDateOrNull(value: string | null | undefined, now = new Date()): Date | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  if (date <= EARLIEST_PLAUSIBLE_REGISTRATION || date > now) return null;
  return date;
}

/** "3 years ago" / "1 year ago", or `null` if the registration date is missing/unknown (see registrationDateOrNull). */
export function yearsAgoLabel(value: string | null | undefined, now = new Date()): string | null {
  const date = registrationDateOrNull(value, now);
  if (!date) return null;
  let years = now.getFullYear() - date.getFullYear();
  const anniversary = new Date(now.getFullYear(), date.getMonth(), date.getDate());
  if (now < anniversary) years--;
  years = Math.max(0, years);
  return years === 0 ? 'less than a year ago' : `${years} year${years === 1 ? '' : 's'} ago`;
}

export function initials(p: { firstName: string; lastName: string }): string {
  return (p.firstName?.[0] ?? '') + (p.lastName?.[0] ?? '');
}

/** Query parameters for the globe, to fly to an address and open it. */
export function globeParams(latitude: number | null | undefined, longitude: number | null | undefined, addressId: string)
  : Record<string, string | number> | null {
  if (latitude === null || latitude === undefined || longitude === null || longitude === undefined) return null;
  return { lat: latitude, lon: longitude, address: addressId };
}

/** "420 ft" under a mile, "1.3 mi" past it: the walking-distance units US addresses are read in. */
export function formatDistance(meters: number): string {
  const feet = meters * 3.28084;
  if (feet < 1000) return `${Math.round(feet)} ft`;
  return `${(meters / 1609.344).toFixed(1)} mi`;
}
