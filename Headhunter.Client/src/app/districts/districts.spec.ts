import { DISTRICT_TYPES, districtQuery, districtRef, districtTitle, districtTypeOption } from './districts';

describe('districts', () => {
  it('offers every district type the voter file has, precincts and wards within a jurisdiction', () => {
    expect(DISTRICT_TYPES.map(t => t.value)).toEqual([
      'County', 'Jurisdiction', 'Precinct', 'Ward', 'SchoolDistrict', 'StateHouse', 'StateSenate', 'UsCongress', 'CountyCommissioner',
    ]);
    expect(districtTypeOption('Ward')?.within).toBe('Jurisdiction');
    expect(districtTypeOption('CountyCommissioner')?.within).toBe('County');
    expect(districtTypeOption('StateHouse')?.within).toBeUndefined();
    expect(districtTypeOption('Galaxy')).toBeUndefined();
  });

  it('makes a district only from a known type and a name, and what it is in when it needs one', () => {
    expect(districtRef('County', 'TESTSHIRE', 'ignored')).toEqual({ type: 'County', name: 'TESTSHIRE', within: '' });
    expect(districtRef('Precinct', '00012', 'CITY OF TESTVILLE')).toEqual({ type: 'Precinct', name: '00012', within: 'CITY OF TESTVILLE' });
    expect(districtRef('Precinct', '00012', '')).toBeUndefined();
    expect(districtRef('County', '', '')).toBeUndefined();
    expect(districtRef(null, 'TESTSHIRE', '')).toBeUndefined();
  });

  it('titles a district by its name, or its type and number, and what it is in', () => {
    expect(districtTitle({ type: 'County', name: 'TESTSHIRE', within: '' })).toBe('TESTSHIRE');
    expect(districtTitle({ type: 'Precinct', name: '00012', within: 'CITY OF TESTVILLE' })).toBe('Precinct 00012, CITY OF TESTVILLE');
    expect(districtTitle({ type: 'StateHouse', name: '1ST DISTRICT', within: '' })).toBe('State House 1ST DISTRICT');
  });

  it('asks the API by type, name and within', () => {
    expect(districtQuery({ type: 'Ward', name: '01', within: 'CITY OF TESTVILLE' }))
      .toEqual({ type: 'Ward', name: '01', within: 'CITY OF TESTVILLE' });
  });
});
