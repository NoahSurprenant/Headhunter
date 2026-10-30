import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_UNAVAILABLE, stubFetch } from '../../../testing/fetch-stub';
import { districtStats } from '../../../testing/synthetic-data';
import { DistrictRef } from '../districts';
import { DistrictCardComponent } from './district-card.component';

describe('DistrictCardComponent', () => {
  const precinct: DistrictRef = { type: 'Precinct', name: '00012', within: 'CITY OF TESTVILLE' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DistrictCardComponent],
      providers: [provideRouter([])],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(district: DistrictRef = precinct) {
    const fixture: ComponentFixture<DistrictCardComponent> = TestBed.createComponent(DistrictCardComponent);
    fixture.componentRef.setInput('district', district);
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  // The leaf elements' texts, spaced: the template has no whitespace between its elements once compiled.
  const text = (el: HTMLElement) => Array.from(el.querySelectorAll('*'))
    .filter(e => e.children.length === 0)
    .map(e => e.textContent!.trim())
    .join(' ');

  it('asks for the district\'s stats and shows its counts', async () => {
    const fetchMock = stubFetch(() => districtStats());
    const { el } = await render();

    expect(fetchMock.mock.calls[0][0]).toBe('api/district?type=Precinct&name=00012&within=CITY+OF+TESTVILLE');
    expect(text(el)).toContain('Precinct 00012, CITY OF TESTVILLE');
    expect(text(el)).toContain('1,240 voters');
    expect(text(el)).toContain('610 households');
    expect(text(el)).toContain('37.5% permanent absentee');
  });

  it('draws the ages as bars against the busiest band, each with its count', async () => {
    stubFetch(() => districtStats());
    const { el } = await render();

    const bars = Array.from(el.querySelectorAll<HTMLElement>('.age-bar'));
    expect(bars.map(b => b.style.height)).toEqual(['50%', '100%', '83%', '75%', '92%', '67%', '50%']);
    expect(bars[1].parentElement!.title).toBe('25–34: 240');
  });

  it('splits the voters by gender and lists the newest registrations', async () => {
    stubFetch(() => districtStats());
    const { el } = await render();

    expect(text(el)).toContain('Female 52.5%');
    expect(text(el)).toContain('Male 47.5%');
    const links = Array.from(el.querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(links).toContain('/voter/voter-2001');
    expect(text(el)).toContain('Jordan Q Sample');
  });

  it('links to the globe showing the district', async () => {
    stubFetch(() => districtStats());
    const { el } = await render();

    const globe = Array.from(el.querySelectorAll('a')).find(a => a.textContent!.includes('Open on globe'))!;
    expect(globe.getAttribute('href')).toBe('/globe?districtType=Precinct&district=00012&districtWithin=CITY%20OF%20TESTVILLE');
  });

  it('leaves out what a district needs no jurisdiction for', async () => {
    stubFetch(() => districtStats({ type: 'County', name: 'TESTSHIRE', within: '', genders: [{ gender: '', count: 1240 }] }));
    const { el } = await render({ type: 'County', name: 'TESTSHIRE', within: '' });

    const globe = Array.from(el.querySelectorAll('a')).find(a => a.textContent!.includes('Open on globe'))!;
    expect(globe.getAttribute('href')).toBe('/globe?districtType=County&district=TESTSHIRE');
    expect(text(el)).toContain('Not given 100%');
  });

  it('says so when nobody is registered in the district', async () => {
    const fetchMock = vi.fn(async () => new Response('', { status: 404 }));
    vi.stubGlobal('fetch', fetchMock);
    const { el } = await render();

    expect(text(el)).toContain('Nobody is registered in this district.');
  });

  it('says so when the stats cannot be loaded', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const { el } = await render();

    expect(text(el)).toContain('Couldn\'t load this district.');
  });
});
