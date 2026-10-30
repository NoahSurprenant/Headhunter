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
  // The asked-for metric over the cell's voters (api/area?metric=); absent without one, or with no voters.
  value?: number,
}

/** What clusters can be coloured by: the API's ClusterMetric. */
export type ClusterMetric = 'None' | 'MedianAge' | 'PermanentAbsentee' | 'RecentlyRegistered' | 'Female';

export interface MetricOption
{
  metric: ClusterMetric,
  label: string,
  // A value as the legend and tooltip show it, e.g. "47 yrs" or "32%".
  format: (value: number) => string,
  // How a cluster's tooltip names it, after the value: "32% permanent absentee".
  noun: string,
}

const percent = (value: number) => (value < 10 ? value.toFixed(1).replace(/\.0$/, '') : Math.round(value).toString()) + '%';

export const metricOptions: MetricOption[] = [
  { metric: 'None', label: 'None', format: v => v.toString(), noun: '' },
  { metric: 'MedianAge', label: 'Median age', format: v => Math.round(v) + ' yrs', noun: 'median age' },
  { metric: 'PermanentAbsentee', label: '% permanent absentee', format: percent, noun: 'permanent absentee' },
  { metric: 'RecentlyRegistered', label: '% registered in the last 12 months', format: percent, noun: 'registered in the last 12 months' },
  { metric: 'Female', label: '% female', format: percent, noun: 'female' },
];

export function metricOption(metric: ClusterMetric): MetricOption {
  return metricOptions.find(o => o.metric === metric) ?? metricOptions[0];
}

// Coloured clusters get their own ids: switching metric replaces the entities (and their names) instead of keeping
// the old ones.
export function clusterId(c: ClusterDto, metric: ClusterMetric = 'None'): string {
  const prefix = metric === 'None' ? 'cluster:' : `cluster:${metric}:`;
  return `${prefix}${c.latitude.toFixed(5)},${c.longitude.toFixed(5)},${c.count}`;
}

/** "12,345 addresses", and with a metric "12,345 addresses · median age 47 yrs" or "· 32% female". */
export function clusterName(c: ClusterDto, metric: ClusterMetric): string {
  const name = c.count.toLocaleString() + (c.count === 1 ? ' address' : ' addresses');
  if (metric === 'None') return name;
  const option = metricOption(metric);
  if (c.value === undefined || c.value === null) return name + ' · no voters';
  return name + ' · ' + (metric === 'MedianAge'
    ? option.noun + ' ' + option.format(c.value)
    : option.format(c.value) + ' ' + option.noun);
}

/** The lowest and highest value among the clusters in view, or undefined when none has one. */
export function valueRange(clusters: ClusterDto[]): { min: number, max: number } | undefined {
  const values = clusters.map(c => c.value).filter((v): v is number => typeof v === 'number');
  if (values.length === 0) return undefined;
  return { min: Math.min(...values), max: Math.max(...values) };
}

// Viridis (matplotlib's perceptually uniform sequential scale, readable for colour-blind viewers), without its
// darkest purple, which disappears on the satellite imagery. Low values are blue-purple, high ones yellow.
export const scaleStops = ['#482878', '#3e4989', '#31688e', '#26828e', '#1f9e89', '#35b779', '#6ece58', '#b5de2b', '#fde725'];

/** The scale's colour for a value within the view's range, as #rrggbb. A range of one value sits mid-scale. */
export function scaleColor(value: number, range: { min: number, max: number }): string {
  const span = range.max - range.min;
  const t = span > 0 ? Math.min(1, Math.max(0, (value - range.min) / span)) : 0.5;
  const position = t * (scaleStops.length - 1);
  const i = Math.min(Math.floor(position), scaleStops.length - 2);
  const from = hex(scaleStops[i]);
  const to = hex(scaleStops[i + 1]);
  const f = position - i;
  return '#' + from.map((channel, k) => Math.round(channel + (to[k] - channel) * f).toString(16).padStart(2, '0')).join('');
}

function hex(color: string): number[] {
  return [1, 3, 5].map(i => parseInt(color.slice(i, i + 2), 16));
}

/** The legend's gradient: the same stops, left (low) to right (high). */
export const scaleGradient = `linear-gradient(to right, ${scaleStops.join(', ')})`;

/** A cluster label's font for a dot of this many pixels: about four characters ("1.2k", "355k") fit inside. */
export function clusterLabelFont(size: number): string {
  const px = size >= 34 ? 12 : size >= 24 ? 10 : 9;
  return `bold ${px}px sans-serif`;
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
