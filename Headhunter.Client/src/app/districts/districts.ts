// The districts every address is in (Headhunter.API/Districts.cs). No cesium import, so specs and the voters page's
// form can use these without loading Cesium.

export type DistrictType =
  | 'County'
  | 'Jurisdiction'
  | 'Precinct'
  | 'Ward'
  | 'SchoolDistrict'
  | 'StateHouse'
  | 'StateSenate'
  | 'UsCongress'
  | 'CountyCommissioner';

export interface DistrictTypeOption {
  value: DistrictType,
  label: string,
  /** Precincts and wards are numbered within a jurisdiction, commissioner districts within a county. */
  within?: 'Jurisdiction' | 'County',
}

export const DISTRICT_TYPES: DistrictTypeOption[] = [
  { value: 'County', label: 'County' },
  { value: 'Jurisdiction', label: 'Jurisdiction' },
  { value: 'Precinct', label: 'Precinct', within: 'Jurisdiction' },
  { value: 'Ward', label: 'Ward', within: 'Jurisdiction' },
  { value: 'SchoolDistrict', label: 'School district' },
  { value: 'StateHouse', label: 'State House' },
  { value: 'StateSenate', label: 'State Senate' },
  { value: 'UsCongress', label: 'US Congress' },
  { value: 'CountyCommissioner', label: 'County Commissioner', within: 'County' },
];

export function districtTypeOption(type: string | null | undefined): DistrictTypeOption | undefined {
  return DISTRICT_TYPES.find(t => t.value === type);
}

/** A district picked: its type, its name and, for precincts, wards and commissioner districts, what it is in. */
export interface DistrictRef {
  type: DistrictType,
  name: string,
  within: string,
}

/** A district from loose parts (form values, query parameters), or none if any needed part is missing. */
export function districtRef(type: string | null | undefined, name: string | null | undefined,
  within: string | null | undefined): DistrictRef | undefined {
  const option = districtTypeOption(type);
  if (!option || !name) return undefined;
  if (option.within && !within) return undefined;
  return { type: option.value, name, within: option.within ? within! : '' };
}

/** "Precinct 00012, CITY OF LANSING". */
export function districtTitle(d: DistrictRef): string {
  const label = districtTypeOption(d.type)!.label;
  const named = d.type === 'County' || d.type === 'Jurisdiction' || d.type === 'SchoolDistrict' ? d.name : `${label} ${d.name}`;
  return d.within ? `${named}, ${d.within}` : named;
}

/** The query parameters the API's district endpoints take. */
export function districtQuery(d: DistrictRef): Record<string, string> {
  return { type: d.type, name: d.name, within: d.within };
}

export interface DistrictExtentDto
{
  west: number,
  east: number,
  south: number,
  north: number,
}

export interface DistrictStatsDto
{
  type: DistrictType,
  name: string,
  within: string,
  voters: number,
  households: number,
  ages: { label: string, count: number }[],
  genders: { gender: string, count: number }[],
  permanentAbsenteePercent: number,
  newestRegistrations: { id: string, firstName: string, middleName: string, lastName: string, registrationDate: string }[],
  extent: DistrictExtentDto | null,
}
