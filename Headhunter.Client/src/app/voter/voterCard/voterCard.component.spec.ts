import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { voterDetail } from '../../../testing/synthetic-data';
import { VoterCardComponent } from './voterCard.component';

describe('VoterCardComponent', () => {
  function render(voter = voterDetail(), displayAddress?: boolean) {
    TestBed.configureTestingModule({
      imports: [VoterCardComponent],
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(VoterCardComponent);
    fixture.componentRef.setInput('voter', voter);
    if (displayAddress !== undefined)
      fixture.componentRef.setInput('displayAddress', displayAddress);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows initials, the full name with the middle name highlighted, and the birth year', () => {
    const el = render();
    const text = el.textContent!.replace(/\s+/g, ' ');
    expect(text).toContain('AE');
    expect(text).toContain('Alex Quinn Example');
    expect(el.querySelector('.text-purple-500')?.textContent).toBe('Quinn');
    expect(text).toContain('Born 1990');
  });

  it('omits the middle name when there is none', () => {
    const el = render(voterDetail({ middleName: '' }));
    expect(el.querySelector('.text-purple-500')).toBeNull();
    expect(el.textContent!.replace(/\s+/g, ' ')).toContain('Alex Example');
  });

  it('formats the registration date as a long date', () => {
    const el = render();
    expect(el.textContent).toContain('Registered: March 15, 2020');
  });

  it('links the address to the address page by default', () => {
    const el = render();
    const link = el.querySelector('a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/address/addr-0001');
    expect(link.textContent).toContain('123 Fake St');
    expect(link.textContent).toContain('Testville, MI 49999');
  });

  it('hides the address when displayAddress is false', () => {
    const el = render(voterDetail(), false);
    expect(el.querySelector('a')).toBeNull();
    expect(el.textContent).not.toContain('123 Fake St');
  });
});
