import { TestBed } from '@angular/core/testing';
import { RegistrationTimelineComponent, TimelineVoter, layoutTimeline } from './registration-timeline.component';

function voter(overrides: Partial<TimelineVoter> = {}): TimelineVoter {
  return {
    id: 'voter-0001',
    firstName: 'Alex',
    middleName: '',
    lastName: 'Example',
    birthYear: 1990,
    registrationDate: '2020-03-15T00:00:00',
    ...overrides,
  };
}

describe('layoutTimeline', () => {
  const now = new Date(2026, 9, 29);

  it('orders points earliest to latest and spreads them 0-100 by date', () => {
    const { points, unknown } = layoutTimeline(
      [
        voter({ id: 'v2', registrationDate: '2022-01-01T00:00:00' }),
        voter({ id: 'v1', registrationDate: '2012-01-01T00:00:00' }),
        voter({ id: 'v3', registrationDate: '2024-01-01T00:00:00' }),
      ],
      now,
    );
    expect(points.map(p => p.id)).toEqual(['v1', 'v2', 'v3']);
    expect(points[0].position).toBe(0);
    expect(points[points.length - 1].position).toBe(100);
    expect(points[1].position).toBeGreaterThan(0);
    expect(points[1].position).toBeLessThan(100);
    expect(unknown).toEqual([]);
  });

  it('centres a single dated voter', () => {
    const { points } = layoutTimeline([voter()], now);
    expect(points[0].position).toBe(50);
  });

  it('spreads voters evenly when they all share the exact same date', () => {
    const { points } = layoutTimeline(
      [voter({ id: 'v1' }), voter({ id: 'v2' }), voter({ id: 'v3' })],
      now,
    );
    expect(points.map(p => p.position)).toEqual([0, 50, 100]);
  });

  it('keeps 1900-01-01 placeholder, unparseable and future dates out of the track', () => {
    const { points, unknown } = layoutTimeline(
      [
        voter({ id: 'placeholder', registrationDate: '1900-01-01T00:00:00' }),
        voter({ id: 'garbage', registrationDate: 'not-a-date' }),
        voter({ id: 'future', registrationDate: '2099-01-01T00:00:00' }),
        voter({ id: 'missing', registrationDate: '' }),
        voter({ id: 'real', registrationDate: '2015-06-01T00:00:00' }),
      ],
      now,
    );
    expect(points.map(p => p.id)).toEqual(['real']);
    expect(unknown.map(v => v.id).sort()).toEqual(['future', 'garbage', 'missing', 'placeholder']);
  });

  it('reports no points and all-unknown when nobody has a usable date', () => {
    const { points, unknown } = layoutTimeline([voter({ registrationDate: '1900-01-01T00:00:00' })], now);
    expect(points).toEqual([]);
    expect(unknown.length).toBe(1);
  });
});

describe('RegistrationTimelineComponent', () => {
  it('renders a dot and label for each dated voter, and a footnote for unknown dates', async () => {
    const fixture = TestBed.createComponent(RegistrationTimelineComponent);
    fixture.componentRef.setInput('voters', [
      voter({ id: 'v1', firstName: 'Jordan', lastName: 'Sample', registrationDate: '2012-01-01T00:00:00' }),
      voter({ id: 'v2', firstName: 'Alex', lastName: 'Example', registrationDate: '1900-01-01T00:00:00' }),
    ]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.textContent).toContain('Jordan Sample');
    expect(el.textContent).toContain('Registration date unknown for Alex Example');
  });

  it('shows a message instead of a track when nobody has a usable date', async () => {
    const fixture = TestBed.createComponent(RegistrationTimelineComponent);
    fixture.componentRef.setInput('voters', [voter({ registrationDate: '1900-01-01T00:00:00' })]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No usable registration dates to plot.');
  });
});
