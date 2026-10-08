import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AppComponent } from './app.component';

@Component({ template: '<p class="stub">stub page</p>' })
class StubPageComponent {}

describe('AppComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([{ path: 'stub', component: StubPageComponent }])],
    });
  });

  it('creates the root component with a router outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.nativeElement.querySelector('router-outlet')).not.toBeNull();
  });

  it('renders the routed page inside its outlet', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl('/stub');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.stub')?.textContent).toBe('stub page');
  });
});
