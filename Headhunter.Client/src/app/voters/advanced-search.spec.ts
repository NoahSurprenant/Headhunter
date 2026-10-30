import {
  appendConditionParams, ConditionRow, decodeConditions, encodeConditions, isComplete, MAX_CONDITIONS, newRow, operatorsFor,
  toDto, withField,
} from './advanced-search';

const row = (field: string, op: string, value = '', value2 = ''): ConditionRow => ({ id: 0, field, op, value, value2 });
const withoutIds = (rows: ConditionRow[]) => rows.map(r => ({ field: r.field, op: r.op, value: r.value, value2: r.value2 }));

describe('advanced search', () => {
  it('encodes rows compactly, as the URL keeps them', () => {
    expect(encodeConditions([row('birthYear', 'between', '1990', '1999'), row('lastName', 'startsWith', 'SUR')]))
      .toBe('birthYear.between.1990.1999~lastName.startsWith.SUR');
    // value2 only means something for between
    expect(encodeConditions([row('birthYear', 'eq', '1990', 'left over')])).toBe('birthYear.eq.1990');
  });

  it('round-trips values with the separators, percent signs and other odd characters in them', () => {
    const rows = [
      row('city', 'equals', 'ST. CLAIR SHORES'),
      row('street', 'contains', '50% ~ off.'),
      row('lastName', 'startsWith', "O'BRIEN-SMITH & CO"),
      row('registrationDate', 'between', '2020-01-01', '2020-12-31'),
      row('lastName', 'equals', ''),
    ];

    const encoded = encodeConditions(rows);
    expect(encoded.split('~')).toHaveLength(5);
    expect(withoutIds(decodeConditions(encoded))).toEqual(withoutIds(rows));
  });

  it('leaves out unknown fields and operators, and stops at the API\'s limit', () => {
    expect(withoutIds(decodeConditions('ssn.equals.1~lastName.gt.A~birthYear.startsWith.19~gender.is.F')))
      .toEqual([{ field: 'gender', op: 'is', value: 'F', value2: '' }]);
    expect(decodeConditions(Array(15).fill('age.eq.30').join('~'))).toHaveLength(MAX_CONDITIONS);
    expect(decodeConditions(null)).toEqual([]);
    expect(decodeConditions('')).toEqual([]);
    // A malformed escape is taken as it is
    expect(decodeConditions('lastName.equals.100%')[0].value).toBe('100%');
  });

  it('sends only the rows that are filled in, trimmed', () => {
    expect(toDto([
      row('lastName', 'startsWith', ' SUR '),
      row('city', 'equals', '  '),
      row('age', 'between', '30', ''),
      row('age', 'between', '30', '39'),
    ])).toEqual([
      { field: 'lastName', operator: 'startsWith', value: 'SUR' },
      { field: 'age', operator: 'between', value: '30', value2: '39' },
    ]);
    expect(isComplete(row('age', 'between', '30', '39'))).toBe(true);
  });

  it('starts each type with an operator that can use an index', () => {
    expect(newRow()).toEqual(expect.objectContaining({ field: 'lastName', op: 'startsWith', value: '' }));
    expect(newRow('birthYear').op).toBe('eq');
    expect(newRow('registrationDate').op).toBe('ge');
    expect(newRow('gender', [{ value: 'F', label: 'Female' }])).toEqual(expect.objectContaining({ op: 'is', value: 'F' }));
    expect(newRow('voterStatus').value).toBe('');
  });

  it('keeps what still fits when the field changes', () => {
    // Text to text: the operator and the value
    expect(withField(row('firstName', 'contains', 'ANN'), 'lastName')).toEqual(row('lastName', 'contains', 'ANN'));
    // Number to number: the operator, not the value (a year isn't an age)
    expect(withField(row('birthYear', 'between', '1990', '1999'), 'age')).toEqual(row('age', 'between'));
    // Another type starts over
    expect(withField(row('lastName', 'contains', 'ANN'), 'birthYear')).toEqual(row('birthYear', 'eq'));
    expect(withField(row('gender', 'isNot', 'M'), 'permanentAbsentee', [{ value: 'yes', label: 'Yes' }]))
      .toEqual(row('permanentAbsentee', 'is', 'yes'));
    expect(withField(row('lastName', 'equals', 'X'), 'nope')).toEqual(row('lastName', 'equals', 'X'));
  });

  it('falls back to the text operators for an unknown field', () => {
    expect(operatorsFor('nope').map(o => o.value)).toEqual(['equals', 'notEquals', 'startsWith', 'endsWith', 'contains']);
  });

  it('writes the CSV download\'s conditions as ASP.NET binds them', () => {
    const query = new URLSearchParams({ lastName: 'X' });
    appendConditionParams(query, [
      { field: 'age', operator: 'between', value: '30', value2: '39' },
      { field: 'city', operator: 'equals', value: 'ST. CLAIR' },
    ]);
    expect(query.toString()).toBe('lastName=X'
      + '&conditions%5B0%5D.field=age&conditions%5B0%5D.operator=between&conditions%5B0%5D.value=30&conditions%5B0%5D.value2=39'
      + '&conditions%5B1%5D.field=city&conditions%5B1%5D.operator=equals&conditions%5B1%5D.value=ST.+CLAIR');
  });
});
