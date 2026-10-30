// A box drawn on the globe (the voters page's "Select area"). No cesium import, so specs can use these without
// loading Cesium.

/** In degrees. West > east means the box crosses the date line, as for GET api/area. */
export interface AreaBox
{
  west: number,
  east: number,
  north: number,
  south: number,
}

export interface Corner
{
  longitude: number,
  latitude: number,
}

/** The id of the box's entity on the globe. */
export const SELECTED_AREA_ID = 'selected-area';

/**
 * The box between two dragged corners. Of the two ways round the globe between their longitudes it takes the
 * shorter, so a drag across the date line is a narrow box that crosses it, not one around the rest of the world.
 * Six decimals (about 10 cm) keep the address bar readable.
 */
export function boxFromCorners(a: Corner, b: Corner): AreaBox {
  const crossesDateLine = Math.abs(a.longitude - b.longitude) > 180;
  return {
    west: round(crossesDateLine ? Math.max(a.longitude, b.longitude) : Math.min(a.longitude, b.longitude)),
    east: round(crossesDateLine ? Math.min(a.longitude, b.longitude) : Math.max(a.longitude, b.longitude)),
    north: round(Math.max(a.latitude, b.latitude)),
    south: round(Math.min(a.latitude, b.latitude)),
  };
}

/** A click without a drag makes a box with no area: not a selection. */
export function isEmptyBox(box: AreaBox): boolean {
  return box.west === box.east || box.north === box.south;
}

/** The box in ?west=&east=&north=&south=, or undefined when any is missing or out of range. */
export function boxFromParams(get: (name: string) => string | null): AreaBox | undefined {
  const [west, east, north, south] = ['west', 'east', 'north', 'south'].map(name => {
    const value = get(name);
    return value === null || value.trim() === '' ? NaN : Number(value);
  });
  const longitude = (n: number) => Number.isFinite(n) && n >= -180 && n <= 180;
  const latitude = (n: number) => Number.isFinite(n) && n >= -90 && n <= 90;
  if (!longitude(west) || !longitude(east) || !latitude(north) || !latitude(south) || south > north) {
    return undefined;
  }
  return { west, east, north, south };
}

/** Query parameters for the box; with none, nulls, which the router drops from the URL. */
export function boxParams(box: AreaBox | undefined): Record<keyof AreaBox, number | null> {
  return {
    west: box?.west ?? null,
    east: box?.east ?? null,
    north: box?.north ?? null,
    south: box?.south ?? null,
  };
}

function round(degrees: number): number {
  return Math.round(degrees * 1e6) / 1e6;
}
