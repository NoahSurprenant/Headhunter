// The globe's zoomed-out view (GET api/area answers with addresses or clusters). No cesium import, so specs can use
// these without loading Cesium.
import type { AddressDto } from './cesium.component';

export interface AreaDto
{
  addresses: AddressDto[],
  clusters: ClusterDto[],
}

export interface ClusterDto
{
  latitude: number,
  longitude: number,
  count: number,
}

export function clusterId(c: ClusterDto): string {
  return `cluster:${c.latitude.toFixed(5)},${c.longitude.toFixed(5)},${c.count}`;
}

/** 1,234 as "1.2k", 45,678 as "46k". */
export function shortCount(n: number): string {
  if (n < 1000) return n.toString();
  if (n < 10000) return (n / 1000).toFixed(1).replace(/\.0$/, '') + 'k';
  if (n < 1000000) return Math.round(n / 1000) + 'k';
  return (n / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
}

/**
 * A cluster's dot in pixels: by its share of the view's busiest cell (square root, so area tracks the count), from 14
 * to 40, which stays inside the spacing of the 20x20 grid on a desktop screen. A single address is a plain small dot.
 */
export function clusterSize(n: number, max: number): number {
  if (n <= 1) return 8;
  return Math.round(14 + 26 * Math.sqrt(n / Math.max(max, n)));
}
